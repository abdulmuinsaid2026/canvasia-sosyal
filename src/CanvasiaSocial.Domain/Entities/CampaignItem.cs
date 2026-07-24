using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class CampaignItem : Entity
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid ProductCacheId { get; set; }
    public ProductCache ProductCache { get; set; } = null!;
    public Guid? GeneratedContentId { get; set; }
    public GeneratedContent? GeneratedContent { get; set; }
    public Guid? ScheduledPostId { get; set; }
    public ScheduledPost? ScheduledPost { get; set; }
    public int SortOrder { get; set; }
    public ContentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
