using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Application.Ai;

namespace CanvasiaSocial.Application.Campaigns;

public interface ICampaignService
{
    Task<Guid> CreateAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampaignListItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialAccountOption>> GetSocialAccountsAsync(Platform platform, CancellationToken cancellationToken = default);
    Task<CampaignDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task PauseAsync(Guid id, CancellationToken cancellationToken = default);
    Task ResumeAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task RetryItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid campaignId, string userId, CancellationToken cancellationToken = default);
    Task ScheduleAsync(Guid campaignId, CancellationToken cancellationToken = default);
}

public interface IDraftService
{
    Task<IReadOnlyList<GeneratedContentView>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task ReviewAsync(IReadOnlyCollection<Guid> contentIds, bool approve, string userId, CancellationToken cancellationToken = default);
    Task ScheduleApprovedAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default);
}

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEntry>> GetAsync(DateTime fromUtc, DateTime toUtc, Platform? platform, ContentStatus? status, CancellationToken cancellationToken = default);
    Task RescheduleAsync(Guid scheduledPostId, DateTime localTime, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid scheduledPostId, CancellationToken cancellationToken = default);
}

public interface IScheduleCalculator
{
    IReadOnlyList<DateTime> Calculate(ScheduleRequest request);
}
