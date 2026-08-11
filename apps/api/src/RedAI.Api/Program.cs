using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RedAI.Application;
using RedAI.Domain;
using RedAI.Infrastructure;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Npgsql;

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
builder.Services.AddScoped<IDeterministicCreativeRenderer, PngCreativeRenderer>();
builder.Services.AddHttpClient<OpenAIResponsesClient>();
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
api.MapGet("/health", (IConfiguration c) => Results.Ok(new { status = "ok", aiMode = c["AI:Mode"] ?? "mock" }));
api.MapGet("/projects", async (RedAIDbContext db) => await db.Projects.AsNoTracking().OrderByDescending(p => p.UpdatedAt).ToListAsync());
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

api.MapPost("/projects/{id:guid}/brand/analyze", async (Guid id, RedAIDbContext db, IServiceProvider services, CancellationToken ct) => !await db.Projects.AnyAsync(p => p.Id == id, ct) ? Results.NotFound() : await RunSynchronously(db, services, "brand-analysis", "project", id, 1, async (sp, token) => await MaterializeBrand(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IAIClient>(), sp.GetRequiredService<IContractSchemaCatalog>(), id, token), ct));
api.MapGet("/projects/{id:guid}/brand", async (Guid id, RedAIDbContext db) => await db.BrandProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id) is { } b ? Results.Content(b.ProfileJson, "application/json") : Results.NotFound());
api.MapPut("/projects/{id:guid}/brand", async (Guid id, JsonElement profile, RedAIDbContext db) => await SaveBrand(db, id, profile.GetRawText()));
api.MapPost("/projects/{id:guid}/brand/approve", async (Guid id, RedAIDbContext db) => await ApproveBrand(db, id));

