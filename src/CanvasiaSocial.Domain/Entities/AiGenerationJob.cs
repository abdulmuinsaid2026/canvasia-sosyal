using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class AiGenerationJob : Entity
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid ProductCacheId { get; set; }
    public ProductCache ProductCache { get; set; } = null!;
    public Guid? CampaignItemId { get; set; }
    public CampaignItem? CampaignItem { get; set; }
    public Platform Platform { get; set; }
    public ContentStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? ModelName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
