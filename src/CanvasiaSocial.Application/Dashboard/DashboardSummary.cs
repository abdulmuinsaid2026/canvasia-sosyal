namespace CanvasiaSocial.Application.Dashboard;

public sealed record DashboardSummary(
    int SocialAccountCount,
    int ProductCount,
    int DraftContentCount,
    int ScheduledPostCount,
    int ActiveCampaignCount,
    DateTime? LastCanvasiaSyncAtUtc,
    bool IsCanvasiaApiHealthy);