api.MapPost("/projects/{id:guid}/campaign", async (Guid id, CreateCampaign r, RedAIDbContext db) => {
    var p = await db.Projects.Include(x => x.Campaign).FirstOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound(); if (p.Campaign is not null) return Results.Conflict(new { error = "Project already has a campaign" });
    var c = new Campaign { ProjectId = id, Name = r.Name, Objective = r.Objective ?? "AI decide", TargetCount = r.TargetCount == 0 ? 12 : r.TargetCount, Context = r.Context }; db.Campaigns.Add(c); p.CurrentStep = "strategy"; p.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Created($"/api/campaigns/{c.Id}", c);
});
api.MapGet("/campaigns/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(c) : Results.NotFound());
api.MapPost("/campaigns/{id:guid}/strategy/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await db.Campaigns.AnyAsync(c => c.Id == id) ? await Start(db, jobs, "strategy-generation", "campaign", id, 1, async (sp, ct) => await MaterializeStrategy(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IAIClient>(), sp.GetRequiredService<IContractSchemaCatalog>(), id, ct)) : Results.NotFound());
api.MapPut("/campaigns/{id:guid}/strategy", async (Guid id, JsonElement strategy, RedAIDbContext db) => await SaveStrategy(db, id, strategy.GetRawText()));
api.MapPost("/campaigns/{id:guid}/strategy/approve", async (Guid id, RedAIDbContext db) => await ApproveStrategy(db, id));

api.MapPost("/campaigns/{id:guid}/ideas/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateIdeas(db, jobs, id));
api.MapGet("/campaigns/{id:guid}/ideas", async (Guid id, RedAIDbContext db) => !await db.Campaigns.AnyAsync(c => c.Id == id) ? Results.NotFound() : Results.Ok(await db.ContentIdeas.Where(i => i.CampaignId == id).OrderBy(i => i.Ordinal).ToListAsync()));
api.MapPost("/campaigns/{id:guid}/ideas/select", async (Guid id, SelectIdeas r, RedAIDbContext db) => await SelectIdeas(db, id, r.IdeaIds));
api.MapPost("/campaigns/{id:guid}/ideas/auto-select", async (Guid id, RedAIDbContext db) => await SelectIdeas(db, id, (await db.ContentIdeas.Where(i => i.CampaignId == id).OrderBy(i => i.Ordinal).Take(12).Select(i => i.Id).ToArrayAsync())));
api.MapPost("/campaigns/{id:guid}/ideas/regenerate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateIdeas(db, jobs, id));

api.MapPost("/campaigns/{id:guid}/content/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateContent(db, jobs, id));
api.MapGet("/campaigns/{id:guid}/content", async (Guid id, RedAIDbContext db) => Results.Ok(await db.ContentItems.Where(x => x.CampaignId == id).OrderBy(x => x.Sequence).Select(x => new { x.Id, x.Sequence, revision = db.ContentRevisions.Where(r => r.ContentItemId == x.Id).OrderByDescending(r => r.Version).Select(r => new { r.Headline, r.Caption, r.Cta, r.VisualDirection, r.Version }).First() }).Select(x => new { x.Id, x.Sequence, x.revision.Headline, x.revision.Caption, x.revision.Cta, x.revision.VisualDirection, x.revision.Version }).ToListAsync()));
api.MapGet("/content/{id:guid}", async (Guid id, RedAIDbContext db) => await db.ContentItems.FindAsync(id) is { } item ? Results.Ok(new { item, revisions = await db.ContentRevisions.Where(r => r.ContentItemId == id).OrderBy(r => r.Version).ToListAsync() }) : Results.NotFound());
api.MapPost("/content/{id:guid}/revise", async (Guid id, ReviseRequest r, RedAIDbContext db) => await Revise(db, id, r));
api.MapPut("/content/{id:guid}/revision/{revisionId:guid}", async (Guid id, Guid revisionId, EditRevision r, RedAIDbContext db) => await EditRevision(db, id, revisionId, r));
api.MapPost("/content/{id:guid}/revision/{revisionId:guid}/approve", async (Guid id, Guid revisionId, RedAIDbContext db) => await ApproveRevision(db, id, revisionId));

api.MapPost("/campaigns/{id:guid}/creatives/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await db.Campaigns.AnyAsync(c => c.Id == id) ? await Start(db, jobs, "creative-generation", "campaign", id, 12, async (sp, ct) => await MaterializeCreatives(sp.GetRequiredService<RedAIDbContext>(), sp.GetRequiredService<IDeterministicCreativeRenderer>(), id, ct)) : Results.NotFound());
api.MapGet("/content/{id:guid}/creatives", async (Guid id, RedAIDbContext db) => Results.Ok(await db.CreativeVersions.Where(x => x.ContentItemId == id).OrderBy(x => x.Version).ToListAsync()));
api.MapPost("/content/{id:guid}/creative/revise", async (Guid id, ReviseRequest _, RedAIDbContext db, JobQueue jobs) => await db.ContentItems.AnyAsync(x => x.Id == id) ? await Start(db, jobs, "creative-revision", "content", id, 1, (_, _) => Task.CompletedTask) : Results.NotFound());
api.MapPost("/content/{id:guid}/creative/{versionId:guid}/select", async (Guid id, Guid versionId, RedAIDbContext db) => await SelectCreative(db, id, versionId));
api.MapGet("/projects/{id:guid}/result", async (Guid id, RedAIDbContext db) => await db.Projects.Include(p => p.Campaign).FirstOrDefaultAsync(p => p.Id == id) is { } p ? Results.Ok(new { p.Id, p.Name, campaign = p.Campaign, status = p.Status }) : Results.NotFound());
api.MapGet("/projects/{id:guid}/export", async (Guid id, RedAIDbContext db) => await db.Projects.Include(p => p.Campaign).FirstOrDefaultAsync(p => p.Id == id) is { Campaign: { } campaign } project ? Results.File(CreateExportZip(project, campaign, await db.ContentItems.Where(x => x.CampaignId == campaign.Id).ToListAsync(), await db.CreativeVersions.Join(db.ContentItems, v => v.ContentItemId, i => i.Id, (v, i) => new { v, i }).Where(x => x.i.CampaignId == campaign.Id).Select(x => x.v).ToListAsync()), "application/zip", $"red-ai-{id:N}.zip") : Results.NotFound());
api.MapGet("/jobs/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) is { } job ? Results.Ok(job) : Results.NotFound());
api.MapPost("/demo/reset", async (RedAIDbContext db, InMemoryCampaignStore jobs) => { db.RemoveRange(db.Projects); await db.SaveChangesAsync(); jobs.Projects.Clear(); jobs.Jobs.Clear(); return Results.NoContent(); });
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
    var job = new Job { Type = type, EntityType = entityType, EntityId = id, TotalSteps = total, Status = "running", Message = "Analisando" };
    db.Jobs.Add(job); await db.SaveChangesAsync(ct);
    try { await work(services, ct); job.Status = "completed"; job.Progress = 100; job.CompletedSteps = total; job.Message = "Concluído"; job.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Results.Ok(job); }
    catch (Exception exception) { job.Status = "failed"; job.Error = exception.Message; job.Message = "Falhou"; job.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Results.Problem("Não foi possível mapear a identidade da marca.", statusCode: 502); }
}
static async Task<IResult> Delete(RedAIDbContext db, Project p) { db.Remove(p); await db.SaveChangesAsync(); return Results.NoContent(); }
static async Task<IResult> DeleteSource(RedAIDbContext db, IAssetStorage storage, BrandSource s, CancellationToken ct) { if (s.StorageKey is not null) await storage.DeleteAsync(s.StorageKey, ct); db.Remove(s); await db.SaveChangesAsync(ct); return Results.NoContent(); }
static async Task<IResult> SaveBrand(RedAIDbContext db, Guid id, string json) { if (!await db.Projects.AnyAsync(x => x.Id == id)) return Results.NotFound(); var p = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id); if (p is null) db.BrandProfiles.Add(new BrandProfile { ProjectId = id, ProfileJson = json, Confidence = .8m }); else { p.ProfileJson = json; p.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); return Results.Ok(JsonDocument.Parse(json).RootElement); }
static async Task MaterializeBrand(RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, Guid id, CancellationToken ct) { var p = await db.Projects.FindAsync([id], ct) ?? throw new KeyNotFoundException(); string json; decimal confidence; if (ai.Mode == "mock") { json = JsonSerializer.Serialize(new { brandName = p.Name, industry = "Serviços", summary = "Perfil demonstrativo para validação do fluxo.", confidence = .8, visualIdentity = new { colors = new[] { new { hex = "#090909", role = "base", confidence = .9 }, new { hex = "#FF3D1F", role = "accent", confidence = .9 }, new { hex = "#F6F6F3", role = "text", confidence = .9 } }, visualStyle = new[] { "editorial" }, imageStyle = new[] { "humano" }, layoutCharacteristics = new[] { "alto contraste" } }, voice = new { traits = new[] { "premium", "direto", "humano" }, formality = .7, energy = .6, avoid = new[] { "jargão" } }, audiences = new[] { new { name = "Decisores", description = "Público principal", confidence = .8 } }, products = new[] { new { name = "Serviços", confidence = .8 } }, contentAnalysis = new { currentPillars = new[] { "autoridade" }, strengths = new[] { "clareza" }, opportunities = new[] { "educação" }, recommendations = new[] { "prova social" } }, restrictions = new[] { "Não inventar promessas" } }); confidence = .8m; } else { using var output = await ai.CompleteStructuredAsync(new StructuredTextRequest("brand-analysis", "Você é o BrandAnalyzer do RED AI. Analise apenas as evidências fornecidas. Não invente fatos. Responda estritamente no schema.", new { project = new { p.Name, p.InstagramHandle, p.WebsiteUrl, p.ManualContext }, sources = await db.BrandSources.Where(s => s.ProjectId == id).Select(s => new { s.Type, s.OriginalFilename, s.MimeType, s.ExtractedText }).ToListAsync(ct) }, "brand-profile", schemas.Load("brand-profile")), ct); json = output.RootElement.GetRawText(); confidence = output.RootElement.GetProperty("confidence").GetDecimal(); } var profile = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id, ct); if (profile is null) db.BrandProfiles.Add(new BrandProfile { ProjectId = id, ProfileJson = json, Confidence = confidence }); else { profile.ProfileJson = json; profile.Confidence = confidence; profile.Status = "generated"; profile.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(ct); }
static async Task<IResult> ApproveBrand(RedAIDbContext db, Guid id) { var p = await db.Projects.FindAsync(id); var b = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id); if (p is null || b is null) return Results.NotFound(); b.Status = "approved"; p.CurrentStep = "campaign"; p.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Ok(b); }
static async Task<IResult> SaveStrategy(RedAIDbContext db, Guid id, string json) { if (!await db.Campaigns.AnyAsync(x => x.Id == id)) return Results.NotFound(); var s = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id); if (s is null) db.CampaignStrategies.Add(new CampaignStrategy { CampaignId = id, StrategyJson = json }); else { s.StrategyJson = json; s.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); return Results.Ok(JsonDocument.Parse(json).RootElement); }
static async Task MaterializeStrategy(RedAIDbContext db, IAIClient ai, IContractSchemaCatalog schemas, Guid id, CancellationToken ct) { var c = await db.Campaigns.FindAsync([id], ct) ?? throw new KeyNotFoundException(); var brand = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == c.ProjectId, ct) ?? throw new InvalidOperationException("Brand DNA must be generated before strategy."); var json = ai.Mode == "mock" ? JsonSerializer.Serialize(new { campaignName = c.Name, strategicObjective = c.Objective, rationale = "Estratégia demonstrativa baseada no Brand DNA aprovado.", contentMix = new[] { new { pillar = "Autoridade", percentage = 34 }, new { pillar = "Educação", percentage = 33 }, new { pillar = "Conversão", percentage = 33 } }, pillars = new[] { new { id = "authority", name = "Autoridade", description = "Constrói confiança" }, new { id = "education", name = "Educação", description = "Explica escolhas" }, new { id = "conversion", name = "Conversão", description = "Convida ao contato" } }, targetAudiences = new[] { "Decisores" }, messages = new[] { "Escolhas bem informadas" }, creativeDirection = new { style = new[] { "editorial" }, recommendations = new[] { "alto contraste" }, avoid = new[] { "promessas absolutas" } }, avoid = new[] { "jargão" } }) : (await ai.CompleteStructuredAsync(new StructuredTextRequest("strategy-generation", "Você é o CampaignStrategist do RED AI. Use exclusivamente o Brand DNA fornecido e o briefing. Responda estritamente no schema.", new { campaign = new { c.Name, c.Objective, c.TargetCount, c.Context }, brandDna = JsonDocument.Parse(brand.ProfileJson).RootElement }, "campaign-strategy", schemas.Load("campaign-strategy")), ct)).RootElement.GetRawText(); var strategy = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id, ct); if (strategy is null) db.CampaignStrategies.Add(new CampaignStrategy { CampaignId = id, StrategyJson = json }); else { strategy.StrategyJson = json; strategy.Status = "generated"; strategy.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(ct); }
static async Task<IResult> ApproveStrategy(RedAIDbContext db, Guid id) { var c = await db.Campaigns.FindAsync(id); var s = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id); if (c is null || s is null) return Results.NotFound(); c.StrategyApproved = true; s.Status = "approved"; await db.SaveChangesAsync(); return Results.Ok(c); }
static async Task<IResult> GenerateIdeas(RedAIDbContext db, JobQueue jobs, Guid id) { if (!await db.Campaigns.AnyAsync(c => c.Id == id)) return Results.NotFound(); return await Start(db, jobs, "ideas-generation", "campaign", id, 30, async (sp, ct) => { var context = sp.GetRequiredService<RedAIDbContext>(); var ai = sp.GetRequiredService<IAIClient>(); var campaign = await context.Campaigns.FindAsync([id], ct) ?? throw new KeyNotFoundException(); var strategy = await context.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id, ct) ?? throw new InvalidOperationException("Strategy must be generated before ideas."); var ideas = ai.Mode == "mock" ? Enumerable.Range(1, 30).Select(i => new { title = $"Ideia de proteção {i:00}", pillar = i % 2 == 0 ? "Educação" : "Autoridade", contentType = i % 3 == 0 ? "Carrossel" : "Post único", description = "Abordagem educativa e humana para proteção patrimonial." }).ToArray() : (await ai.CompleteStructuredAsync(new StructuredTextRequest("ideas-generation", "Você é o IdeaGenerator do RED AI. Gere exatamente 30 ideias distintas. Responda estritamente no schema.", new { campaign = new { campaign.Name, campaign.Objective, campaign.Context }, strategy = JsonDocument.Parse(strategy.StrategyJson).RootElement }, "ideas", sp.GetRequiredService<IContractSchemaCatalog>().Load("ideas")), ct)).RootElement.GetProperty("ideas").EnumerateArray().Select(x => new { title = x.GetProperty("title").GetString() ?? "Ideia", pillar = x.GetProperty("pillar").GetString() ?? "Educação", contentType = x.GetProperty("contentType").GetString() ?? "Post único", description = x.GetProperty("description").GetString() ?? string.Empty }).ToArray(); if (ideas.Length != 30) throw new InvalidOperationException("The IdeaGenerator must return exactly 30 ideas."); context.ContentIdeas.RemoveRange(context.ContentIdeas.Where(i => i.CampaignId == id)); context.ContentIdeas.AddRange(ideas.Select((idea, index) => new ContentIdea { CampaignId = id, Ordinal = index + 1, Title = idea.title, Pillar = idea.pillar, ContentType = idea.contentType, Description = idea.description })); await context.SaveChangesAsync(ct); }); }
static async Task<IResult> SelectIdeas(RedAIDbContext db, Guid id, Guid[] ids) { if (ids.Distinct().Count() != 12) return Results.BadRequest(new { error = "Selecione exatamente 12 ideias." }); var ideas = await db.ContentIdeas.Where(i => i.CampaignId == id).ToListAsync(); if (ideas.Count == 0 || ids.Any(x => ideas.All(i => i.Id != x))) return Results.BadRequest(new { error = "Ideas must belong to the campaign." }); foreach (var i in ideas) i.Selected = ids.Contains(i.Id); await db.SaveChangesAsync(); return Results.Ok(ideas.Where(i => i.Selected)); }
static async Task<IResult> GenerateContent(RedAIDbContext db, JobQueue jobs, Guid id) { var ideas = await db.ContentIdeas.Where(i => i.CampaignId == id && i.Selected).OrderBy(i => i.Ordinal).ToListAsync(); if (ideas.Count != 12) return Results.BadRequest(new { error = "A campanha precisa de exatamente 12 ideias selecionadas." }); return await Start(db, jobs, "content-generation", "campaign", id, 12, async (sp, ct) => { var context = sp.GetRequiredService<RedAIDbContext>(); var ai = sp.GetRequiredService<IAIClient>(); var selected = await context.ContentIdeas.Where(i => i.CampaignId == id && i.Selected).OrderBy(i => i.Ordinal).ToListAsync(ct); context.ContentItems.RemoveRange(context.ContentItems.Where(i => i.CampaignId == id)); foreach (var (idea, index) in selected.Select((v, i) => (v, i))) { var item = new ContentItem { CampaignId = id, SourceIdeaId = idea.Id, Sequence = index + 1 }; context.ContentItems.Add(item); GeneratedCopy copy; if (ai.Mode == "mock") copy = new(idea.Title, $"Proteção começa com escolhas bem informadas. {idea.Description}", null, "Fale com a equipe", "Editorial azul com fotografia humana", ["#proteção"]); else { using var output = await ai.CompleteStructuredAsync(new StructuredTextRequest("content-generation", "Você é o ContentGenerator do RED AI. Crie copy social em português brasileiro para a ideia. Responda estritamente no schema.", new { idea.Title, idea.Pillar, idea.ContentType, idea.Description }, "content-revision", sp.GetRequiredService<IContractSchemaCatalog>().Load("content-revision")), ct); copy = output.Deserialize<GeneratedCopy>() ?? throw new InvalidOperationException("ContentGenerator returned no copy."); } context.ContentRevisions.Add(new ContentRevision { ContentItemId = item.Id, Version = 1, Headline = copy.Headline, Caption = copy.Caption, SupportingText = copy.SupportingText, Cta = copy.Cta, VisualDirection = copy.VisualDirection, HashtagsJson = JsonSerializer.Serialize(copy.Hashtags) }); } await context.SaveChangesAsync(ct); }); }
static async Task<IResult> Revise(RedAIDbContext db, Guid id, ReviseRequest request) { var item = await db.ContentItems.FindAsync(id); if (item is null) return Results.NotFound(); var current = await db.ContentRevisions.Where(r => r.ContentItemId == id).OrderByDescending(r => r.Version).FirstAsync(); var next = new ContentRevision { ContentItemId = id, Version = current.Version + 1, Headline = current.Headline, Caption = current.Caption, SupportingText = current.SupportingText, Cta = current.Cta, HashtagsJson = current.HashtagsJson, VisualDirection = current.VisualDirection, Instruction = request.Instruction }; db.ContentRevisions.Add(next); await db.SaveChangesAsync(); return Results.Ok(next); }
static async Task<IResult> EditRevision(RedAIDbContext db, Guid id, Guid revisionId, EditRevision edit) { var r = await db.ContentRevisions.FirstOrDefaultAsync(x => x.Id == revisionId && x.ContentItemId == id); if (r is null) return Results.NotFound(); r.Headline = edit.Headline ?? r.Headline; r.Caption = edit.Caption ?? r.Caption; r.Cta = edit.Cta ?? r.Cta; r.SupportingText = edit.SupportingText ?? r.SupportingText; await db.SaveChangesAsync(); return Results.Ok(r); }
static async Task<IResult> ApproveRevision(RedAIDbContext db, Guid id, Guid revisionId) { var item = await db.ContentItems.FindAsync(id); var r = await db.ContentRevisions.FirstOrDefaultAsync(x => x.Id == revisionId && x.ContentItemId == id); if (item is null || r is null) return Results.NotFound(); r.IsApproved = true; item.ApprovedRevisionId = r.Id; item.Status = "approved"; await db.SaveChangesAsync(); return Results.Ok(r); }
static async Task<IResult> SelectCreative(RedAIDbContext db, Guid id, Guid versionId) { var v = await db.CreativeVersions.FirstOrDefaultAsync(x => x.Id == versionId && x.ContentItemId == id); if (v is null) return Results.NotFound(); foreach (var x in await db.CreativeVersions.Where(x => x.ContentItemId == id).ToListAsync()) x.IsSelected = x.Id == versionId; await db.SaveChangesAsync(); return Results.Ok(v); }
static async Task MaterializeCreatives(RedAIDbContext db, IDeterministicCreativeRenderer renderer, Guid campaignId, CancellationToken ct) { var campaign = await db.Campaigns.FindAsync([campaignId], ct) ?? throw new KeyNotFoundException(); var items = await db.ContentItems.Where(x => x.CampaignId == campaignId).OrderBy(x => x.Sequence).ToListAsync(ct); foreach (var item in items) { if (await db.CreativeVersions.AnyAsync(x => x.ContentItemId == item.Id, ct)) continue; var revision = await db.ContentRevisions.Where(x => x.ContentItemId == item.Id).OrderByDescending(x => x.Version).FirstAsync(ct); var template = (item.Sequence % 6) switch { 0 => "promotional", 1 => "editorial-bold", 2 => "minimal-center", 3 => "split-image", 4 => "statement", _ => "educational" }; var layout = new CreativeLayout(template, new CreativePalette("#0B0D10", "#F6F6F3", "#FF3D1F"), new CreativeHeadline(revision.Headline, template is "minimal-center" or "statement" ? "center" : "left", "xl", []), new CreativeLogo("rodapé"), revision.SupportingText, revision.Cta); var key = $"projects/{campaign.ProjectId}/content/{item.Id}/creatives/v1/final.png"; await renderer.RenderPngAsync(new DeterministicCreativeRenderRequest(layout, key), ct); db.CreativeVersions.Add(new CreativeVersion { ContentItemId = item.Id, Version = 1, SourceContentRevisionId = revision.Id, LayoutJson = JsonSerializer.Serialize(layout), ImageStorageKey = key }); } await db.SaveChangesAsync(ct); }
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
static byte[] CreateExportZip(Project project, Campaign campaign, IReadOnlyList<ContentItem> items, IReadOnlyList<CreativeVersion> creatives) { using var stream = new MemoryStream(); using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) { var manifest = archive.CreateEntry("campaign.json"); using (var writer = new StreamWriter(manifest.Open(), Encoding.UTF8)) writer.Write(JsonSerializer.Serialize(new { project = project.Name, campaign = campaign.Name, contentCount = items.Count, creativeCount = creatives.Count }, new JsonSerializerOptions { WriteIndented = true })); foreach (var creative in creatives) { var entry = archive.CreateEntry($"creatives/content-{creative.ContentItemId}/v{creative.Version}.json"); using var writer = new StreamWriter(entry.Open(), Encoding.UTF8); writer.Write(creative.LayoutJson); } } return stream.ToArray(); }
public record CreateProject(string Name, string? InstagramHandle, string? WebsiteUrl, string? ManualContext);
public record CreateCampaign(string Name, string? Objective, int TargetCount, string? Context);
public record SelectIdeas(Guid[] IdeaIds);
public record ReviseRequest(string Instruction);
public record EditRevision(string? Headline, string? SupportingText, string? Caption, string? Cta);
public record GeneratedCopy(string Headline, string Caption, string? SupportingText, string? Cta, string VisualDirection, string[] Hashtags);
