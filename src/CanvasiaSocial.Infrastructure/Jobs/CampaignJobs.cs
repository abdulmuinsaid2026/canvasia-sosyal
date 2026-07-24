using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Ai;
using CanvasiaSocial.Infrastructure.Campaigns;
using CanvasiaSocial.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanvasiaSocial.Infrastructure.Jobs;

public sealed class PrepareCampaignJob(ApplicationDbContext dbContext, IBackgroundJobClient jobs)
{
    [Queue("campaign")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [15, 60, 180])]
    public async Task ExecuteAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Campaigns.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
        if (campaign is null || campaign.Status is CampaignStatus.Paused or CampaignStatus.Cancelled) return;
        campaign.Status = CampaignStatus.Preparing;

        var itemIds = campaign.Items.Select(x => x.Id).ToArray();
        var existing = await dbContext.AiGenerationJobs.Where(x => x.CampaignItemId.HasValue && itemIds.Contains(x.CampaignItemId.Value))
            .ToDictionaryAsync(x => x.CampaignItemId!.Value, cancellationToken);
        var jobIds = new List<Guid>();
        foreach (var item in campaign.Items.Where(x => x.GeneratedContentId == null && x.Status != ContentStatus.Cancelled))
        {
            if (!existing.TryGetValue(item.Id, out var generationJob))
            {
                generationJob = CampaignService.CreateGenerationJob(item);
                dbContext.AiGenerationJobs.Add(generationJob);
            }
            if (generationJob.Status is ContentStatus.Generating or ContentStatus.Failed)
            {
                generationJob.Status = ContentStatus.Generating;
                jobIds.Add(generationJob.Id);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var jobId in jobIds)
        {
            jobs.Enqueue<GenerateSocialContentJob>(job => job.ExecuteAsync(jobId, CancellationToken.None));
        }
    }
}

public sealed class GenerateSocialContentJob(
    ApplicationDbContext dbContext,
    IAiContentGenerator generator,
    ICampaignService campaignService,
    ILogger<GenerateSocialContentJob> logger)
{
    [Queue("ai")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid generationJobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.AiGenerationJobs.Include(x => x.CampaignItem).ThenInclude(x => x!.Campaign)
            .Include(x => x.ProductCache).FirstOrDefaultAsync(x => x.Id == generationJobId, cancellationToken);
        if (job?.CampaignItem is null || job.CampaignItem.GeneratedContentId.HasValue) return;
        var campaign = job.CampaignItem.Campaign;
        if (campaign.Status is CampaignStatus.Paused or CampaignStatus.Cancelled) return;

        job.AttemptCount++;
        job.StartedAtUtc = DateTime.UtcNow;
        job.Status = ContentStatus.Generating;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var product = job.ProductCache;
            var result = await generator.GenerateAsync(new AiContentRequest(product.Id, product.Title, product.CategoryName,
                product.Price, product.Description, product.PromptSummary, product.ProductUrl, campaign.Platform,
                campaign.IncludePrice, campaign.IncludeProductLink), cancellationToken);
            var status = campaign.Mode switch
            {
                CampaignMode.DraftOnly => ContentStatus.Draft,
                CampaignMode.RequireApproval => ContentStatus.AwaitingApproval,
                CampaignMode.AutoSchedule => ContentStatus.Approved,
                _ => ContentStatus.Draft
            };
            var content = SingleContentService.CreateEntity(product, campaign.Platform, campaign.CreatedByUserId, result, status);
            dbContext.GeneratedContents.Add(content);
            job.CampaignItem.GeneratedContentId = content.Id;
            job.CampaignItem.Status = status;
            job.CampaignItem.ErrorMessage = null;
            job.CampaignItem.UpdatedAtUtc = DateTime.UtcNow;
            job.Status = status;
            job.ModelName = result.ModelName;
            job.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var safeError = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
            job.Status = ContentStatus.Failed;
            job.ErrorMessage = safeError;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.CampaignItem.Status = ContentStatus.Failed;
            job.CampaignItem.ErrorMessage = safeError;
            job.CampaignItem.UpdatedAtUtc = DateTime.UtcNow;
            logger.LogError("Ürün içerik üretimi başarısız. Job: {JobId}, hata: {Error}", job.Id, safeError);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        await FinalizeCampaignAsync(campaign.Id, cancellationToken);
    }

    private async Task FinalizeCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Campaigns.Include(x => x.Items).FirstAsync(x => x.Id == campaignId, cancellationToken);
        campaign.CompletedItems = campaign.Items.Count(x => x.Status is ContentStatus.Draft or ContentStatus.AwaitingApproval or ContentStatus.Approved or ContentStatus.Scheduled);
        campaign.FailedItems = campaign.Items.Count(x => x.Status == ContentStatus.Failed);
        var finished = campaign.CompletedItems + campaign.FailedItems + campaign.Items.Count(x => x.Status == ContentStatus.Cancelled);
        if (finished >= campaign.TotalItems)
        {
            campaign.Status = campaign.FailedItems == campaign.TotalItems
                ? CampaignStatus.Failed
                : campaign.FailedItems > 0 ? CampaignStatus.PartiallyFailed : CampaignStatus.Ready;
        }
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (finished >= campaign.TotalItems && campaign.Mode == CampaignMode.AutoSchedule && campaign.CompletedItems > 0)
        {
            await campaignService.ScheduleAsync(campaign.Id, cancellationToken);
        }
    }
}

public sealed class RecoverCampaignJobsJob(ApplicationDbContext dbContext, IBackgroundJobClient jobs)
{
    [Queue("campaign")]
    [DisableConcurrentExecution(60)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var campaignIds = await dbContext.Campaigns.AsNoTracking()
            .Where(x => x.Status == CampaignStatus.Preparing)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var campaignId in campaignIds)
        {
            jobs.Enqueue<PrepareCampaignJob>(job => job.ExecuteAsync(campaignId, CancellationToken.None));
        }
    }
}
