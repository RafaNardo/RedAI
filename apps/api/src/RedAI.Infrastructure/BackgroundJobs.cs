using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedAI.Domain;

namespace RedAI.Infrastructure;

public sealed record QueuedJob(Guid JobId, Func<IServiceProvider, CancellationToken, Task> Execute);

public sealed class JobQueue
{
    private readonly Channel<QueuedJob> _channel = Channel.CreateUnbounded<QueuedJob>();
    public ValueTask EnqueueAsync(QueuedJob job, CancellationToken ct = default) => _channel.Writer.WriteAsync(job, ct);
    public IAsyncEnumerable<QueuedJob> DequeueAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class JobWorker(IServiceScopeFactory scopes, JobQueue queue, ILogger<JobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var work in queue.DequeueAllAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RedAIDbContext>();
            var job = await db.Jobs.FindAsync([work.JobId], stoppingToken); if (job is null) continue;
            job.Status = "running"; job.Message = "Processando"; await db.SaveChangesAsync(stoppingToken);
            try { await work.Execute(scope.ServiceProvider, stoppingToken); job = await db.Jobs.FindAsync([work.JobId], stoppingToken); if (job is not null) { job.Status = "completed"; job.Progress = 100; job.CompletedSteps = job.TotalSteps; job.Message = "Concluído"; job.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(stoppingToken); } }
            catch (Exception ex)
            {
                logger.LogError(ex, "RED AI job {JobId} failed", work.JobId);
                using var failureScope = scopes.CreateScope();
                var failureDb = failureScope.ServiceProvider.GetRequiredService<RedAIDbContext>();
                var failedJob = await failureDb.Jobs.FindAsync([work.JobId], stoppingToken);
                if (failedJob is not null)
                {
                    failedJob.Status = "failed";
                    failedJob.Error = ex.Message;
                    failedJob.Message = "Falhou";
                    failedJob.CompletedAt = DateTimeOffset.UtcNow;
                    await failureDb.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }
}
