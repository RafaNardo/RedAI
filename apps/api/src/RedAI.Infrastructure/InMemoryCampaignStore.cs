using System.Collections.Concurrent;
using RedAI.Domain;
namespace RedAI.Infrastructure;
public sealed class InMemoryCampaignStore { public ConcurrentDictionary<Guid, Project> Projects { get; } = new(); public ConcurrentDictionary<Guid, Job> Jobs { get; } = new(); public Project Add(Project project) { Projects[project.Id] = project; return project; } public Job Complete(string type, int total = 1) { var job = new Job { Type = type, EntityType = "campaign", EntityId = Guid.Empty, Status = "completed", Progress = 100, CompletedSteps = total, TotalSteps = total, Message = $"{type} concluído no modo mock", CompletedAt = DateTimeOffset.UtcNow }; Jobs[job.Id] = job; return job; } }
