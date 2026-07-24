using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class Campaign : Entity
{
    public string Name { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public Guid? SocialAccountId { get; set; }
    public SocialAccount? SocialAccount { get; set; }
    public CampaignMode Mode { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime? StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public int IntervalMinutes { get; set; }
    public int? DailyLimit { get; set; }
    public TimeOnly? AllowedStartTime { get; set; }
    public TimeOnly? AllowedEndTime { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public bool RequireApproval { get; set; }
    public bool IncludePrice { get; set; } = true;
    public bool IncludeProductLink { get; set; } = true;
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<CampaignItem> Items { get; set; } = [];
}
