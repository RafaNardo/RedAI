using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RedAI.Application;
using RedAI.Domain;
using RedAI.Infrastructure;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<InMemoryCampaignStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddDbContext<RedAIDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=redai;Username=redai;Password=redai_dev"));
builder.Services.AddScoped<IAssetStorage, LocalAssetStorage>();
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

api.MapPost("/projects/{id:guid}/brand/analyze", async (Guid id, RedAIDbContext db, JobQueue jobs) => !await db.Projects.AnyAsync(p => p.Id == id) ? Results.NotFound() : await Start(db, jobs, "brand-analysis", "project", id, 1, async (sp, ct) => await MaterializeBrand(sp.GetRequiredService<RedAIDbContext>(), id, ct)));
api.MapGet("/projects/{id:guid}/brand", async (Guid id, RedAIDbContext db) => await db.BrandProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id) is { } b ? Results.Content(b.ProfileJson, "application/json") : Results.NotFound());
api.MapPut("/projects/{id:guid}/brand", async (Guid id, JsonElement profile, RedAIDbContext db) => await SaveBrand(db, id, profile.GetRawText()));
api.MapPost("/projects/{id:guid}/brand/approve", async (Guid id, RedAIDbContext db) => await ApproveBrand(db, id));

api.MapPost("/projects/{id:guid}/campaign", async (Guid id, CreateCampaign r, RedAIDbContext db) => {
    var p = await db.Projects.Include(x => x.Campaign).FirstOrDefaultAsync(x => x.Id == id); if (p is null) return Results.NotFound(); if (p.Campaign is not null) return Results.Conflict(new { error = "Project already has a campaign" });
    var c = new Campaign { ProjectId = id, Name = r.Name, Objective = r.Objective ?? "AI decide", TargetCount = r.TargetCount == 0 ? 12 : r.TargetCount, Context = r.Context }; db.Campaigns.Add(c); p.CurrentStep = "strategy"; p.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Created($"/api/campaigns/{c.Id}", c);
});
api.MapGet("/campaigns/{id:guid}", async (Guid id, RedAIDbContext db) => await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id) is { } c ? Results.Ok(c) : Results.NotFound());
api.MapPost("/campaigns/{id:guid}/strategy/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await db.Campaigns.AnyAsync(c => c.Id == id) ? await Start(db, jobs, "strategy-generation", "campaign", id, 1, async (sp, ct) => await MaterializeStrategy(sp.GetRequiredService<RedAIDbContext>(), id, ct)) : Results.NotFound());
api.MapPut("/campaigns/{id:guid}/strategy", async (Guid id, JsonElement strategy, RedAIDbContext db) => await SaveStrategy(db, id, strategy.GetRawText()));
api.MapPost("/campaigns/{id:guid}/strategy/approve", async (Guid id, RedAIDbContext db) => await ApproveStrategy(db, id));

api.MapPost("/campaigns/{id:guid}/ideas/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateIdeas(db, jobs, id));
api.MapGet("/campaigns/{id:guid}/ideas", async (Guid id, RedAIDbContext db) => !await db.Campaigns.AnyAsync(c => c.Id == id) ? Results.NotFound() : Results.Ok(await db.ContentIdeas.Where(i => i.CampaignId == id).OrderBy(i => i.Ordinal).ToListAsync()));
api.MapPost("/campaigns/{id:guid}/ideas/select", async (Guid id, SelectIdeas r, RedAIDbContext db) => await SelectIdeas(db, id, r.IdeaIds));
api.MapPost("/campaigns/{id:guid}/ideas/auto-select", async (Guid id, RedAIDbContext db) => await SelectIdeas(db, id, (await db.ContentIdeas.Where(i => i.CampaignId == id).OrderBy(i => i.Ordinal).Take(12).Select(i => i.Id).ToArrayAsync())));
api.MapPost("/campaigns/{id:guid}/ideas/regenerate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateIdeas(db, jobs, id));

api.MapPost("/campaigns/{id:guid}/content/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await GenerateContent(db, jobs, id));
api.MapGet("/campaigns/{id:guid}/content", async (Guid id, RedAIDbContext db) => Results.Ok(await db.ContentItems.Where(x => x.CampaignId == id).OrderBy(x => x.Sequence).ToListAsync()));
api.MapGet("/content/{id:guid}", async (Guid id, RedAIDbContext db) => await db.ContentItems.FindAsync(id) is { } item ? Results.Ok(new { item, revisions = await db.ContentRevisions.Where(r => r.ContentItemId == id).OrderBy(r => r.Version).ToListAsync() }) : Results.NotFound());
api.MapPost("/content/{id:guid}/revise", async (Guid id, ReviseRequest r, RedAIDbContext db) => await Revise(db, id, r));
api.MapPut("/content/{id:guid}/revision/{revisionId:guid}", async (Guid id, Guid revisionId, EditRevision r, RedAIDbContext db) => await EditRevision(db, id, revisionId, r));
api.MapPost("/content/{id:guid}/revision/{revisionId:guid}/approve", async (Guid id, Guid revisionId, RedAIDbContext db) => await ApproveRevision(db, id, revisionId));

api.MapPost("/campaigns/{id:guid}/creatives/generate", async (Guid id, RedAIDbContext db, JobQueue jobs) => await db.Campaigns.AnyAsync(c => c.Id == id) ? await Start(db, jobs, "creative-generation", "campaign", id, 12, async (sp, ct) => await MaterializeCreatives(sp.GetRequiredService<RedAIDbContext>(), id, ct)) : Results.NotFound());
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
static async Task<IResult> Delete(RedAIDbContext db, Project p) { db.Remove(p); await db.SaveChangesAsync(); return Results.NoContent(); }
static async Task<IResult> DeleteSource(RedAIDbContext db, IAssetStorage storage, BrandSource s, CancellationToken ct) { if (s.StorageKey is not null) await storage.DeleteAsync(s.StorageKey, ct); db.Remove(s); await db.SaveChangesAsync(ct); return Results.NoContent(); }
static async Task<IResult> SaveBrand(RedAIDbContext db, Guid id, string json) { if (!await db.Projects.AnyAsync(x => x.Id == id)) return Results.NotFound(); var p = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id); if (p is null) db.BrandProfiles.Add(new BrandProfile { ProjectId = id, ProfileJson = json, Confidence = .8m }); else { p.ProfileJson = json; p.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); return Results.Ok(JsonDocument.Parse(json).RootElement); }
static async Task MaterializeBrand(RedAIDbContext db, Guid id, CancellationToken ct) { var p = await db.Projects.FindAsync([id], ct) ?? throw new KeyNotFoundException(); var json = JsonSerializer.Serialize(new { brandName = p.Name, industry = "Serviços", confidence = .8, colors = new[] { "#090909", "#FF3D1F", "#F6F6F3" }, voice = new[] { "premium", "direto", "humano" } }); var profile = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id, ct); if (profile is null) db.BrandProfiles.Add(new BrandProfile { ProjectId = id, ProfileJson = json, Confidence = .8m }); else { profile.ProfileJson = json; profile.Status = "generated"; profile.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(ct); }
static async Task<IResult> ApproveBrand(RedAIDbContext db, Guid id) { var p = await db.Projects.FindAsync(id); var b = await db.BrandProfiles.FirstOrDefaultAsync(x => x.ProjectId == id); if (p is null || b is null) return Results.NotFound(); b.Status = "approved"; p.CurrentStep = "campaign"; p.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return Results.Ok(b); }
static async Task<IResult> SaveStrategy(RedAIDbContext db, Guid id, string json) { if (!await db.Campaigns.AnyAsync(x => x.Id == id)) return Results.NotFound(); var s = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id); if (s is null) db.CampaignStrategies.Add(new CampaignStrategy { CampaignId = id, StrategyJson = json }); else { s.StrategyJson = json; s.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); return Results.Ok(JsonDocument.Parse(json).RootElement); }
static async Task MaterializeStrategy(RedAIDbContext db, Guid id, CancellationToken ct) { var c = await db.Campaigns.FindAsync([id], ct) ?? throw new KeyNotFoundException(); var json = JsonSerializer.Serialize(new { campaignName = c.Name, objective = c.Objective, pillars = new[] { "Autoridade", "Educação", "Conversão" }, cadence = "3 posts por semana" }); var strategy = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id, ct); if (strategy is null) db.CampaignStrategies.Add(new CampaignStrategy { CampaignId = id, StrategyJson = json }); else { strategy.StrategyJson = json; strategy.Status = "generated"; strategy.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(ct); }
static async Task<IResult> ApproveStrategy(RedAIDbContext db, Guid id) { var c = await db.Campaigns.FindAsync(id); var s = await db.CampaignStrategies.FirstOrDefaultAsync(x => x.CampaignId == id); if (c is null || s is null) return Results.NotFound(); c.StrategyApproved = true; s.Status = "approved"; await db.SaveChangesAsync(); return Results.Ok(c); }
static async Task<IResult> GenerateIdeas(RedAIDbContext db, JobQueue jobs, Guid id) { if (!await db.Campaigns.AnyAsync(c => c.Id == id)) return Results.NotFound(); return await Start(db, jobs, "ideas-generation", "campaign", id, 30, async (sp, ct) => { var context = sp.GetRequiredService<RedAIDbContext>(); context.ContentIdeas.RemoveRange(context.ContentIdeas.Where(i => i.CampaignId == id)); context.ContentIdeas.AddRange(Enumerable.Range(1, 30).Select(i => new ContentIdea { CampaignId = id, Ordinal = i, Title = $"Ideia de proteção {i:00}", Pillar = i % 2 == 0 ? "Educação" : "Autoridade", ContentType = i % 3 == 0 ? "Carrossel" : "Post único", Description = "Abordagem educativa e humana para proteção patrimonial." })); await context.SaveChangesAsync(ct); }); }
static async Task<IResult> SelectIdeas(RedAIDbContext db, Guid id, Guid[] ids) { if (ids.Distinct().Count() != 12) return Results.BadRequest(new { error = "Selecione exatamente 12 ideias." }); var ideas = await db.ContentIdeas.Where(i => i.CampaignId == id).ToListAsync(); if (ideas.Count == 0 || ids.Any(x => ideas.All(i => i.Id != x))) return Results.BadRequest(new { error = "Ideas must belong to the campaign." }); foreach (var i in ideas) i.Selected = ids.Contains(i.Id); await db.SaveChangesAsync(); return Results.Ok(ideas.Where(i => i.Selected)); }
static async Task<IResult> GenerateContent(RedAIDbContext db, JobQueue jobs, Guid id) { var ideas = await db.ContentIdeas.Where(i => i.CampaignId == id && i.Selected).OrderBy(i => i.Ordinal).ToListAsync(); if (ideas.Count != 12) return Results.BadRequest(new { error = "A campanha precisa de exatamente 12 ideias selecionadas." }); return await Start(db, jobs, "content-generation", "campaign", id, 12, async (sp, ct) => { var context = sp.GetRequiredService<RedAIDbContext>(); var selected = await context.ContentIdeas.Where(i => i.CampaignId == id && i.Selected).OrderBy(i => i.Ordinal).ToListAsync(ct); context.ContentItems.RemoveRange(context.ContentItems.Where(i => i.CampaignId == id)); foreach (var (idea, index) in selected.Select((v, i) => (v, i))) { var item = new ContentItem { CampaignId = id, SourceIdeaId = idea.Id, Sequence = index + 1 }; context.ContentItems.Add(item); context.ContentRevisions.Add(new ContentRevision { ContentItemId = item.Id, Version = 1, Headline = idea.Title, Caption = $"Proteção começa com escolhas bem informadas. {idea.Description}", Cta = "Fale com a equipe", VisualDirection = "Editorial azul com fotografia humana" }); } await context.SaveChangesAsync(ct); }); }
static async Task<IResult> Revise(RedAIDbContext db, Guid id, ReviseRequest request) { var item = await db.ContentItems.FindAsync(id); if (item is null) return Results.NotFound(); var current = await db.ContentRevisions.Where(r => r.ContentItemId == id).OrderByDescending(r => r.Version).FirstAsync(); var next = new ContentRevision { ContentItemId = id, Version = current.Version + 1, Headline = current.Headline, Caption = current.Caption, SupportingText = current.SupportingText, Cta = current.Cta, HashtagsJson = current.HashtagsJson, VisualDirection = current.VisualDirection, Instruction = request.Instruction }; db.ContentRevisions.Add(next); await db.SaveChangesAsync(); return Results.Ok(next); }
static async Task<IResult> EditRevision(RedAIDbContext db, Guid id, Guid revisionId, EditRevision edit) { var r = await db.ContentRevisions.FirstOrDefaultAsync(x => x.Id == revisionId && x.ContentItemId == id); if (r is null) return Results.NotFound(); r.Headline = edit.Headline ?? r.Headline; r.Caption = edit.Caption ?? r.Caption; r.Cta = edit.Cta ?? r.Cta; r.SupportingText = edit.SupportingText ?? r.SupportingText; await db.SaveChangesAsync(); return Results.Ok(r); }
static async Task<IResult> ApproveRevision(RedAIDbContext db, Guid id, Guid revisionId) { var item = await db.ContentItems.FindAsync(id); var r = await db.ContentRevisions.FirstOrDefaultAsync(x => x.Id == revisionId && x.ContentItemId == id); if (item is null || r is null) return Results.NotFound(); r.IsApproved = true; item.ApprovedRevisionId = r.Id; item.Status = "approved"; await db.SaveChangesAsync(); return Results.Ok(r); }
static async Task<IResult> SelectCreative(RedAIDbContext db, Guid id, Guid versionId) { var v = await db.CreativeVersions.FirstOrDefaultAsync(x => x.Id == versionId && x.ContentItemId == id); if (v is null) return Results.NotFound(); foreach (var x in await db.CreativeVersions.Where(x => x.ContentItemId == id).ToListAsync()) x.IsSelected = x.Id == versionId; await db.SaveChangesAsync(); return Results.Ok(v); }
static async Task MaterializeCreatives(RedAIDbContext db, Guid campaignId, CancellationToken ct) { var items = await db.ContentItems.Where(x => x.CampaignId == campaignId).OrderBy(x => x.Sequence).ToListAsync(ct); foreach (var item in items) { if (await db.CreativeVersions.AnyAsync(x => x.ContentItemId == item.Id, ct)) continue; var revision = await db.ContentRevisions.Where(x => x.ContentItemId == item.Id).OrderByDescending(x => x.Version).FirstAsync(ct); var layout = JsonSerializer.Serialize(new { template = (item.Sequence % 6) switch { 0 => "promotional", 1 => "editorial-bold", 2 => "minimal-center", 3 => "split-image", 4 => "statement", _ => "educational" }, headline = revision.Headline, cta = revision.Cta }); db.CreativeVersions.Add(new CreativeVersion { ContentItemId = item.Id, Version = 1, SourceContentRevisionId = revision.Id, LayoutJson = layout }); } await db.SaveChangesAsync(ct); }
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
static byte[] CreateExportZip(Project project, Campaign campaign, IReadOnlyList<ContentItem> items, IReadOnlyList<CreativeVersion> creatives) { using var stream = new MemoryStream(); using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) { var manifest = archive.CreateEntry("campaign.json"); using (var writer = new StreamWriter(manifest.Open(), Encoding.UTF8)) writer.Write(JsonSerializer.Serialize(new { project = project.Name, campaign = campaign.Name, contentCount = items.Count, creativeCount = creatives.Count }, new JsonSerializerOptions { WriteIndented = true })); foreach (var creative in creatives) { var entry = archive.CreateEntry($"creatives/content-{creative.ContentItemId}/v{creative.Version}.json"); using var writer = new StreamWriter(entry.Open(), Encoding.UTF8); writer.Write(creative.LayoutJson); } } return stream.ToArray(); }
public record CreateProject(string Name, string? InstagramHandle, string? WebsiteUrl, string? ManualContext);
public record CreateCampaign(string Name, string? Objective, int TargetCount, string? Context);
public record SelectIdeas(Guid[] IdeaIds);
public record ReviseRequest(string Instruction);
public record EditRevision(string? Headline, string? SupportingText, string? Caption, string? Cta);
