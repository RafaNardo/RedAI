using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RedAI.Application;
using RedAI.Domain;
using RedAI.Infrastructure;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Npgsql;
using System.Net;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<InMemoryCampaignStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddDbContext<RedAIDbContext>(o => o.UseNpgsql(ResolveConnectionString(builder.Configuration)));
builder.Services.AddScoped<IAssetStorage, LocalAssetStorage>();
builder.Services.AddScoped<IAIRunWriter, EfAIRunWriter>();
var bundledContracts = Path.Combine(builder.Environment.ContentRootPath, "contracts");
var repositoryContracts = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "..", "docs", "contracts"));
builder.Services.AddSingleton<IContractSchemaCatalog>(_ => new FileContractSchemaCatalog(Directory.Exists(bundledContracts) ? bundledContracts : repositoryContracts));
builder.Services.AddScoped<IDeterministicCreativeRenderer, PlaywrightCreativeRenderer>();
builder.Services.AddHttpClient<OpenAIResponsesClient>(client => client.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddHttpClient("brand-website");
builder.Services.AddSingleton<MockAIClient>();
builder.Services.AddScoped<IAIClient>(services => string.Equals(builder.Configuration["AI:Mode"] ?? Environment.GetEnvironmentVariable("AI_MODE"), "openai", StringComparison.OrdinalIgnoreCase)
    ? services.GetRequiredService<OpenAIResponsesClient>()
    : services.GetRequiredService<MockAIClient>());
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
await ApplyMigrationsWithRetryAsync(app.Services, app.Logger, app.Lifetime.ApplicationStopping);
app.UseCors();
var root = builder.Configuration["ASSET_STORAGE_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "assets");
Directory.CreateDirectory(root);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root), RequestPath = "/assets" });

