using RedAI.Application;
using RedAI.Domain;

namespace RedAI.Infrastructure;

/// <summary>EF-backed audit writer. Register scoped in the API host when enabling live AI.</summary>
public sealed class EfAIRunWriter(RedAIDbContext db) : IAIRunWriter
{
    public async Task<Guid> StartAsync(AIRunStart run, CancellationToken ct)
    {
        var entity = new AIRun { Operation = run.Operation, EntityType = run.EntityType, EntityId = run.EntityId, Model = run.Model, InputJson = run.InputJson };
        db.AIRuns.Add(entity); await db.SaveChangesAsync(ct); return entity.Id;
    }
    public async Task CompleteAsync(Guid id, string outputJson, CancellationToken ct)
    {
        var entity = await db.AIRuns.FindAsync([id], ct); if (entity is null) return;
        entity.OutputJson = outputJson; entity.Status = "completed"; entity.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
    }
    public async Task FailAsync(Guid id, string error, CancellationToken ct)
    {
        var entity = await db.AIRuns.FindAsync([id], ct); if (entity is null) return;
        entity.Error = error; entity.Status = "failed"; entity.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
    }
}
