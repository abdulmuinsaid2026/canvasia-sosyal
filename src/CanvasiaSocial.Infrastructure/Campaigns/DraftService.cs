using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Campaigns;

public sealed class DraftService(ApplicationDbContext dbContext, ICampaignService campaignService) : IDraftService
{
    public async Task<IReadOnlyList<GeneratedContentView>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var contents = await dbContext.GeneratedContents.AsNoTracking()
            .Where(x => x.Status == ContentStatus.Draft || x.Status == ContentStatus.AwaitingApproval ||
                x.Status == ContentStatus.Approved && dbContext.CampaignItems.Any(item =>
                    item.GeneratedContentId == x.Id && item.ScheduledPostId == null))
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return contents.Select(x => new GeneratedContentView(x.Id, x.ProductCacheId, x.Platform, x.Caption,
            x.StoryText, x.CallToAction, System.Text.Json.JsonSerializer.Deserialize<string[]>(x.HashtagsJson) ?? [],
            x.Status, x.CreatedAtUtc)).ToArray();
    }

    public async Task ReviewAsync(IReadOnlyCollection<Guid> contentIds, bool approve, string userId, CancellationToken cancellationToken = default)
    {
        var distinctIds = contentIds.Distinct().ToArray();
        var contents = await dbContext.GeneratedContents.Where(x => distinctIds.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var content in contents.Where(x => x.Status is ContentStatus.Draft or ContentStatus.AwaitingApproval))
        {
            content.Status = approve ? ContentStatus.Approved : ContentStatus.Rejected;
            content.ReviewedByUserId = userId;
            content.ReviewedAtUtc = DateTime.UtcNow;
            content.UpdatedAtUtc = DateTime.UtcNow;
        }
        var itemByContent = await dbContext.CampaignItems.Where(x => x.GeneratedContentId.HasValue && distinctIds.Contains(x.GeneratedContentId.Value)).ToListAsync(cancellationToken);
        foreach (var item in itemByContent) item.Status = approve ? ContentStatus.Approved : ContentStatus.Rejected;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ScheduleApprovedAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        var ids = contentIds.Distinct().ToArray();
        var campaignIds = await dbContext.CampaignItems.AsNoTracking()
            .Where(x => x.GeneratedContentId.HasValue && ids.Contains(x.GeneratedContentId.Value) && x.Status == ContentStatus.Approved)
            .Select(x => x.CampaignId).Distinct().ToListAsync(cancellationToken);
        foreach (var campaignId in campaignIds) await campaignService.ScheduleAsync(campaignId, cancellationToken);
    }
}
