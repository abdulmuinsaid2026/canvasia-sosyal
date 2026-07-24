using CanvasiaSocial.Application.Dashboard;
using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Application.Synchronization;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Dashboard;

internal sealed class DashboardService(
    ApplicationDbContext dbContext,
    ICanvasiaApiClient canvasiaApiClient,
    ICanvasiaProductSyncService syncService) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var socialAccountCount = await dbContext.SocialAccounts.CountAsync(cancellationToken);
        var productCount = await dbContext.ProductCaches.CountAsync(cancellationToken);
        var draftContentCount = await dbContext.GeneratedContents
            .CountAsync(x => x.Status == ContentStatus.Draft, cancellationToken);
        var scheduledPostCount = await dbContext.ScheduledPosts
            .CountAsync(x => x.Status == ContentStatus.Scheduled, cancellationToken);
        var activeCampaignCount = await dbContext.Campaigns
            .CountAsync(x => x.Status == CampaignStatus.Active, cancellationToken);
        var syncStatus = await syncService.GetStatusAsync(cancellationToken);
        var connection = await canvasiaApiClient.TestConnectionAsync(cancellationToken);

        return new DashboardSummary(
            socialAccountCount,
            productCount,
            draftContentCount,
            scheduledPostCount,
            activeCampaignCount,
            syncStatus.LastSuccessfulAtUtc,
            connection.IsHealthy);
    }
}
