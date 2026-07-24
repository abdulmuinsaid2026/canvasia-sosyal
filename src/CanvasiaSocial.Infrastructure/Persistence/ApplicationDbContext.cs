using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();
    public DbSet<ProductCache> ProductCaches => Set<ProductCache>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<CanvasiaSyncState> CanvasiaSyncStates => Set<CanvasiaSyncState>();
    public DbSet<GeneratedContent> GeneratedContents => Set<GeneratedContent>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignItem> CampaignItems => Set<CampaignItem>();
    public DbSet<ScheduledPost> ScheduledPosts => Set<ScheduledPost>();
    public DbSet<PublishAttempt> PublishAttempts => Set<PublishAttempt>();
    public DbSet<AiGenerationJob> AiGenerationJobs => Set<AiGenerationJob>();
    public DbSet<ProductPublicationHistory> ProductPublicationHistories => Set<ProductPublicationHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
