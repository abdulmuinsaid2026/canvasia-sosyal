using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Campaigns;
using CanvasiaSocial.Infrastructure.Jobs;
using CanvasiaSocial.Infrastructure.Persistence;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanvasiaSocial.IntegrationTests;

public sealed class CampaignWorkflowTests
{
    [Fact]
    public async Task Rejects_more_than_100_and_duplicate_products()
    {
        await using var db = CreateContext();
        var service = CreateService(db, new RecordingJobs());
        var tooMany = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(tooMany)));
        var duplicate = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request([duplicate, duplicate])));
    }

    [Fact]
    public async Task Pause_and_resume_persist_and_resume_enqueues_work()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 1);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();
        var service = CreateService(db, jobs);

        await service.PauseAsync(campaign.Id);
        Assert.Equal(CampaignStatus.Paused, campaign.Status);
        await service.ResumeAsync(campaign.Id);

        Assert.Equal(CampaignStatus.Preparing, campaign.Status);
        Assert.NotEmpty(jobs.Created);
    }

    [Fact]
    public async Task Scheduling_is_idempotent_and_uses_utc()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 1, CampaignMode.AutoSchedule);
        var item = campaign.Items.Single();
        var content = Content(item.ProductCacheId, campaign.Platform, ContentStatus.Approved);
        db.GeneratedContents.Add(content);
        item.GeneratedContent = content;
        item.GeneratedContentId = content.Id;
        item.Status = ContentStatus.Approved;
        await db.SaveChangesAsync();
        var service = CreateService(db, new RecordingJobs());

        await service.ScheduleAsync(campaign.Id);
        await service.ScheduleAsync(campaign.Id);

        var post = Assert.Single(await db.ScheduledPosts.ToListAsync());
        Assert.Equal(DateTimeKind.Utc, post.ScheduledAtUtc.Kind);
        Assert.Equal($"campaign:{campaign.Id}:item:{item.Id}", post.IdempotencyKey);
    }

    [Fact]
    public async Task Auto_schedule_schedules_each_success_without_waiting_for_campaign_completion()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 2, CampaignMode.AutoSchedule);
        var items = campaign.Items.OrderBy(x => x.SortOrder).ToArray();
        var firstJob = CampaignService.CreateGenerationJob(items[0]);
        var secondJob = CampaignService.CreateGenerationJob(items[1]);
        db.AiGenerationJobs.AddRange(firstJob, secondJob);
        await db.SaveChangesAsync();
        var service = CreateService(db, new RecordingJobs());
        var generator = new SelectiveGenerator(Guid.Empty);
        var job = new GenerateSocialContentJob(db, generator, service, NullLogger<GenerateSocialContentJob>.Instance);

        await job.ExecuteAsync(firstJob.Id, CancellationToken.None);

        var firstPost = Assert.Single(await db.ScheduledPosts.ToListAsync());
        Assert.Equal(items[0].Id, (await db.CampaignItems.SingleAsync(x => x.ScheduledPostId == firstPost.Id)).Id);
        Assert.Equal(ContentStatus.Scheduled, items[0].Status);
        Assert.Equal(ContentStatus.Generating, items[1].Status);

        await job.ExecuteAsync(secondJob.Id, CancellationToken.None);

        var posts = await db.ScheduledPosts.OrderBy(x => x.ScheduledAtUtc).ToListAsync();
        Assert.Equal(2, posts.Count);
        Assert.Equal(TimeSpan.FromMinutes(campaign.IntervalMinutes), posts[1].ScheduledAtUtc - posts[0].ScheduledAtUtc);
    }

    [Fact]
    public async Task Incremental_scheduling_preserves_daily_limit_across_calls()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 3, CampaignMode.AutoSchedule);
        campaign.DailyLimit = 2;
        var service = CreateService(db, new RecordingJobs());

        foreach (var item in campaign.Items.OrderBy(x => x.SortOrder))
        {
            var content = Content(item.ProductCacheId, campaign.Platform, ContentStatus.Approved);
            db.GeneratedContents.Add(content);
            item.GeneratedContent = content;
            item.GeneratedContentId = content.Id;
            item.Status = ContentStatus.Approved;
            await db.SaveChangesAsync();
            await service.ScheduleAsync(campaign.Id);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(campaign.TimeZoneId);
        var localTimes = (await db.ScheduledPosts.OrderBy(x => x.ScheduledAtUtc).ToListAsync())
            .Select(x => TimeZoneInfo.ConvertTimeFromUtc(x.ScheduledAtUtc, zone)).ToArray();
        Assert.Equal(localTimes[0].Date, localTimes[1].Date);
        Assert.Equal(localTimes[0].Date.AddDays(1), localTimes[2].Date);
        Assert.Equal(campaign.AllowedStartTime, TimeOnly.FromDateTime(localTimes[2]));
    }

    [Fact]
    public async Task Approved_unscheduled_content_stays_visible_until_scheduled()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 1);
        var item = campaign.Items.Single();
        var content = Content(item.ProductCacheId, campaign.Platform, ContentStatus.AwaitingApproval);
        db.GeneratedContents.Add(content);
        item.GeneratedContent = content;
        item.GeneratedContentId = content.Id;
        item.Status = ContentStatus.AwaitingApproval;
        await db.SaveChangesAsync();
        var campaignService = CreateService(db, new RecordingJobs());
        var drafts = new DraftService(db, campaignService);

        await drafts.ReviewAsync([content.Id], true, "reviewer");

        Assert.Contains(await drafts.GetPendingAsync(), x => x.Id == content.Id && x.Status == ContentStatus.Approved);

        await drafts.ScheduleApprovedAsync([content.Id]);

        Assert.DoesNotContain(await drafts.GetPendingAsync(), x => x.Id == content.Id);
        Assert.NotNull(item.ScheduledPostId);
        Assert.Equal(ContentStatus.Scheduled, item.Status);
    }

    [Fact]
    public async Task One_product_failure_does_not_stop_other_campaign_items()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 2);
        var campaignItems = campaign.Items.OrderBy(x => x.SortOrder).ToArray();
        var firstJob = CampaignService.CreateGenerationJob(campaignItems[0]);
        var secondJob = CampaignService.CreateGenerationJob(campaignItems[1]);
        db.AiGenerationJobs.AddRange(firstJob, secondJob);
        await db.SaveChangesAsync();
        var service = CreateService(db, new RecordingJobs());
        var generator = new SelectiveGenerator(campaignItems[0].ProductCacheId);
        var first = new GenerateSocialContentJob(db, generator, service, NullLogger<GenerateSocialContentJob>.Instance);

        await first.ExecuteAsync(firstJob.Id, CancellationToken.None);
        await first.ExecuteAsync(secondJob.Id, CancellationToken.None);

        Assert.Equal(ContentStatus.Failed, campaignItems[0].Status);
        Assert.Equal(ContentStatus.AwaitingApproval, campaignItems[1].Status);
        Assert.Equal(1, campaign.FailedItems);
        Assert.Equal(1, campaign.CompletedItems);
        Assert.Equal(CampaignStatus.PartiallyFailed, campaign.Status);
    }

    [Fact]
    public async Task Retry_failed_items_requeues_only_failed_content_generation()
    {
        await using var db = CreateContext();
        var campaign = SeedCampaign(db, 4);
        var items = campaign.Items.OrderBy(x => x.SortOrder).ToArray();
        items[0].Status = ContentStatus.Failed;
        items[0].ErrorMessage = "AI hatası";
        items[1].Status = ContentStatus.Failed;
        items[1].ErrorMessage = "AI hatası";
        items[2].Status = ContentStatus.Failed;
        items[2].ErrorMessage = "Yayın hatası";
        items[2].ScheduledPostId = Guid.NewGuid();
        items[3].Status = ContentStatus.AwaitingApproval;
        campaign.Status = CampaignStatus.PartiallyFailed;
        campaign.FailedItems = 3;
        var existingJob = CampaignService.CreateGenerationJob(items[0]);
        existingJob.Status = ContentStatus.Failed;
        existingJob.ErrorMessage = "AI hatası";
        db.AiGenerationJobs.Add(existingJob);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();
        var service = CreateService(db, jobs);

        await service.RetryFailedItemsAsync(campaign.Id);

        Assert.All(items[..2], item =>
        {
            Assert.Equal(ContentStatus.Generating, item.Status);
            Assert.Null(item.ErrorMessage);
            Assert.Equal(1, item.RetryCount);
        });
        Assert.Equal(ContentStatus.Failed, items[2].Status);
        Assert.Equal(ContentStatus.AwaitingApproval, items[3].Status);
        Assert.Equal(CampaignStatus.Preparing, campaign.Status);
        Assert.Equal(1, campaign.FailedItems);
        Assert.Equal(2, jobs.Created.Count);
        Assert.All(jobs.Created, job => Assert.Equal(typeof(GenerateSocialContentJob), job.Type));
        Assert.Equal(2, await db.AiGenerationJobs.CountAsync());
    }

    [Fact]
    public async Task Recovery_requeues_preparing_campaign_after_restart()
    {
        await using var db = CreateContext();
        SeedCampaign(db, 1);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();

        await new RecoverCampaignJobsJob(db, jobs).ExecuteAsync(CancellationToken.None);

        Assert.NotEmpty(jobs.Created);
    }

    [Fact]
    public void Auto_publish_is_disabled_by_default()
    {
        Assert.False(new CampaignOptions().AutoPublishEnabled);
    }

    [Fact]
    public async Task Manual_publish_requires_global_publish_switch()
    {
        await using var db = CreateContext();
        var post = new ScheduledPost
        {
            GeneratedContentId = Guid.NewGuid(), Platform = Platform.Instagram, Status = ContentStatus.Scheduled,
            ScheduledAtUtc = DateTime.UtcNow.AddDays(1), IdempotencyKey = "manual-disabled", CreatedByUserId = "tester"
        };
        db.ScheduledPosts.Add(post);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();
        var service = new CalendarService(db, jobs, new CampaignOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishNowAsync(post.Id));

        Assert.Empty(jobs.Created);
        Assert.True(post.ScheduledAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Manual_publish_makes_post_due_and_enqueues_only_that_post()
    {
        await using var db = CreateContext();
        var post = new ScheduledPost
        {
            GeneratedContentId = Guid.NewGuid(), Platform = Platform.Instagram, Status = ContentStatus.Scheduled,
            ScheduledAtUtc = DateTime.UtcNow.AddDays(1), IdempotencyKey = "manual-enabled", CreatedByUserId = "tester"
        };
        db.ScheduledPosts.Add(post);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();
        var service = new CalendarService(db, jobs, new CampaignOptions { AutoPublishEnabled = true });

        await service.PublishNowAsync(post.Id);

        Assert.True(post.ScheduledAtUtc <= DateTime.UtcNow);
        var job = Assert.Single(jobs.Created);
        Assert.Equal(typeof(PublishScheduledPostJob), job.Type);
        Assert.Equal(post.Id, job.Args[0]);
    }

    [Fact]
    public async Task Failed_publish_can_be_explicitly_requeued()
    {
        await using var db = CreateContext();
        var content = Content(Guid.NewGuid(), Platform.Instagram, ContentStatus.Failed);
        var post = new ScheduledPost
        {
            GeneratedContent = content, GeneratedContentId = content.Id, Platform = Platform.Instagram,
            Status = ContentStatus.Failed, ScheduledAtUtc = DateTime.UtcNow.AddHours(-1),
            IdempotencyKey = "manual-retry", CreatedByUserId = "tester", AttemptCount = 3,
            LastErrorCode = "ERROR", LastErrorMessage = "Instagram görseli işleyemedi."
        };
        db.AddRange(content, post);
        await db.SaveChangesAsync();
        var jobs = new RecordingJobs();
        var service = new CalendarService(db, jobs, new CampaignOptions { AutoPublishEnabled = true });

        await service.RetryPublishAsync(post.Id);

        Assert.Equal(ContentStatus.Scheduled, post.Status);
        Assert.Equal(ContentStatus.Scheduled, content.Status);
        Assert.Null(post.LastErrorCode);
        Assert.Null(post.LastErrorMessage);
        Assert.True(post.ScheduledAtUtc <= DateTime.UtcNow);
        var job = Assert.Single(jobs.Created);
        Assert.Equal(typeof(PublishScheduledPostJob), job.Type);
        Assert.Equal(post.Id, job.Args[0]);
    }

    private static CampaignService CreateService(ApplicationDbContext db, RecordingJobs jobs) =>
        new(db, jobs, new ScheduleCalculator(), new CampaignOptions());

    private static CreateCampaignRequest Request(IReadOnlyCollection<Guid> ids) => new(
        "Test", Platform.Instagram, null, CampaignMode.RequireApproval, new DateTime(2026, 7, 22, 9, 0, 0),
        60, 10, new TimeOnly(9, 0), new TimeOnly(21, 0), true, true, ids, "tester");

    private static Campaign SeedCampaign(ApplicationDbContext db, int productCount, CampaignMode mode = CampaignMode.RequireApproval)
    {
        var campaign = new Campaign
        {
            Name = "Test kampanyası", Platform = Platform.Instagram, Mode = mode, Status = CampaignStatus.Preparing,
            StartAtUtc = new DateTime(2026, 7, 22, 6, 0, 0, DateTimeKind.Utc), IntervalMinutes = 60, DailyLimit = 10,
            AllowedStartTime = new TimeOnly(9, 0), AllowedEndTime = new TimeOnly(21, 0), TimeZoneId = "Europe/Istanbul",
            TotalItems = productCount, CreatedByUserId = "tester"
        };
        for (var index = 0; index < productCount; index++)
        {
            var product = new ProductCache { CanvasiaProductId = index + 1, Title = $"Ürün {index + 1}", Slug = $"urun-{index + 1}", Price = 100, ProductUrl = "https://example.test", RawJson = "{}" };
            db.ProductCaches.Add(product);
            campaign.Items.Add(new CampaignItem { ProductCache = product, ProductCacheId = product.Id, Campaign = campaign, CampaignId = campaign.Id, SortOrder = index, Status = ContentStatus.Generating });
        }
        db.Campaigns.Add(campaign);
        return campaign;
    }

    private static GeneratedContent Content(Guid productId, Platform platform, ContentStatus status) => new()
    {
        ProductCacheId = productId, Platform = platform, Caption = "İçerik", HashtagsJson = "[]", Language = "tr",
        Tone = "test", ModelName = "test", PromptVersion = "v1", PromptHash = "hash", Status = status, CreatedByUserId = "tester"
    };

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class SelectiveGenerator(Guid failingProductId) : IAiContentGenerator
    {
        public Task<AiContentResult> GenerateAsync(AiContentRequest request, CancellationToken cancellationToken = default) =>
            request.ProductId == failingProductId
                ? throw new HttpRequestException("Simüle ürün hatası")
                : Task.FromResult(new AiContentResult("Caption", null, null, ["test"], "test-model", "{}"));
    }

    private sealed class RecordingJobs : IBackgroundJobClient
    {
        public List<Job> Created { get; } = [];
        public string Create(Job job, IState state) { Created.Add(job); return Guid.NewGuid().ToString(); }
        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }
}
