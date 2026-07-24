using CanvasiaSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanvasiaSocial.Infrastructure.Persistence.Configurations;

internal static class ConfigurationHelpers
{
    public static void ConfigurePlatform<T>(EntityTypeBuilder<T> builder, string propertyName)
        where T : class
    {
        builder.Property(propertyName).HasConversion<string>().HasMaxLength(32);
    }
}

internal sealed class SocialAccountConfiguration : IEntityTypeConfiguration<SocialAccount>
{
    public void Configure(EntityTypeBuilder<SocialAccount> builder)
    {
        builder.ToTable("SocialAccounts");
        builder.HasKey(x => x.Id);
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(SocialAccount.Platform));
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalAccountId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(200);
        builder.Property(x => x.ProfileImageUrl).HasMaxLength(2048);
        builder.Property(x => x.EncryptedAccessToken).HasColumnType("text").IsRequired();
        builder.Property(x => x.EncryptedRefreshToken).HasColumnType("text");
        builder.Property(x => x.Scopes).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.Platform, x.ExternalAccountId }).IsUnique();
    }
}

internal sealed class OAuthStateConfiguration : IEntityTypeConfiguration<OAuthState>
{
    public void Configure(EntityTypeBuilder<OAuthState> builder)
    {
        builder.ToTable("OAuthStates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StateHash).HasMaxLength(64).IsRequired();
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(OAuthState.Platform));
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.EncryptedCodeVerifier).HasColumnType("text");
        builder.HasIndex(x => x.StateHash).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}

internal sealed class ProductCacheConfiguration : IEntityTypeConfiguration<ProductCache>
{
    public void Configure(EntityTypeBuilder<ProductCache> builder)
    {
        builder.ToTable("ProductCaches");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CanvasiaProductId).IsUnique();
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CategoryName).HasMaxLength(300);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.DiscountedPrice).HasPrecision(18, 2);
        builder.Property(x => x.ProductUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.PromptSummary).HasColumnType("text");
        builder.Property(x => x.RawJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.CategoryName);
        builder.HasIndex(x => new { x.InStock, x.IsDiscounted });
        builder.HasMany(x => x.Images).WithOne(x => x.ProductCache)
            .HasForeignKey(x => x.ProductCacheId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CanvasiaSyncStateConfiguration : IEntityTypeConfiguration<CanvasiaSyncState>
{
    public void Configure(EntityTypeBuilder<CanvasiaSyncState> builder)
    {
        builder.ToTable("CanvasiaSyncStates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);
    }
}

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).HasMaxLength(2048).IsRequired();
        builder.HasIndex(x => new { x.ProductCacheId, x.SortOrder });
    }
}

internal sealed class GeneratedContentConfiguration : IEntityTypeConfiguration<GeneratedContent>
{
    public void Configure(EntityTypeBuilder<GeneratedContent> builder)
    {
        builder.ToTable("GeneratedContents");
        builder.HasKey(x => x.Id);
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(GeneratedContent.Platform));
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Caption).HasColumnType("text").IsRequired();
        builder.Property(x => x.StoryText).HasColumnType("text");
        builder.Property(x => x.CallToAction).HasMaxLength(500);
        builder.Property(x => x.HashtagsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Tone).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PromptVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PromptHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RawAiResponse).HasColumnType("text");
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.HasOne(x => x.ProductCache).WithMany().HasForeignKey(x => x.ProductCacheId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ProductCacheId);
    }
}

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(Campaign.Platform));
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(x => x.SocialAccount).WithMany().HasForeignKey(x => x.SocialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}

internal sealed class CampaignItemConfiguration : IEntityTypeConfiguration<CampaignItem>
{
    public void Configure(EntityTypeBuilder<CampaignItem> builder)
    {
        builder.ToTable("CampaignItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ErrorMessage).HasColumnType("text");
        builder.HasOne(x => x.ProductCache).WithMany().HasForeignKey(x => x.ProductCacheId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeneratedContent).WithMany().HasForeignKey(x => x.GeneratedContentId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ScheduledPost).WithMany().HasForeignKey(x => x.ScheduledPostId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.CampaignId, x.SortOrder }).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.ProductCacheId }).IsUnique();
        builder.HasIndex(x => x.ScheduledPostId).IsUnique();
    }
}

internal sealed class ScheduledPostConfiguration : IEntityTypeConfiguration<ScheduledPost>
{
    public void Configure(EntityTypeBuilder<ScheduledPost> builder)
    {
        builder.ToTable("ScheduledPosts");
        builder.HasKey(x => x.Id);
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(ScheduledPost.Platform));
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ExternalPostId).HasMaxLength(500);
        builder.Property(x => x.ExternalPostUrl).HasMaxLength(2048);
        builder.Property(x => x.LastErrorCode).HasMaxLength(200);
        builder.Property(x => x.LastErrorMessage).HasColumnType("text");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
        builder.HasIndex(x => new { x.Status, x.NextRetryAtUtc, x.ScheduledAtUtc });
        builder.HasOne(x => x.SocialAccount).WithMany().HasForeignKey(x => x.SocialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeneratedContent).WithMany().HasForeignKey(x => x.GeneratedContentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.PublishAttempts).WithOne(x => x.ScheduledPost)
            .HasForeignKey(x => x.ScheduledPostId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PublishAttemptConfiguration : IEntityTypeConfiguration<PublishAttempt>
{
    public void Configure(EntityTypeBuilder<PublishAttempt> builder)
    {
        builder.ToTable("PublishAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlatformErrorCode).HasMaxLength(200);
        builder.Property(x => x.SanitizedRequest).HasColumnType("text");
        builder.Property(x => x.SanitizedResponse).HasColumnType("text");
        builder.Property(x => x.ErrorMessage).HasColumnType("text");
        builder.HasIndex(x => new { x.ScheduledPostId, x.AttemptNumber }).IsUnique();
    }
}

internal sealed class AiGenerationJobConfiguration : IEntityTypeConfiguration<AiGenerationJob>
{
    public void Configure(EntityTypeBuilder<AiGenerationJob> builder)
    {
        builder.ToTable("AiGenerationJobs");
        builder.HasKey(x => x.Id);
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(AiGenerationJob.Platform));
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ModelName).HasMaxLength(200);
        builder.Property(x => x.ErrorMessage).HasColumnType("text");
        builder.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ProductCache).WithMany().HasForeignKey(x => x.ProductCacheId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CampaignItem).WithMany().HasForeignKey(x => x.CampaignItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CampaignItemId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}

internal sealed class ProductPublicationHistoryConfiguration : IEntityTypeConfiguration<ProductPublicationHistory>
{
    public void Configure(EntityTypeBuilder<ProductPublicationHistory> builder)
    {
        builder.ToTable("ProductPublicationHistories");
        builder.HasKey(x => x.Id);
        ConfigurationHelpers.ConfigurePlatform(builder, nameof(ProductPublicationHistory.Platform));
        builder.HasOne(x => x.ProductCache).WithMany().HasForeignKey(x => x.ProductCacheId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SocialAccount).WithMany().HasForeignKey(x => x.SocialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ScheduledPost).WithMany().HasForeignKey(x => x.ScheduledPostId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProductCacheId, x.Platform, x.PublishedAtUtc });
        builder.HasIndex(x => x.ScheduledPostId).IsUnique();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.Action).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(200);
        builder.Property(x => x.SanitizedDetails).HasColumnType("text");
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
