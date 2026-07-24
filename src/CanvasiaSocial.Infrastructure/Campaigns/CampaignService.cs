using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Jobs;
using CanvasiaSocial.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Campaigns;

public sealed class CampaignService(
    ApplicationDbContext dbContext,
    IBackgroundJobClient jobs,
    IScheduleCalculator scheduleCalculator,
    CampaignOptions options) : ICampaignService
{
    public async Task<Guid> CreateAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var productIds = request.ProductIds.Distinct().ToArray();
        if (productIds.Length == 0 || productIds.Length > options.BatchMaxProducts || productIds.Length != request.ProductIds.Count)
        {
            throw new InvalidOperationException($"Kampanya 1-{options.BatchMaxProducts} benzersiz ürün içermelidir.");
        }
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Kampanya adı zorunludur.");
        if (request.AllowedStartTime >= request.AllowedEndTime) throw new InvalidOperationException("İzin verilen saat aralığı geçersiz.");
        var products = await dbContext.ProductCaches.AsNoTracking().Where(x => productIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (products.Count != productIds.Length) throw new InvalidOperationException("Seçilen ürünlerden biri cache içinde bulunamadı.");

        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.DefaultTimeZone);
        var startLocal = DateTime.SpecifyKind(request.StartLocal, DateTimeKind.Unspecified);
        var campaign = new Campaign
        {
            Name = request.Name.Trim(),
            Platform = request.Platform,
            SocialAccountId = request.SocialAccountId,
            Mode = request.Mode,
            Status = CampaignStatus.Preparing,
            StartAtUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, zone),
            IntervalMinutes = Math.Max(1, request.IntervalMinutes),
            DailyLimit = Math.Min(Math.Max(1, request.DailyLimit), options.GetPlatformDailyLimit(request.Platform)),
            AllowedStartTime = request.AllowedStartTime,
            AllowedEndTime = request.AllowedEndTime,
            TimeZoneId = options.DefaultTimeZone,
            RequireApproval = request.Mode == CampaignMode.RequireApproval,
            IncludePrice = request.IncludePrice,
            IncludeProductLink = request.IncludeProductLink,
            TotalItems = productIds.Length,
            CreatedByUserId = request.CreatedByUserId
        };
        campaign.Items = productIds.Select((id, index) => new CampaignItem
        {
            ProductCacheId = id,
            SortOrder = index,
            Status = ContentStatus.Generating
        }).ToList();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<PrepareCampaignJob>(job => job.ExecuteAsync(campaign.Id, CancellationToken.None));
        return campaign.Id;
    }

    public async Task<IReadOnlyList<CampaignListItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Campaigns.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CampaignListItem(x.Id, x.Name, x.Platform, x.Mode, x.Status, x.TotalItems,
                x.CompletedItems, x.FailedItems, x.StartAtUtc, x.CreatedAtUtc)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SocialAccountOption>> GetSocialAccountsAsync(Platform platform, CancellationToken cancellationToken = default) =>
        await dbContext.SocialAccounts.AsNoTracking().Where(x => x.Platform == platform && x.Status == "Active")
            .OrderBy(x => x.DisplayName).Select(x => new SocialAccountOption(x.Id, x.Platform, x.DisplayName)).ToListAsync(cancellationToken);

    public async Task<CampaignDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.ProductCache)
            .Include(x => x.Items).ThenInclude(x => x.GeneratedContent)
            .Include(x => x.Items).ThenInclude(x => x.ScheduledPost)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (campaign is null) return null;
        return new CampaignDetails(campaign.Id, campaign.Name, campaign.Platform, campaign.Mode, campaign.Status,
            campaign.TotalItems, campaign.CompletedItems, campaign.FailedItems, campaign.StartAtUtc, campaign.TimeZoneId,
            campaign.Items.OrderBy(x => x.SortOrder).Select(x => new CampaignItemDetails(x.Id, x.ProductCacheId,
                x.ProductCache.Title, x.Status, x.GeneratedContent?.Caption, x.ErrorMessage, x.ScheduledPost?.ScheduledAtUtc)).ToArray());
    }

    public Task PauseAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, CampaignStatus.Paused, cancellationToken);

    public async Task ResumeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await GetCampaignAsync(id, cancellationToken);
        if (campaign.Status != CampaignStatus.Paused) return;
        campaign.Status = CampaignStatus.Preparing;
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<PrepareCampaignJob>(job => job.ExecuteAsync(id, CancellationToken.None));
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.Include(x => x.Items).ThenInclude(x => x.ScheduledPost)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("Kampanya bulunamadı.");
        campaign.Status = CampaignStatus.Cancelled;
        foreach (var item in campaign.Items.Where(x => x.Status is not ContentStatus.Published))
        {
            item.Status = ContentStatus.Cancelled;
            if (item.ScheduledPost is not null) item.ScheduledPost.Status = ContentStatus.Cancelled;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.CampaignItems.Include(x => x.Campaign).FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new InvalidOperationException("Kampanya ürünü bulunamadı.");
        if (item.Campaign.Status == CampaignStatus.Cancelled) throw new InvalidOperationException("İptal edilmiş kampanya yeniden denenemez.");
        item.Status = ContentStatus.Generating;
        item.ErrorMessage = null;
        item.RetryCount++;
        item.Campaign.Status = CampaignStatus.Preparing;
        var generationJob = await dbContext.AiGenerationJobs.FirstOrDefaultAsync(x => x.CampaignItemId == itemId, cancellationToken);
        if (generationJob is null)
        {
            generationJob = CreateGenerationJob(item);
            dbContext.AiGenerationJobs.Add(generationJob);
        }
        else
        {
            generationJob.Status = ContentStatus.Generating;
            generationJob.ErrorMessage = null;
            generationJob.CompletedAtUtc = null;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<GenerateSocialContentJob>(job => job.ExecuteAsync(generationJob.Id, CancellationToken.None));
    }

    public async Task ApproveAsync(Guid campaignId, string userId, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.Include(x => x.Items).ThenInclude(x => x.GeneratedContent)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken) ?? throw new InvalidOperationException("Kampanya bulunamadı.");
        foreach (var content in campaign.Items.Select(x => x.GeneratedContent).Where(x => x is not null && x.Status is ContentStatus.Draft or ContentStatus.AwaitingApproval))
        {
            content!.Status = ContentStatus.Approved;
            content.ReviewedByUserId = userId;
            content.ReviewedAtUtc = DateTime.UtcNow;
        }
        campaign.Status = CampaignStatus.Ready;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ScheduleAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.Include(x => x.Items).ThenInclude(x => x.GeneratedContent)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken) ?? throw new InvalidOperationException("Kampanya bulunamadı.");
        if (campaign.Status == CampaignStatus.Cancelled) return;
        var items = campaign.Items.Where(x => x.GeneratedContent is not null && x.GeneratedContent.Status == ContentStatus.Approved && x.ScheduledPostId == null)
            .OrderBy(x => x.SortOrder).ToList();
        if (items.Count == 0) return;

        var previouslyPublished = await dbContext.ProductPublicationHistories.AsNoTracking()
            .Where(x => x.Platform == campaign.Platform && items.Select(i => i.ProductCacheId).Contains(x.ProductCacheId))
            .Select(x => x.ProductCacheId).Distinct().ToListAsync(cancellationToken);
        foreach (var skipped in items.Where(x => previouslyPublished.Contains(x.ProductCacheId)).ToList())
        {
            skipped.Status = ContentStatus.Cancelled;
            skipped.ErrorMessage = "Ürün bu platformda daha önce yayımlandı.";
            items.Remove(skipped);
        }

        var startUtc = campaign.StartAtUtc ?? DateTime.UtcNow;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(campaign.TimeZoneId);
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), zone);
        var times = scheduleCalculator.Calculate(new ScheduleRequest(startLocal, campaign.TimeZoneId,
            campaign.AllowedStartTime ?? new TimeOnly(9, 0), campaign.AllowedEndTime ?? new TimeOnly(21, 0),
            Math.Max(1, campaign.IntervalMinutes), Math.Max(1, campaign.DailyLimit ?? options.GetPlatformDailyLimit(campaign.Platform)), items.Count));

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var key = $"campaign:{campaign.Id}:item:{item.Id}";
            var existing = await dbContext.ScheduledPosts.FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
            if (existing is not null)
            {
                item.ScheduledPostId = existing.Id;
                item.Status = ContentStatus.Scheduled;
                continue;
            }
            var post = new ScheduledPost
            {
                SocialAccountId = campaign.SocialAccountId,
                GeneratedContentId = item.GeneratedContentId!.Value,
                Platform = campaign.Platform,
                Status = ContentStatus.Scheduled,
                ScheduledAtUtc = times[index],
                IdempotencyKey = key,
                CreatedByUserId = campaign.CreatedByUserId
            };
            dbContext.ScheduledPosts.Add(post);
            item.ScheduledPostId = post.Id;
            item.Status = ContentStatus.Scheduled;
        }
        campaign.Status = CampaignStatus.Active;
        campaign.EndAtUtc = times.LastOrDefault();
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static AiGenerationJob CreateGenerationJob(CampaignItem item) => new()
    {
        CampaignId = item.CampaignId,
        CampaignItemId = item.Id,
        ProductCacheId = item.ProductCacheId,
        Platform = item.Campaign.Platform,
        Status = ContentStatus.Generating
    };

    private async Task ChangeStatusAsync(Guid id, CampaignStatus status, CancellationToken cancellationToken)
    {
        var campaign = await GetCampaignAsync(id, cancellationToken);
        if (campaign.Status == CampaignStatus.Cancelled) return;
        campaign.Status = status;
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Campaign> GetCampaignAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("Kampanya bulunamadı.");
}