var api = app.MapGroup("/api");
if (app.Environment.IsDevelopment()) api.MapGet("/dev/creative-templates", async (IDeterministicCreativeRenderer renderer, CancellationToken ct) =>
{
    var samples = new[]
    {
        ("editorial-bold", "Proteção começa com escolhas bem informadas", "Planeje hoje para viver com mais tranquilidade amanhã.", "Fale com um especialista", new CreativePalette("#0B0D10", "#F8F5EE", "#FF5A36")),
        ("minimal-center", "Clareza para decidir melhor", "Informação simples para proteger o que importa.", "Conheça as opções", new CreativePalette("#F7F2EA", "#13233A", "#E56A3C")),
        ("statement", "Seu futuro merece atenção", "Uma escolha de cada vez, com confiança.", "Planeje agora", new CreativePalette("#15263B", "#F8F5EE", "#F1B24A")),
        ("split-image", "Cuidar é estar presente", "Proteção para os seus próximos passos.", "Converse com a gente", new CreativePalette("#EAE4DA", "#13233A", "#D65C40")),
        ("educational", "O que observar antes de contratar", "Entenda coberturas, prazos e a proteção ideal para sua realidade.", "Saiba mais", new CreativePalette("#F8F5EE", "#173653", "#E75C3B")),
        ("promotional", "Proteção que acompanha seus planos", "Soluções claras para quem quer seguir em frente.", "Fale com a equipe", new CreativePalette("#F6F1E8", "#142D4A", "#ED663E"))
    };
    var output = new List<object>();
    foreach (var sample in samples)
    {
        var key = $"development/creative-templates/{sample.Item1}.png";
        await renderer.RenderPngAsync(new DeterministicCreativeRenderRequest(new CreativeLayout(sample.Item1, sample.Item5, new CreativeHeadline(sample.Item2, sample.Item1 is "minimal-center" or "statement" ? "center" : "left", sample.Item1 is "editorial-bold" or "statement" ? "2xl" : "xl", ["Proteção"]), new CreativeLogo("bottom-right"), sample.Item3, sample.Item4), key), ct);
        output.Add(new { template = sample.Item1, key, url = $"/assets/{key}" });
    }
    return Results.Ok(output);
});
api.MapGet("/health", (IConfiguration c) => Results.Ok(new { status = "ok", aiMode = c["AI:Mode"] ?? "mock" }));
api.MapGet("/projects", async (RedAIDbContext db) => await db.Projects.AsNoTracking().Include(p => p.Campaign).OrderByDescending(p => p.UpdatedAt).ToListAsync());
api.MapPost("/projects", async (CreateProject r, RedAIDbContext db) => {
    if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 160) return Results.BadRequest(new { error = "name is required and limited to 160 characters" });
    var p = new Project { Name = r.Name.Trim(), InstagramHandle = r.InstagramHandle, WebsiteUrl = r.WebsiteUrl, ManualContext = r.ManualContext };
    db.Projects.Add(p); await db.SaveChangesAsync(); return Results.Created($"/api/projects/{p.Id}", p);
});
api.MapGet("/projects/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Projects.AsNoTracking().Include(p => p.Campaign).FirstOrDefaultAsync(p => p.Id == id) is { } p ? Results.Ok(p) : Results.NotFound());
api.MapDelete("/projects/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Projects.FindAsync(id) is { } p ? await Delete(db, p) : Results.NotFound());

api.MapPost("/projects/{id:guid}/sources", async (Guid id, HttpRequest request, RedAIDbContext db, IAssetStorage storage, CancellationToken ct) => {
    if (!await db.Projects.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data required" });
    var saved = new List<BrandSource>();
    foreach (var file in (await request.ReadFormAsync(ct)).Files) {
        if (file.Length == 0) continue;
        var key = $"projects/{id}/sources/{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}";
        await using var stream = file.OpenReadStream(); var asset = await storage.PutAsync(stream, key, file.ContentType ?? "application/octet-stream", ct);
        saved.Add(new BrandSource { ProjectId = id, Type = "image", OriginalFilename = file.FileName, MimeType = asset.ContentType, StorageKey = asset.StorageKey });
    }
    db.BrandSources.AddRange(saved); await db.SaveChangesAsync(ct); return Results.Ok(saved);
});
api.MapGet("/projects/{id:guid}/sources", async (Guid id, RedAIDbContext db) => !await db.Projects.AnyAsync(p => p.Id == id) ? Results.NotFound() : Results.Ok(await db.BrandSources.Where(s => s.ProjectId == id).OrderBy(s => s.CreatedAt).ToListAsync()));
api.MapDelete("/projects/{id:guid}/sources/{sourceId:guid}", async (Guid id, Guid sourceId, RedAIDbContext db, IAssetStorage storage, CancellationToken ct) => await db.BrandSources.FirstOrDefaultAsync(s => s.Id == sourceId && s.ProjectId == id, ct) is not { } source ? Results.NotFound() : await DeleteSource(db, storage, source, ct));

api.MapPost("/projects/{id:guid}/brand/analyze", async (Guid id, RedAIDbContext db, IServiceProvider services, CancellationToken ct) => !await db.Projects.AnyAsync(p => p.Id == id, ct) ? Results.NotFound() : await RunSynchronously(db, services, "brand-analysis", "project", id, 1, async (sp, token) => await MaterializeBrand(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IAIClient>(), sp.GetRequiredService<IContractSchemaCatalog>(), sp.GetRequiredService<IAssetStorage>(), sp.GetRequiredService<IHttpClientFactory>(), id, token), ct));
api.MapGet("/projects/{id:guid}/brand", async (Guid id, RedAIDbContext db) => await db.BrandProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id) is { } b ? Results.Content(b.ProfileJson, "application/json") : Results.NotFound());
api.MapPut("/projects/{id:guid}/brand", async (Guid id, JsonElement profile, RedAIDbContext db) => await SaveBrand(db, id, profile.GetRawText()));
api.MapPost("/projects/{id:guid}/brand/approve", async (Guid id, RedAIDbContext db) => await ApproveBrand(db, id));

api.MapPost("/projects/{id:guid}/campaign", async (Guid id, CreateCampaign r, RedAIDbContext db) => {
    var p = await db.Projects.Include(x => x.Campaign).FirstOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound(); if (p.Campaign is not null) return Results.Conflict(new { error = "Project already has a campaign" });
    var c = new Campaign { ProjectId = id, Name = r.Name, Objective = r.Objective ?? "AI decide", TargetCount = 5, Context = r.Context }; db.Campaigns.Add(c); p.CurrentStep = "strategy"; p.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Created($"/api/campaigns/{c.Id}", c);
});
api.MapGet("/campaigns/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(c) : Results.NotFound());
api.MapPost("/campaigns/{id:guid}/strategy/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await db.Campaigns.AnyAsync(c => c.Id == id) ? await Start(db, jobs, "strategy-generation", "campaign", id, 1, async (sp, ct) => await MaterializeStrategy(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IAIClient>(), sp.GetRequiredService<IContractSchemaCatalog>(), id, ct)) : Results.NotFound());
api.MapGet("/campaigns/{id:guid}/strategy", async (Guid id, RedAIDbContext db) => await db.CampaignStrategies.AsNoTracking().FirstOrDefaultAsync(s => s.CampaignId == id) is { } strategy ? Results.Content(strategy.StrategyJson, "application/json") : Results.NotFound());
api.MapPut("/campaigns/{id:guid}/strategy", async (Guid id, JsonElement strategy, RedAIDbContext db) => await SaveStrategy(db, id, strategy.GetRawText()));
api.MapPost("/campaigns/{id:guid}/strategy/approve", async (Guid id, RedAIDbContext db) => await ApproveStrategy(db, id));

api.MapPost("/campaigns/{id:guid}/ideas/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateRoutes(db, jobs, id));
api.MapGet("/campaigns/{id:guid}/ideas", async (Guid id, RedAIDbContext db) => !await db.Campaigns.AnyAsync(c => c.Id == id) ? Results.NotFound() : Results.Ok(await db.ContentIdeas.Where(i => i.CampaignId == id).OrderBy(i => i.Ordinal).ToListAsync()));
api.MapPost("/campaigns/{id:guid}/ideas/select", async (Guid id, SelectIdeas r, RedAIDbContext db) => await SelectIdeas(db, id, r.IdeaIds));
api.MapPost("/campaigns/{id:guid}/ideas/auto-select", async (Guid id, RedAIDbContext db) => await SelectIdeas(db, id, (await db.ContentIdeas.Where(i => i.CampaignId == id).OrderBy(i => i.Ordinal).Take(1).Select(i => i.Id).ToArrayAsync())));
api.MapPost("/campaigns/{id:guid}/ideas/regenerate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateRoutes(db, jobs, id));

api.MapPost("/campaigns/{id:guid}/content/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateContent(db, jobs, id));
api.MapGet("/campaigns/{id:guid}/content", async (Guid id, RedAIDbContext db) => Results.Ok(await db.ContentItems.Where(x => x.CampaignId == id).OrderBy(x => x.Sequence).Select(x => new { contentId = x.Id, x.Sequence, revision = db.ContentRevisions.Where(r => r.ContentItemId == x.Id).OrderByDescending(r => r.Version).Select(r => new { revisionId = r.Id, r.Headline, r.SupportingText, r.Caption, r.Cta, r.VisualDirection, r.Version, r.IsApproved }).First() }).Select(x => new { x.contentId, x.Sequence, x.revision.revisionId, x.revision.Headline, x.revision.SupportingText, x.revision.Caption, x.revision.Cta, x.revision.VisualDirection, x.revision.Version, x.revision.IsApproved }).ToListAsync()));
api.MapGet("/content/{id:guid}", async (Guid id, RedAIDbContext db) => await db.ContentItems.FindAsync(id) is { } item ? Results.Ok(new { item, revisions = await db.ContentRevisions.Where(r => r.ContentItemId == id).OrderBy(r => r.Version).ToListAsync() }) : Results.NotFound());
api.MapPost("/content/{id:guid}/revise", async (Guid id, ReviseRequest r, RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, CancellationToken ct) => await Revise(db, ai, schemas, id, r, ct));
api.MapPut("/content/{id:guid}/revision/{revisionId:guid}", async (Guid id, Guid revisionId, EditRevision r, RedAIDbContext db) => await EditRevision(db, id, revisionId, r));
api.MapPost("/content/{id:guid}/revision/{revisionId:guid}/approve", async (Guid id, Guid revisionId, RedAIDbContext db) => await ApproveRevision(db, id, revisionId));

api.MapPost("/campaigns/{id:guid}/creatives/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => !await db.Campaigns.AnyAsync(c => c.Id == id) ? Results.NotFound() : await Start(db, jobs, "creative-generation", "campaign", id, await db.ContentItems.CountAsync(item => item.CampaignId == id), async (sp, ct) => await MaterializeCreatives(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IServiceScopeFactory>(), id, ct)));
api.MapGet("/content/{id:guid}/creatives", async (Guid id, RedAIDbContext db) => Results.Ok(await db.CreativeVersions.Where(x => x.ContentItemId == id).OrderBy(x => x.Version).ToListAsync()));
api.MapPost("/content/{id:guid}/creative/revise", async (Guid id, ReviseRequest request, RedAIDbContext db, JobQueue jobs) => await db.ContentItems.AnyAsync(x => x.Id == id) ? await Start(db, jobs, "creative-revision", "content", id, 1, async (sp, ct) => await ReviseCreative(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IAIClient>(), sp.GetRequiredService<IContractSchemaCatalog>(), sp.GetRequiredService<IAssetStorage>(), sp.GetRequiredService<IDeterministicCreativeRenderer>(), id, request.Instruction, ct)) : Results.NotFound());
api.MapPost("/content/{id:guid}/creative/{versionId:guid}/select", async (Guid id, Guid versionId, RedAIDbContext db) => await SelectCreative(db, id, versionId));
api.MapGet("/projects/{id:guid}/result", async (Guid id, RedAIDbContext db) => await db.Projects.Include(p => p.Campaign).FirstOrDefaultAsync(p => p.Id == id) is { } p ? Results.Ok(new { p.Id, p.Name, campaign = p.Campaign, status = p.Status }) : Results.NotFound());
api.MapGet("/projects/{id:guid}/export", async (Guid id, RedAIDbContext db, IAssetStorage storage, CancellationToken ct) => await db.Projects.Include(p => p.Campaign).FirstOrDefaultAsync(p => p.Id == id, ct) is { Campaign: { } campaign } project ? Results.File(await CreateExportZip(project, campaign, await db.ContentItems.Where(x => x.CampaignId == campaign.Id).ToListAsync(ct), await db.CreativeVersions.Join(db.ContentItems, v => v.ContentItemId, i => i.Id, (v, i) => new { v, i }).Where(x => x.i.CampaignId == campaign.Id && x.v.IsSelected).Select(x => x.v).ToListAsync(ct), storage, ct), "application/zip", $"red-ai-{id:N}.zip") : Results.NotFound());
api.MapGet("/jobs/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) is { } job ? Results.Ok(job) : Results.NotFound());
api.MapGet("/campaigns/{id:guid}/content/job", async (Guid id, RedAIDbContext db) => await db.Jobs.AsNoTracking().Where(job => job.Type == "content-generation" && job.EntityType == "campaign" && job.EntityId == id && (job.Status == "queued" || job.Status == "running")).OrderByDescending(job => job.CreatedAt).FirstOrDefaultAsync() is { } job ? Results.Ok(job) : Results.NoContent());
api.MapPost("/demo/reset", async (RedAIDbContext db, IAssetStorage storage, InMemoryCampaignStore jobs, CancellationToken ct) => { var assetKeys = await db.BrandSources.Where(source => source.StorageKey != null).Select(source => source.StorageKey!).Concat(db.CreativeVersions.Where(version => version.ImageStorageKey != null).Select(version => version.ImageStorageKey!)).Concat(db.CreativeVersions.Where(version => version.ThumbnailStorageKey != null).Select(version => version.ThumbnailStorageKey!)).ToListAsync(ct); foreach (var key in assetKeys.Distinct()) await storage.DeleteAsync(key, ct); db.AIRuns.RemoveRange(db.AIRuns); db.Jobs.RemoveRange(db.Jobs); db.RemoveRange(db.Projects); await db.SaveChangesAsync(ct); jobs.Projects.Clear(); jobs.Jobs.Clear(); return Results.NoContent(); });
api.MapPost("/demo/seed/cassel", async (RedAIDbContext db) => await Seed(db, "Cassel Seguros", "@casselseguros"));
api.MapPost("/demo/seed/redzone", async (RedAIDbContext db) => await Seed(db, "Redzone MKT", "@redzonemkt"));
app.Run();

static async Task<IResult> Start(RedAIDbContext db, JobQueue queue, string type, string entityType, Guid id, int total, Func<IServiceProvider, CancellationToken, Task> work)
{
    var job = new Job { Type = type, EntityType = entityType, EntityId = id, TotalSteps = total, Message = "Na fila" };
    db.Jobs.Add(job); await db.SaveChangesAsync(); await queue.EnqueueAsync(new QueuedJob(job.Id, work));
    return Results.Accepted($"/api/jobs/{job.Id}", job);
}
static async Task<IResult> RunSynchronously(RedAIDbContext db, IServiceProvider services, string type, string entityType, Guid id, int total, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken ct)
{
    var job = new Job { Type = type, EntityType = entityType, EntityId = id, TotalSteps = total, Status = "running", Message = "Analisando fontes da marca" };
    db.Jobs.Add(job); await db.SaveChangesAsync(ct);
    try { await work(services, ct); job.Status = "completed"; job.Progress = 100; job.CompletedSteps = total; job.Message = "Concluído"; job.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Results.Ok(job); }
    catch (Exception exception) { services.GetRequiredService<ILoggerFactory>().CreateLogger("RedAI.BrandAnalysis").LogError(exception, "Synchronous job {JobId} failed", job.Id); job.Status = "failed"; job.Error = exception.Message; job.Message = "Falhou"; job.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Results.Problem("Não foi possível mapear a identidade da marca.", statusCode: 502, extensions: new Dictionary<string, object?> { ["jobId"] = job.Id }); }
}
static async Task<IResult> Delete(RedAIDbContext db, Project p) { db.Remove(p); await db.SaveChangesAsync(); return Results.NoContent(); }
static async Task<IResult> DeleteSource(RedAIDbContext db, IAssetStorage storage, BrandSource s, CancellationToken ct) { if (s.StorageKey is not null) await storage.DeleteAsync(s.StorageKey, ct); db.Remove(s); await db.SaveChangesAsync(ct); return Results.NoContent(); }
static async Task<IResult> SaveBrand(RedAIDbContext db, Guid id, string json) { if (!await db.Projects.AnyAsync(x => x.Id == id)) return Results.NotFound(); var p = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id); if (p is null) db.BrandProfiles.Add(new BrandProfile { ProjectId = id, ProfileJson = json, Confidence = .8m }); else { p.ProfileJson = json; p.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); return Results.Ok(JsonDocument.Parse(json).RootElement); }
static async Task MaterializeBrand(RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, IAssetStorage storage, IHttpClientFactory httpClients, Guid id, CancellationToken ct) { var p = await db.Projects.FindAsync([id], ct) ?? throw new KeyNotFoundException(); string json; decimal confidence; if (ai.Mode == "mock") { json = JsonSerializer.Serialize(new { brandName = p.Name, industry = "Serviços", summary = "Perfil demonstrativo para validação do fluxo.", confidence = .8, visualIdentity = new { colors = new[] { new { hex = "#090909", role = "base", confidence = .9 }, new { hex = "#FF3D1F", role = "accent", confidence = .9 }, new { hex = "#F6F6F3", role = "text", confidence = .9 } }, visualStyle = new[] { "editorial" }, imageStyle = new[] { "humano" }, layoutCharacteristics = new[] { "alto contraste" } }, voice = new { traits = new[] { "premium", "direto", "humano" }, formality = .7, energy = .6, avoid = new[] { "jargão" } }, audiences = new[] { new { name = "Decisores", description = "Público principal", confidence = .8 } }, products = new[] { new { name = "Serviços", confidence = .8 } }, contentAnalysis = new { currentPillars = new[] { "autoridade" }, strengths = new[] { "clareza" }, opportunities = new[] { "educação" }, recommendations = new[] { "prova social" } }, restrictions = new[] { "Não inventar promessas" } }); confidence = .8m; } else { var sources = await db.BrandSources.Where(s => s.ProjectId == id).OrderBy(s => s.CreatedAt).ToListAsync(ct); var images = new List<StructuredImageInput>(); foreach (var source in sources.Where(s => s.StorageKey is not null && IsVisionImage(s.MimeType))) { await using var stream = await storage.OpenReadAsync(source.StorageKey!, ct); using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes, ct); images.Add(new StructuredImageInput(source.OriginalFilename ?? "brand-source", source.MimeType!, bytes.ToArray())); } var homepage = await FetchHomepageText(httpClients.CreateClient("brand-website"), p.WebsiteUrl, ct); using var output = await ai.CompleteStructuredAsync(new StructuredTextRequest("brand-analysis", "Você é o BrandAnalyzer do RED AI. Analise apenas as evidências fornecidas, incluindo as imagens anexadas. Não invente fatos. Responda estritamente no schema.", new { project = new { p.Name, p.InstagramHandle, p.WebsiteUrl, p.ManualContext }, homepage, sources = sources.Select(s => new { s.Type, s.OriginalFilename, s.MimeType, s.ExtractedText }) }, "brand-profile", schemas.Load("brand-profile"), Images: images), ct); json = output.RootElement.GetRawText(); confidence = output.RootElement.GetProperty("confidence").GetDecimal(); } var profile = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id, ct); if (profile is null) db.BrandProfiles.Add(new BrandProfile { ProjectId = id, ProfileJson = json, Confidence = confidence }); else { profile.ProfileJson = json; profile.Confidence = confidence; profile.Status = "generated"; profile.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(ct); }
static bool IsVisionImage(string? contentType) => contentType is "image/png" or "image/jpeg" or "image/webp" or "image/gif";
static async Task<string?> FetchHomepageText(HttpClient client, string? websiteUrl, CancellationToken ct) { if (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null; try { using var response = await client.GetAsync(uri, ct); if (!response.IsSuccessStatusCode) return null; var html = await response.Content.ReadAsStringAsync(ct); var text = WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase), "<[^>]+>", " ")).Trim(); return text[..Math.Min(12000, text.Length)]; } catch { return null; } }
static async Task<IResult> ApproveBrand(RedAIDbContext db, Guid id) { var p = await db.Projects.FindAsync(id); var b = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id); if (p is null || b is null) return Results.NotFound(); b.Status = "approved"; p.CurrentStep = "campaign"; p.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Ok(b); }
static async Task<IResult> SaveStrategy(RedAIDbContext db, Guid id, string json) { if (!await db.Campaigns.AnyAsync(x => x.Id == id)) return Results.NotFound(); var s = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id); if (s is null) db.CampaignStrategies.Add(new CampaignStrategy { CampaignId = id, StrategyJson = json }); else { s.StrategyJson = json; s.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); return Results.Ok(JsonDocument.Parse(json).RootElement); }
static async Task MaterializeStrategy(RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, Guid id, CancellationToken ct) { var c = await db.Campaigns.FindAsync([id], ct) ?? throw new KeyNotFoundException(); var brand = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == c.ProjectId, ct) ?? throw new InvalidOperationException("Brand DNA must be generated before strategy."); if (brand.Status != "approved") throw new InvalidOperationException("Brand DNA must be approved before strategy."); var json = ai.Mode == "mock" ? JsonSerializer.Serialize(new { campaignName = c.Name, strategicObjective = c.Objective, rationale = "Estratégia demonstrativa baseada no Brand DNA aprovado.", contentMix = new[] { new { pillar = "Autoridade", percentage = 34 }, new { pillar = "Educação", percentage = 33 }, new { pillar = "Conversão", percentage = 33 } }, pillars = new[] { new { id = "authority", name = "Autoridade", description = "Constrói confiança" }, new { id = "education", name = "Educação", description = "Explica escolhas" }, new { id = "conversion", name = "Conversão", description = "Convida ao contato" } }, targetAudiences = new[] { "Decisores" }, messages = new[] { "Escolhas bem informadas" }, creativeDirection = new { style = new[] { "editorial" }, recommendations = new[] { "alto contraste" }, avoid = new[] { "promessas absolutas" } }, avoid = new[] { "jargão" } }) : (await ai.CompleteStructuredAsync(new StructuredTextRequest("strategy-generation", "Você é o CampaignStrategist do RED AI. Use exclusivamente o Brand DNA aprovado e o briefing. Responda estritamente no schema.", new { campaign = new { c.Name, c.Objective, c.TargetCount, c.Context }, brandDna = JsonDocument.Parse(brand.ProfileJson).RootElement }, "campaign-strategy", schemas.Load("campaign-strategy")), ct)).RootElement.GetRawText(); var strategy = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id, ct); if (strategy is null) db.CampaignStrategies.Add(new CampaignStrategy { CampaignId = id, StrategyJson = json }); else { strategy.StrategyJson = json; strategy.Status = "generated"; strategy.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(ct); }
static async Task<IResult> ApproveStrategy(RedAIDbContext db, Guid id) { var c = await db.Campaigns.FindAsync(id); var s = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id); if (c is null || s is null) return Results.NotFound(); c.StrategyApproved = true; s.Status = "approved"; await db.SaveChangesAsync(); return Results.Ok(c); }
static async Task<IResult> GenerateRoutes(RedAIDbContext db, JobQueue jobs, Guid id)
{
    if (!await db.Campaigns.AnyAsync(campaign => campaign.Id == id)) return Results.NotFound();
    if (!await db.CampaignStrategies.AnyAsync(strategy => strategy.CampaignId == id && strategy.Status == "approved")) return Results.BadRequest(new { error = "Strategy must be approved before routes." });
    return await Start(db, jobs, "campaign-routes-generation", "campaign", id, 5, async (sp, ct) =>
    {
        var context = sp.GetRequiredService<RedAIDbContext>(); var ai = sp.GetRequiredService<IAIClient>();
        var campaign = await context.Campaigns.FindAsync([id], ct) ?? throw new KeyNotFoundException();
        var strategy = await context.CampaignStrategies.FirstAsync(item => item.CampaignId == id && item.Status == "approved", ct);
        var routes = ai.Mode == "mock"
            ? Enumerable.Range(1, 5).Select(index => new {
                title = $"Rota de campanha {index:00}",
                promise = "Transformar planejamento em proteção concreta.",
                targetAudience = "Famílias e decisores que buscam segurança para o futuro.",
                creativeAngle = index % 2 == 0 ? "Educação prática" : "Proteção que cabe na vida real",
                visualDirection = "Editorial humano, contraste forte e espaço para uma mensagem objetiva.",
                pillar = index % 2 == 0 ? "Educação" : "Autoridade",
                contentType = "Post estático",
                description = "Uma rota coerente de cinco posts estáticos para a mesma campanha."
            }).ToArray()
            : (await ai.CompleteStructuredAsync(new StructuredTextRequest(
                "campaign-routes-generation",
                "Você é o Campaign Route Director do RED AI. Gere exatamente 5 rotas de campanha distintas. Para cada rota, detalhe promessa, público, ângulo criativo e direção visual. A entrega final aceita somente Post estático: não proponha carrossel, vídeo, reel, landing page, site ou múltiplos slides. Responda estritamente no schema.",
                new { campaign = new { campaign.Name, campaign.Objective, campaign.Context }, brandDna = JsonDocument.Parse((await context.BrandProfiles.FirstAsync(profile => profile.ProjectId == campaign.ProjectId, ct)).ProfileJson).RootElement, strategy = JsonDocument.Parse(strategy.StrategyJson).RootElement },
                "ideas",
                sp.GetRequiredService<IContractSchemaCatalog>().Load("ideas")), ct)).RootElement.GetProperty("ideas").EnumerateArray().Select(item => new {
                    title = item.GetProperty("title").GetString() ?? string.Empty,
                    promise = item.GetProperty("promise").GetString() ?? string.Empty,
                    targetAudience = item.GetProperty("targetAudience").GetString() ?? string.Empty,
                    creativeAngle = item.GetProperty("creativeAngle").GetString() ?? string.Empty,
                    visualDirection = item.GetProperty("visualDirection").GetString() ?? string.Empty,
                    pillar = item.GetProperty("pillar").GetString() ?? string.Empty,
                    contentType = item.GetProperty("contentType").GetString() ?? string.Empty,
                    description = item.GetProperty("description").GetString() ?? string.Empty
                }).ToArray();
        if (routes.Length != 5 || routes.Select(route => route.title.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5 || routes.Any(route =>
            string.IsNullOrWhiteSpace(route.title) || string.IsNullOrWhiteSpace(route.promise) ||
            string.IsNullOrWhiteSpace(route.targetAudience) || string.IsNullOrWhiteSpace(route.creativeAngle) ||
            string.IsNullOrWhiteSpace(route.visualDirection) || string.IsNullOrWhiteSpace(route.pillar) ||
            string.IsNullOrWhiteSpace(route.description) || route.contentType != "Post estático"))
            throw new InvalidOperationException("Campaign Route Director must return 5 unique static-post routes with all route details.");
        context.ContentIdeas.RemoveRange(context.ContentIdeas.Where(route => route.CampaignId == id));
        context.ContentIdeas.AddRange(routes.Select((route, index) => new ContentIdea {
            CampaignId = id, Ordinal = index + 1, Title = route.title, Pillar = route.pillar, ContentType = route.contentType, Description = route.description,
            Promise = route.promise, TargetAudience = route.targetAudience, CreativeAngle = route.creativeAngle, VisualDirection = route.visualDirection
        }));
        await context.SaveChangesAsync(ct);
    });
}
static async Task<IResult> SelectIdeas(RedAIDbContext db, Guid id, Guid[] ids) { if (ids.Distinct().Count() > 1) return Results.BadRequest(new { error = "Selecione uma única rota de campanha." }); var ideas = await db.ContentIdeas.Where(i => i.CampaignId == id).ToListAsync(); if (ideas.Count == 0 || ids.Any(x => ideas.All(i => i.Id != x))) return Results.BadRequest(new { error = "A rota precisa pertencer à campanha." }); foreach (var i in ideas) i.Selected = ids.Contains(i.Id); await db.SaveChangesAsync(); return Results.Ok(ideas.Where(i => i.Selected)); }
static async Task<IResult> GenerateContent(RedAIDbContext db, JobQueue jobs, Guid id)
{
    var routes = await db.ContentIdeas.Where(route => route.CampaignId == id && route.Selected).OrderBy(route => route.Ordinal).ToListAsync();
    if (routes.Count != 1) return Results.BadRequest(new { error = "A campanha precisa de exatamente uma rota selecionada." });

    return await Start(db, jobs, "content-generation", "campaign", id, 5, async (sp, ct) =>
    {
        var context = sp.GetRequiredService<RedAIDbContext>();
        var route = await context.ContentIdeas.SingleAsync(item => item.CampaignId == id && item.Selected, ct);
        var progressJob = await context.Jobs.Where(job => job.Type == "content-generation" && job.EntityType == "campaign" && job.EntityId == id && job.Status == "running").OrderByDescending(job => job.CreatedAt).FirstAsync(ct);
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var roles = new[] { "abertura da promessa", "educação prática", "prova de relevância", "aplicação no cotidiano", "convite para agir" };

        async Task<GeneratedCopy> GenerateCopyAsync(int sequence)
        {
            using var copyScope = scopeFactory.CreateScope();
            var copyAi = copyScope.ServiceProvider.GetRequiredService<IAIClient>();
            if (copyAi.Mode == "mock")
                return new GeneratedCopy($"{route.Title}: post {sequence}", $"{route.Promise} {route.Description} Post {sequence} da série: {roles[sequence - 1]}.", null, "Fale com a equipe", route.VisualDirection ?? "Editorial humano e objetivo", ["#proteção"]);

            var copySchemas = copyScope.ServiceProvider.GetRequiredService<IContractSchemaCatalog>();
            using var output = await copyAi.CompleteStructuredAsync(new StructuredTextRequest(
                "content-generation",
                "Você é o ContentGenerator do RED AI. Crie uma única copy social em português brasileiro para um post estático da série de campanha. Os cinco posts devem ser complementares, sem repetir headline nem legenda. Não proponha carrossel, vídeo, reel, landing page, site ou múltiplos slides. Responda estritamente no schema.",
                new { route = new { route.Title, route.Promise, route.TargetAudience, route.CreativeAngle, route.VisualDirection, route.Pillar, route.ContentType, route.Description }, series = new { postNumber = sequence, totalPosts = 5, editorialRole = roles[sequence - 1] } },
                "content-revision", copySchemas.Load("content-revision")), ct);
            var copy = output.Deserialize<GeneratedCopy>() ?? throw new InvalidOperationException("ContentGenerator returned no copy.");
            if (string.IsNullOrWhiteSpace(copy.Headline) || string.IsNullOrWhiteSpace(copy.Caption) || string.IsNullOrWhiteSpace(copy.VisualDirection))
                throw new InvalidOperationException("ContentGenerator returned an incomplete copy.");
            return copy;
        }

        context.ContentItems.RemoveRange(context.ContentItems.Where(item => item.CampaignId == id));
        await context.SaveChangesAsync(ct);

        var pending = Enumerable.Range(1, 5).Select(sequence => (sequence, copy: GenerateCopyAsync(sequence))).ToList();
        var headlines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completed = 0;
        while (pending.Count > 0)
        {
            var completedTask = await Task.WhenAny(pending.Select(item => item.copy));
            var index = pending.FindIndex(item => item.copy == completedTask);
            var (sequence, copyTask) = pending[index];
            pending.RemoveAt(index);
            var copy = await copyTask;
            if (!headlines.Add(copy.Headline.Trim())) throw new InvalidOperationException("ContentGenerator returned duplicate headlines for the campaign series.");

            var item = new ContentItem { CampaignId = id, SourceIdeaId = route.Id, Sequence = sequence };
            context.ContentItems.Add(item);
            context.ContentRevisions.Add(new ContentRevision { ContentItemId = item.Id, Version = 1, Headline = copy.Headline, Caption = copy.Caption, SupportingText = copy.SupportingText, Cta = copy.Cta, VisualDirection = copy.VisualDirection, HashtagsJson = JsonSerializer.Serialize(copy.Hashtags) });
            completed++;
            progressJob.CompletedSteps = completed;
            progressJob.Progress = completed * 100 / 5;
            progressJob.Message = $"Gerando post {completed} de 5";
            await context.SaveChangesAsync(ct);
        }
    });
}
static async Task<IResult> Revise(RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, Guid id, ReviseRequest request, CancellationToken ct) { if (string.IsNullOrWhiteSpace(request.Instruction)) return Results.BadRequest(new { error = "A instrução de revisão é obrigatória." }); var item = await db.ContentItems.FindAsync([id], ct); if (item is null) return Results.NotFound(); var current = await db.ContentRevisions.Where(r => r.ContentItemId == id).OrderByDescending(r => r.Version).FirstAsync(ct); GeneratedCopy copy; if (ai.Mode == "mock") copy = new($"{current.Headline} — revisão educativa", $"{current.Caption}\n\nConteúdo revisado para explicar o tema de forma mais educativa e menos comercial.", current.SupportingText, current.Cta, current.VisualDirection ?? string.Empty, JsonSerializer.Deserialize<string[]>(current.HashtagsJson) ?? []); else { using var output = await ai.CompleteStructuredAsync(new StructuredTextRequest("content-revision", "Você é o ContentReviewer do RED AI. Reescreva genuinamente o conteúdo atual seguindo a instrução do usuário. Preserve fatos verificáveis, não invente dados e responda estritamente no schema.", new { instruction = request.Instruction, current = new { current.Headline, current.SupportingText, current.Caption, current.Cta, current.VisualDirection, hashtags = JsonSerializer.Deserialize<string[]>(current.HashtagsJson) ?? [] } }, "content-revision", schemas.Load("content-revision")), ct); copy = output.Deserialize<GeneratedCopy>() ?? throw new InvalidOperationException("ContentReviewer returned no copy."); } var next = new ContentRevision { ContentItemId = id, Version = current.Version + 1, Headline = copy.Headline, Caption = copy.Caption, SupportingText = copy.SupportingText, Cta = copy.Cta, HashtagsJson = JsonSerializer.Serialize(copy.Hashtags), VisualDirection = copy.VisualDirection, Instruction = request.Instruction }; db.ContentRevisions.Add(next); await db.SaveChangesAsync(ct); return Results.Ok(next); }
static async Task<IResult> EditRevision(RedAIDbContext db, Guid id, Guid revisionId, EditRevision edit) { var r = await db.ContentRevisions.FirstOrDefaultAsync(x => x.Id == revisionId && x.ContentItemId == id); if (r is null) return Results.NotFound(); r.Headline = edit.Headline ?? r.Headline; r.Caption = edit.Caption ?? r.Caption; r.Cta = edit.Cta ?? r.Cta; r.SupportingText = edit.SupportingText ?? r.SupportingText; await db.SaveChangesAsync(); return Results.Ok(r); }
static async Task<IResult> ApproveRevision(RedAIDbContext db, Guid id, Guid revisionId) { var item = await db.ContentItems.FindAsync(id); var r = await db.ContentRevisions.FirstOrDefaultAsync(x => x.Id == revisionId && x.ContentItemId == id); if (item is null || r is null) return Results.NotFound(); r.IsApproved = true; item.ApprovedRevisionId = r.Id; item.Status = "approved"; await db.SaveChangesAsync(); return Results.Ok(r); }
static async Task<IResult> SelectCreative(RedAIDbContext db, Guid id, Guid versionId) { var v = await db.CreativeVersions.FirstOrDefaultAsync(x => x.Id == versionId && x.ContentItemId == id); if (v is null) return Results.NotFound(); foreach (var x in await db.CreativeVersions.Where(x => x.ContentItemId == id).ToListAsync()) x.IsSelected = x.Id == versionId; await db.SaveChangesAsync(); return Results.Ok(v); }
static async Task MaterializeCreatives(RedAIDbContext db, IServiceScopeFactory scopeFactory, Guid campaignId, CancellationToken ct)
{
    var itemIds = await db.ContentItems.Where(item => item.CampaignId == campaignId).OrderBy(item => item.Sequence).Select(item => item.Id).ToListAsync(ct);
    var progressJob = await db.Jobs.Where(job => job.Type == "creative-generation" && job.EntityType == "campaign" && job.EntityId == campaignId && job.Status == "running").OrderByDescending(job => job.CreatedAt).FirstAsync(ct);

    async Task RenderCreativeAsync(Guid contentItemId)
    {
        using var renderScope = scopeFactory.CreateScope();
        var context = renderScope.ServiceProvider.GetRequiredService<RedAIDbContext>();
        var item = await context.ContentItems.FindAsync([contentItemId], ct) ?? throw new KeyNotFoundException();
        if (await context.CreativeVersions.AnyAsync(version => version.ContentItemId == contentItemId, ct)) return;

        var campaign = await context.Campaigns.FindAsync([campaignId], ct) ?? throw new KeyNotFoundException();
        var revision = await context.ContentRevisions.Where(revision => revision.ContentItemId == contentItemId).OrderByDescending(revision => revision.Version).FirstAsync(ct);
        var palette = await ResolveBrandPalette(context, campaign.ProjectId, ct);
        var ai = renderScope.ServiceProvider.GetRequiredService<IAIClient>();
        var schemas = renderScope.ServiceProvider.GetRequiredService<IContractSchemaCatalog>();
        var storage = renderScope.ServiceProvider.GetRequiredService<IAssetStorage>();
        var renderer = renderScope.ServiceProvider.GetRequiredService<IDeterministicCreativeRenderer>();
        var brief = await CreateCreativeBrief(ai, schemas, campaign, revision, item.Sequence, palette, ct);
        var asset = brief.ImageRequired ? await GenerateVisualAsset(ai, storage, campaign.ProjectId, item.Id, 1, brief.ImageDirection, ct) : null;
        var layout = new CreativeLayout(brief.Template, palette, new CreativeHeadline(revision.Headline, brief.Template is "minimal-center" or "statement" ? "center" : "left", brief.Template is "editorial-bold" or "statement" ? "2xl" : "xl", []), new CreativeLogo(brief.LogoPlacement), revision.SupportingText, revision.Cta, asset);
        var key = $"projects/{campaign.ProjectId}/content/{item.Id}/creatives/v1/final.png";
        await renderer.RenderPngAsync(new DeterministicCreativeRenderRequest(layout, key), ct);
        context.CreativeVersions.Add(new CreativeVersion { ContentItemId = item.Id, Version = 1, SourceContentRevisionId = revision.Id, LayoutJson = JsonSerializer.Serialize(layout), ImageStorageKey = key });
        await context.SaveChangesAsync(ct);
    }

    var pending = itemIds.Select(itemId => RenderCreativeAsync(itemId)).ToList();
    var completed = 0;
    while (pending.Count > 0)
    {
        var completedTask = await Task.WhenAny(pending);
        pending.Remove(completedTask);
        await completedTask;
        completed++;
        progressJob.CompletedSteps = completed;
        progressJob.Progress = completed * 100 / itemIds.Count;
        progressJob.Message = $"Produzindo arte {completed} de {itemIds.Count}";
        await db.SaveChangesAsync(ct);
    }
}
static async Task<CreativeBrief> CreateCreativeBrief(IAIClient ai, IContractSchemaCatalog schemas, Campaign campaign, ContentRevision revision, int sequence, CreativePalette palette, CancellationToken ct) { if (ai.Mode == "mock") return new CreativeBrief("Comunicação de marca", (sequence % 6) switch { 0 => "promotional", 1 => "editorial-bold", 2 => "minimal-center", 3 => "split-image", 4 => "statement", _ => "educational" }, false, "Sem imagem gerada", "Tipografia editorial", ["claro"], [palette.Background, palette.Primary, palette.Accent], ["headline", "apoio", "cta"], "rodapé", ["texto em imagem"]); using var output = await ai.CompleteStructuredAsync(new StructuredTextRequest("creative-brief", "Você é o Creative Director do RED AI. Crie um brief visual para post social. Decida se uma imagem gerada agrega valor. A imagem, se usada, será apenas fundo: nunca peça tipografia, logo ou CTA nela. Responda estritamente no schema.", new { campaign = new { campaign.Name, campaign.Objective, campaign.Context }, content = new { revision.Headline, revision.SupportingText, revision.Caption, revision.Cta, revision.VisualDirection }, palette }, "creative-brief", schemas.Load("creative-brief")), ct); return output.Deserialize<CreativeBrief>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Creative Director returned no brief."); }
static async Task<string?> GenerateVisualAsset(IAIClient ai, IAssetStorage storage, Guid projectId, Guid contentItemId, int version, string direction, CancellationToken ct) { if (ai.Mode != "openai") return null; var prompt = $"Create a premium editorial background photograph for a Brazilian brand social post. Direction: {direction}. No text, no lettering, no typography, no words, no logo, no brand mark, no watermark, no signature, no UI, no borders. Leave clear negative space for a separate renderer to add copy."; var key = $"projects/{projectId}/content/{contentItemId}/visuals/v{version}/background.png"; return (await new VisualAssetGenerator(ai, storage).GenerateAndStoreAsync(new ImageGenerationRequest("creative-visual", prompt), key, ct)).Asset.StorageKey; }
static async Task ReviseCreative(RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, IAssetStorage storage, IDeterministicCreativeRenderer renderer, Guid contentItemId, string instruction, CancellationToken ct) { if (string.IsNullOrWhiteSpace(instruction)) throw new ArgumentException("A instrução de revisão é obrigatória."); var current = await db.CreativeVersions.Where(item => item.ContentItemId == contentItemId).OrderByDescending(item => item.Version).FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Creative version not found."); var item = await db.ContentItems.FindAsync([contentItemId], ct) ?? throw new KeyNotFoundException(); var revision = await db.ContentRevisions.FindAsync([current.SourceContentRevisionId], ct) ?? throw new KeyNotFoundException(); var layout = JsonSerializer.Deserialize<CreativeLayout>(current.LayoutJson) ?? throw new InvalidOperationException("Creative layout is invalid."); var plan = await CreateRevisionPlan(ai, schemas, instruction, layout, ct); var changes = (plan.Actions ?? []).Select(action => action.Type).ToHashSet(); if (changes.Contains("CHANGE_TYPOGRAPHY")) layout = layout with { Headline = layout.Headline with { Size = "lg" } }; if (changes.Contains("CHANGE_LAYOUT")) layout = layout with { Template = "minimal-center", Headline = layout.Headline with { Alignment = "center" } }; if (changes.Contains("CHANGE_COLORS")) layout = layout with { Palette = new CreativePalette("#F5F1E8", layout.Palette.Primary, layout.Palette.Accent) }; if (changes.Contains("REGENERATE_IMAGE") || changes.Contains("CHANGE_ASSET")) layout = layout with { BackgroundAssetKey = await GenerateVisualAsset(ai, storage, (await db.Campaigns.FindAsync([item.CampaignId], ct))!.ProjectId, contentItemId, current.Version + 1, plan.Summary, ct) }; var key = $"projects/{(await db.Campaigns.FindAsync([item.CampaignId], ct))!.ProjectId}/content/{contentItemId}/creatives/v{current.Version + 1}/final.png"; await renderer.RenderPngAsync(new DeterministicCreativeRenderRequest(layout, key), ct); db.CreativeVersions.Add(new CreativeVersion { ContentItemId = contentItemId, Version = current.Version + 1, SourceContentRevisionId = revision.Id, LayoutJson = JsonSerializer.Serialize(layout), ImageStorageKey = key, RevisionInstruction = instruction }); await db.SaveChangesAsync(ct); }
static async Task<CreativeRevisionPlan> CreateRevisionPlan(IAIClient ai, IContractSchemaCatalog schemas, string instruction, CreativeLayout layout, CancellationToken ct) { if (ai.Mode == "mock") { var action = instruction.Contains("foto", StringComparison.OrdinalIgnoreCase) || instruction.Contains("família", StringComparison.OrdinalIgnoreCase) ? "REGENERATE_IMAGE" : instruction.Contains("claro", StringComparison.OrdinalIgnoreCase) ? "CHANGE_COLORS" : "CHANGE_TYPOGRAPHY"; return new CreativeRevisionPlan(instruction, [new CreativeRevisionAction(action, instruction)]); } using var output = await ai.CompleteStructuredAsync(new StructuredTextRequest("creative-revision-plan", "Você é o Creative Director do RED AI. Produza um plano de revisão. Use REGENERATE_IMAGE ou CHANGE_ASSET somente se o usuário pedir para trocar a cena, pessoa, objeto ou foto. Para cor, espaço, tamanho ou template, não regenere imagem. Responda estritamente no schema.", new { instruction, layout }, "creative-revision-plan", schemas.Load("creative-revision-plan")), ct); return output.Deserialize<CreativeRevisionPlan>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Creative revision plan missing."); }
static async Task<CreativePalette> ResolveBrandPalette(RedAIDbContext db, Guid projectId, CancellationToken ct) { var profile = await db.BrandProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.ProjectId == projectId, ct); if (profile is null) return new CreativePalette("#0B0D10", "#F6F6F3", "#FF3D1F"); try { using var json = JsonDocument.Parse(profile.ProfileJson); var colors = json.RootElement.GetProperty("visualIdentity").GetProperty("colors").EnumerateArray().Select(color => color.GetProperty("hex").GetString()).Where(hex => !string.IsNullOrWhiteSpace(hex) && Regex.IsMatch(hex!, "^#[0-9A-Fa-f]{6}$")).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); return colors.Length switch { >= 3 => new CreativePalette(colors[0], colors[1], colors[2]), 2 => new CreativePalette(colors[0], colors[1], colors[0]), 1 => new CreativePalette(colors[0], "#F6F6F3", "#FF3D1F"), _ => new CreativePalette("#0B0D10", "#F6F6F3", "#FF3D1F") }; } catch (JsonException) { return new CreativePalette("#0B0D10", "#F6F6F3", "#FF3D1F"); } }
static async Task<IResult> Seed(RedAIDbContext db, string name, string handle) { var p = new Project { Name = name, InstagramHandle = handle, CurrentStep = "sources" }; db.Projects.Add(p); await db.SaveChangesAsync(); return Results.Ok(p); }
static async Task ApplyMigrationsWithRetryAsync(IServiceProvider services, ILogger logger, CancellationToken stoppingToken)
{
    const int attempts = 12;
    for (var attempt = 1; attempt <= attempts; attempt++)
    {
        try { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<RedAIDbContext>().Database.MigrateAsync(stoppingToken); return; }
        catch (Exception exception) when (attempt < attempts) { logger.LogWarning(exception, "Database is not ready (attempt {Attempt}/{Attempts}); retrying.", attempt, attempts); await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
    }
    throw new InvalidOperationException("The database did not become ready within the startup retry window.");
}
static string ResolveConnectionString(IConfiguration configuration)
{
    var configured = configuration.GetConnectionString("Default") ?? configuration["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(configured)) return "Host=localhost;Port=5432;Database=redai;Username=redai;Password=redai_dev";
    if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri) || (uri.Scheme != "postgres" && uri.Scheme != "postgresql")) return configured;
    var credentials = uri.UserInfo.Split(':', 2);
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = uri.AbsolutePath.Trim('/'),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
        SslMode = SslMode.Require
    };
    return builder.ConnectionString;
}
static async Task<byte[]> CreateExportZip(Project project, Campaign campaign, IReadOnlyList<ContentItem> items, IReadOnlyList<CreativeVersion> creatives, IAssetStorage storage, CancellationToken ct)
{
    using var stream = new MemoryStream();
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
    {
        var manifest = archive.CreateEntry("campaign.json");
        await using (var writer = new StreamWriter(manifest.Open(), Encoding.UTF8))
            await writer.WriteAsync(JsonSerializer.Serialize(new { project = project.Name, campaign = campaign.Name, contentCount = items.Count, creativeCount = creatives.Count }, new JsonSerializerOptions { WriteIndented = true }));

        foreach (var creative in creatives)
        {
            var metadata = archive.CreateEntry($"creatives/content-{creative.ContentItemId}/v{creative.Version}.json");
            await using (var writer = new StreamWriter(metadata.Open(), Encoding.UTF8))
                await writer.WriteAsync(creative.LayoutJson);

            if (string.IsNullOrWhiteSpace(creative.ImageStorageKey))
                throw new InvalidOperationException($"The selected creative {creative.Id} does not have a rendered PNG.");

            var png = archive.CreateEntry($"creatives/content-{creative.ContentItemId}/v{creative.Version}.png", CompressionLevel.Optimal);
            await using var source = await storage.OpenReadAsync(creative.ImageStorageKey, ct);
            await using var destination = png.Open();
            await source.CopyToAsync(destination, ct);
        }
    }
    return stream.ToArray();
}
public record CreateProject(string Name, string? InstagramHandle, string? WebsiteUrl, string? ManualContext);
public record CreateCampaign(string Name, string? Objective, int TargetCount, string? Context);
public record SelectIdeas(Guid[] IdeaIds);
public record ReviseRequest(string Instruction);
public record EditRevision(string? Headline, string? SupportingText, string? Caption, string? Cta);
public record GeneratedCopy(
    [property: System.Text.Json.Serialization.JsonPropertyName("headline")] string Headline,
    [property: System.Text.Json.Serialization.JsonPropertyName("caption")] string Caption,
    [property: System.Text.Json.Serialization.JsonPropertyName("supportingText")] string? SupportingText,
    [property: System.Text.Json.Serialization.JsonPropertyName("cta")] string? Cta,
    [property: System.Text.Json.Serialization.JsonPropertyName("visualDirection")] string VisualDirection,
    [property: System.Text.Json.Serialization.JsonPropertyName("hashtags")] string[] Hashtags);
