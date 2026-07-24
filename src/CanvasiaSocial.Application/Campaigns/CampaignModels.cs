using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Application.Campaigns;

public sealed record CreateCampaignRequest(
    string Name,
    Platform Platform,
    Guid? SocialAccountId,
    CampaignMode Mode,
    DateTime StartLocal,
    int IntervalMinutes,
    int DailyLimit,
    TimeOnly AllowedStartTime,
    TimeOnly AllowedEndTime,
    bool IncludePrice,
    bool IncludeProductLink,
    IReadOnlyCollection<Guid> ProductIds,
    string CreatedByUserId);

public sealed record CampaignListItem(
    Guid Id,
    string Name,
    Platform Platform,
    CampaignMode Mode,
    CampaignStatus Status,
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    DateTime? StartAtUtc,
    DateTime CreatedAtUtc);

public sealed record CampaignItemDetails(
    Guid Id,
    Guid ProductId,
    string ProductTitle,
    ContentStatus Status,
    string? Caption,
    string? Error,
    DateTime? ScheduledAtUtc);

public sealed record CampaignDetails(
    Guid Id,
    string Name,
    Platform Platform,
    CampaignMode Mode,
    CampaignStatus Status,
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    DateTime? StartAtUtc,
    string TimeZoneId,
    IReadOnlyList<CampaignItemDetails> Items);

public sealed record ScheduleRequest(
    DateTime StartLocal,
    string TimeZoneId,
    TimeOnly AllowedStart,
    TimeOnly AllowedEnd,
    int IntervalMinutes,
    int DailyLimit,
    int Count);

public sealed record CalendarEntry(
    Guid ScheduledPostId,
    Guid? CampaignId,
    string CampaignName,
    string ProductTitle,
    Platform Platform,
    ContentStatus Status,
    DateTime ScheduledAtUtc,
    string TimeZoneId);

public sealed record SocialAccountOption(Guid Id, Platform Platform, string DisplayName);
