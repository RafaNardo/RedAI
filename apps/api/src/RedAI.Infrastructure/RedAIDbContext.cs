using Microsoft.EntityFrameworkCore;
using RedAI.Domain;
namespace RedAI.Infrastructure;
public sealed class RedAIDbContext(DbContextOptions<RedAIDbContext> options) : DbContext(options) {
 public DbSet<Project> Projects => Set<Project>(); public DbSet<Campaign> Campaigns => Set<Campaign>(); public DbSet<ContentIdea> ContentIdeas => Set<ContentIdea>(); public DbSet<BrandSource> BrandSources => Set<BrandSource>(); public DbSet<BrandProfile> BrandProfiles => Set<BrandProfile>(); public DbSet<CampaignStrategy> CampaignStrategies => Set<CampaignStrategy>(); public DbSet<ContentItem> ContentItems => Set<ContentItem>(); public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>(); public DbSet<CreativeVersion> CreativeVersions => Set<CreativeVersion>(); public DbSet<Job> Jobs => Set<Job>(); public DbSet<AIRun> AIRuns => Set<AIRun>();
 protected override void OnModelCreating(ModelBuilder b) {
  b.Entity<Project>().ToTable("projects").HasKey(x => x.Id); b.Entity<Project>().Property(x => x.Name).HasMaxLength(160); b.Entity<Project>().HasOne(x => x.Campaign).WithOne().HasForeignKey<Campaign>(x => x.ProjectId);
  b.Entity<Campaign>().ToTable("campaigns").HasKey(x => x.Id); b.Entity<Campaign>().Property(x => x.Name).HasMaxLength(200);
  b.Entity<ContentIdea>().ToTable("content_ideas").HasIndex(x => new { x.CampaignId, x.Ordinal }).IsUnique();
  b.Entity<Job>().ToTable("jobs"); b.Entity<AIRun>().ToTable("ai_runs");
  b.Entity<BrandSource>().ToTable("brand_sources"); b.Entity<BrandProfile>().ToTable("brand_profiles").HasIndex(x => x.ProjectId).IsUnique(); b.Entity<CampaignStrategy>().ToTable("campaign_strategies").HasIndex(x => x.CampaignId).IsUnique(); b.Entity<ContentItem>().ToTable("content_items").HasIndex(x => new { x.CampaignId, x.Sequence }).IsUnique(); b.Entity<ContentRevision>().ToTable("content_revisions").HasIndex(x => new { x.ContentItemId, x.Version }).IsUnique(); b.Entity<CreativeVersion>().ToTable("creative_versions").HasIndex(x => new { x.ContentItemId, x.Version }).IsUnique();
 }
}
