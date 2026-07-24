using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Campaigns;
using CanvasiaSocial.Infrastructure.Jobs;
using CanvasiaSocial.Infrastructure.Persistence;
using CanvasiaSocial.Infrastructure.Social;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanvasiaSocial.IntegrationTests;

public sealed class PublishScheduledPostJobTests
{
    [Fact]
    public async Task Auto_publish_disabled_makes_no_provider_call()
    {
        var fixture = await Fixture.CreateAsync(new CampaignOptions { AutoPublishEnabled = false });

        await fixture.Job.ExecuteAsync(fixture.Post.Id, CancellationToken.None);

        Assert.Equal(0, fixture.Provider.PublishCalls);
        Assert.Equal(ContentStatus.Scheduled, fixture.Post.Status);
        Assert.Empty(fixture.Db.PublishAttempts);
    }

    [Fact]
    public async Task Success_is_persisted_and_idempotent()
    {
        var fixture = await Fixture.CreateAsync(new CampaignOptions { AutoPublishEnabled = true });

        await fixture.Job.ExecuteAsync(fixture.Post.Id, CancellationToken.None);
        await fixture.Job.ExecuteAsync(fixture.Post.Id, CancellationToken.None);

        Assert.Equal(1, fixture.Provider.PublishCalls);
        Assert.Equal(ContentStatus.Published, fixture.Post.Status);
        Assert.Equal("external-post", fixture.Post.ExternalPostId);
        Assert.Single(fixture.Db.ProductPublicationHistories);
        Assert.Single(fixture.Db.PublishAttempts);
    }

    [Fact]
    public async Task Unauthorized_refreshes_once_then_publishes()
    {
        var fixture = await Fixture.CreateAsync(new CampaignOptions { AutoPublishEnabled = true });
        fixture.Provider.Failures.Enqueue(new SocialPublisherException("expired", SocialPublishFailureKind.Unauthorized, 401));

        await fixture.Job.ExecuteAsync(fixture.Post.Id, CancellationToken.None);

        Assert.Equal(1, fixture.Provider.RefreshCalls);
        Assert.Equal(2, fixture.Provider.PublishCalls);
        Assert.Equal(ContentStatus.Published, fixture.Post.Status);
    }

    [Theory]
    [InlineData(SocialPublishFailureKind.Forbidden, 403, false)]
    [InlineData(SocialPublishFailureKind.InvalidContent, 400, false)]
    [InlineData(SocialPublishFailureKind.RateLimited, 429, true)]
    [InlineData(SocialPublishFailureKind.Transient, 500, true)]
    public async Task Failure_classification_controls_retry(
        SocialPublishFailureKind kind,
        int status,
        bool shouldRetry)
    {
        var fixture = await Fixture.CreateAsync(new CampaignOptions { AutoPublishEnabled = true, PublishMaxRetryCount = 3 });
        fixture.Provider.Failures.Enqueue(new SocialPublisherException("provider failure", kind, status,
            retryAfter: kind == SocialPublishFailureKind.RateLimited ? TimeSpan.FromMinutes(4) : null));

        await fixture.Job.ExecuteAsync(fixture.Post.Id, CancellationToken.None);

        Assert.Equal(shouldRetry ? ContentStatus.Scheduled : ContentStatus.Failed, fixture.Post.Status);
        Assert.Equal(shouldRetry, fixture.Post.NextRetryAtUtc.HasValue);
        var attempt = Assert.Single(fixture.Db.PublishAttempts);
        Assert.False(attempt.Success);
        Assert.Equal(status, attempt.HttpStatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, FakePublisher provider, ScheduledPost post, PublishScheduledPostJob job)
        {
            Db = db; Provider = provider; Post = post; Job = job;
        }
        public ApplicationDbContext Db { get; }
        public FakePublisher Provider { get; }
        public ScheduledPost Post { get; }
        public PublishScheduledPostJob Job { get; }

        public static async Task<Fixture> CreateAsync(CampaignOptions options)
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var protector = new SocialTokenProtector(DataProtectionProvider.Create(
                new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"canvasia-publish-{Guid.NewGuid():N}"))));
            var account = new SocialAccount
            {
                Platform = Platform.Instagram, DisplayName = "Test", ExternalAccountId = "ig-1",
                EncryptedAccessToken = protector.Protect("access-token"), EncryptedRefreshToken = protector.Protect("refresh-token"),
                Scopes = "instagram_business_content_publish", Status = "Active"
            };
            var product = new ProductCache
            {
                CanvasiaProductId = 1, Title = "Product", Slug = "product", Price = 1,
                ProductUrl = "https://www.canvasia.com.tr/product", RawJson = "{}"
            };
            product.Images.Add(new ProductImage { ProductCache = product, ProductCacheId = product.Id, Url = "https://www.canvasia.com.tr/image.jpg", IsPrimary = true });
            var content = new GeneratedContent
            {
                ProductCache = product, ProductCacheId = product.Id, Platform = Platform.Instagram, Caption = "Caption",
                HashtagsJson = "[\"canvasia\"]", Language = "tr", Tone = "test", ModelName = "test",
                PromptVersion = "v1", PromptHash = "hash", Status = ContentStatus.Scheduled, CreatedByUserId = "admin"
            };
            var post = new ScheduledPost
            {
                SocialAccount = account, SocialAccountId = account.Id, GeneratedContent = content, GeneratedContentId = content.Id,
                Platform = Platform.Instagram, Status = ContentStatus.Scheduled, ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1),
                IdempotencyKey = Guid.NewGuid().ToString(), CreatedByUserId = "admin"
            };
            db.AddRange(account, product, content, post);
            await db.SaveChangesAsync();
            var provider = new FakePublisher();
            ISocialPublisher[] providers = [provider, new DisabledPublisher(Platform.Facebook), new DisabledPublisher(Platform.TikTok), new DisabledPublisher(Platform.Pinterest)];
            var job = new PublishScheduledPostJob(db, providers, protector, new FakeImageService(), options,
                NullLogger<PublishScheduledPostJob>.Instance);
            return new Fixture(db, provider, post, job);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakePublisher : ISocialPublisher
    {
        public Queue<SocialPublisherException> Failures { get; } = new();
        public int PublishCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public Platform Platform => Platform.Instagram;
        public SocialProviderConfiguration Configuration { get; } = new(Platform.Instagram, true, true, "https://example.test/callback", ["publish"]);
        public Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.FromResult(new SocialValidationResult(true));
        public Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default) => Task.FromResult(new SocialValidationResult(true));
        public Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            if (Failures.TryDequeue(out var failure)) throw failure;
            return Task.FromResult(new SocialPublishResult("external-post", "https://example.test/post", "{\"id\":\"external-post\"}"));
        }
        public Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(new SocialRefreshResult("refreshed-token", "new-refresh-token", DateTime.UtcNow.AddHours(1), account.Scopes));
        }
        public Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeImageService : ISecureImageService
    {
        public Task<Uri> ValidateAndPrepareAsync(string imageUrl, CancellationToken cancellationToken = default) => Task.FromResult(new Uri(imageUrl));
    }

    private sealed class DisabledPublisher(Platform platform) : ISocialPublisher
    {
        public Platform Platform { get; } = platform;
        public SocialProviderConfiguration Configuration { get; } = new(platform, false, false, "", []);
        public Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
