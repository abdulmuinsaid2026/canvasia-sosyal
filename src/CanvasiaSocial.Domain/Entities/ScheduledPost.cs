using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class ScheduledPost : Entity
{
    public Guid? SocialAccountId { get; set; }
    public SocialAccount? SocialAccount { get; set; }
    public Guid GeneratedContentId { get; set; }
    public GeneratedContent GeneratedContent { get; set; } = null!;
    public Platform Platform { get; set; }
    public ContentStatus Status { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? ExternalPostId { get; set; }
    public string? ExternalPostUrl { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<PublishAttempt> PublishAttempts { get; set; } = [];
}
