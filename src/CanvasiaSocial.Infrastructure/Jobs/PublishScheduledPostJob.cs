using System.Text.Json;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Campaigns;
using CanvasiaSocial.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanvasiaSocial.Infrastructure.Jobs;

public sealed class DispatchDuePostsJob(
    ApplicationDbContext dbContext,
    IBackgroundJobClient jobs,
    CampaignOptions options)
{
    [Queue("publish")]
    [DisableConcurrentExecution(55)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!options.AutoPublishEnabled) return;
        var now = DateTime.UtcNow;
        var ids = await dbContext.ScheduledPosts.AsNoTracking()
            .Where(x => x.Status == ContentStatus.Scheduled && x.ScheduledAtUtc <= now &&
                        (!x.NextRetryAtUtc.HasValue || x.NextRetryAtUtc <= now))
            .OrderBy(x => x.ScheduledAtUtc).Select(x => x.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            jobs.Enqueue<PublishScheduledPostJob>(job => job.ExecuteAsync(id, CancellationToken.None));
        }
    }
}

public sealed class PublishScheduledPostJob(
    ApplicationDbContext dbContext,
    IEnumerable<ISocialPublisher> publishers,
    ISocialTokenProtector tokenProtector,
    ISecureImageService imageService,
    CampaignOptions options,
    ILogger<PublishScheduledPostJob> logger)
{
    private readonly IReadOnlyDictionary<Platform, ISocialPublisher> providers = publishers.ToDictionary(x => x.Platform);

    [Queue("publish")]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(300)]
    public async Task ExecuteAsync(Guid scheduledPostId, CancellationToken cancellationToken)
    {
        if (!options.AutoPublishEnabled) return;
        var post = await dbContext.ScheduledPosts
            .Include(x => x.SocialAccount)
            .Include(x => x.GeneratedContent).ThenInclude(x => x.ProductCache).ThenInclude(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == scheduledPostId, cancellationToken);
        if (post is null || post.Status is ContentStatus.Published or ContentStatus.Cancelled ||
            post.Status != ContentStatus.Scheduled || post.ScheduledAtUtc > DateTime.UtcNow ||
            post.NextRetryAtUtc > DateTime.UtcNow) return;

        var existingHistory = await dbContext.ProductPublicationHistories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ScheduledPostId == post.Id, cancellationToken);
        if (existingHistory is not null)
        {
            post.Status = ContentStatus.Published;
            post.PublishedAtUtc = existingHistory.PublishedAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        post.AttemptCount++;
        post.LastAttemptAtUtc = DateTime.UtcNow;
        post.NextRetryAtUtc = null;
        post.Status = ContentStatus.Publishing;
        var attempt = new PublishAttempt
        {
            ScheduledPostId = post.Id,
            AttemptNumber = post.AttemptCount,
            StartedAtUtc = DateTime.UtcNow,
            SanitizedRequest = JsonSerializer.Serialize(new
            {
                platform = post.Platform.ToString(),
                captionLength = post.GeneratedContent.Caption.Length,
                post.IdempotencyKey
            })
        };
        dbContext.PublishAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            if (post.SocialAccount is null || post.SocialAccount.Platform != post.Platform || post.SocialAccount.Status != "Active")
                throw new SocialPublisherException("Yayın için etkin ve platformla eşleşen bir sosyal hesap seçilmelidir.", SocialPublishFailureKind.InvalidContent);
            var provider = providers[post.Platform];
            if (!provider.Configuration.IsConfigured)
                throw new SocialPublisherException("Sosyal medya provider yapılandırılmamış.", SocialPublishFailureKind.Permanent);

            var image = post.GeneratedContent.ProductCache.Images.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SortOrder).FirstOrDefault()
                ?? throw new SocialPublisherException("Üründe yayımlanabilir görsel bulunamadı.", SocialPublishFailureKind.InvalidContent);
            var safeImageUrl = await imageService.ValidateAndPrepareAsync(image.Url, cancellationToken);
            var caption = BuildCaption(post.GeneratedContent);
            var publishRequest = new SocialPostRequest(caption, safeImageUrl, post.IdempotencyKey);
            var contentValidation = await provider.ValidatePostAsync(publishRequest, cancellationToken);
            if (!contentValidation.IsValid)
                throw new SocialPublisherException(contentValidation.Error ?? "İçerik platform kurallarına uygun değil.", SocialPublishFailureKind.InvalidContent);

            var credentials = Credentials(post.SocialAccount);
            if (credentials.TokenExpiresAtUtc <= DateTime.UtcNow.AddMinutes(5))
            {
                credentials = await RefreshAsync(provider, post.SocialAccount, credentials, cancellationToken);
            }
            var accountValidation = await provider.ValidateAccountAsync(credentials, cancellationToken);
            if (!accountValidation.IsValid)
            {
                credentials = await RefreshAsync(provider, post.SocialAccount, credentials, cancellationToken);
            }

            SocialPublishResult result;
            try
            {
                result = await provider.PublishAsync(credentials, publishRequest, cancellationToken);
            }
            catch (SocialPublisherException exception) when (exception.Kind == SocialPublishFailureKind.Unauthorized)
            {
                credentials = await RefreshAsync(provider, post.SocialAccount, credentials, cancellationToken);
                result = await provider.PublishAsync(credentials, publishRequest, cancellationToken);
            }
            if (string.IsNullOrWhiteSpace(result.ExternalPostId))
                throw new SocialPublisherException("Provider gerçek yayın kimliği döndürmedi.", SocialPublishFailureKind.Transient);

            var now = DateTime.UtcNow;
            post.Status = ContentStatus.Published;
            post.PublishedAtUtc = now;
            post.ExternalPostId = result.ExternalPostId;
            post.ExternalPostUrl = result.ExternalPostUrl;
            post.LastErrorCode = null;
            post.LastErrorMessage = null;
            post.UpdatedAtUtc = now;
            post.GeneratedContent.Status = ContentStatus.Published;
            post.GeneratedContent.UpdatedAtUtc = now;
            attempt.Success = true;
            attempt.CompletedAtUtc = now;
            attempt.SanitizedResponse = result.SanitizedResponse;
            dbContext.ProductPublicationHistories.Add(new ProductPublicationHistory
            {
                ProductCacheId = post.GeneratedContent.ProductCacheId,
                Platform = post.Platform,
                SocialAccountId = post.SocialAccount.Id,
                ScheduledPostId = post.Id,
                PublishedAtUtc = now
            });
            await UpdateCampaignItemAsync(post.Id, ContentStatus.Published, null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (SocialPublisherException exception)
        {
            await RecordFailureAsync(post, attempt, exception, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Security.Cryptography.CryptographicException)
        {
            await RecordFailureAsync(post, attempt,
                new SocialPublisherException("Sosyal medya servisine güvenli bağlantı kurulamadı.", SocialPublishFailureKind.Transient, innerException: exception),
                cancellationToken);
        }
    }

    private async Task<SocialAccountCredentials> RefreshAsync(
        ISocialPublisher provider,
        SocialAccount account,
        SocialAccountCredentials credentials,
        CancellationToken cancellationToken)
    {
        var refreshed = await provider.RefreshTokenAsync(credentials, cancellationToken);
        account.EncryptedAccessToken = tokenProtector.Protect(refreshed.AccessToken);
        if (refreshed.RefreshToken is not null) account.EncryptedRefreshToken = tokenProtector.Protect(refreshed.RefreshToken);
        account.TokenExpiresAtUtc = refreshed.TokenExpiresAtUtc;
        account.Scopes = string.Join(' ', refreshed.Scopes);
        account.LastValidatedAtUtc = DateTime.UtcNow;
        account.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SocialAccountCredentials(account.ExternalAccountId, refreshed.AccessToken, refreshed.RefreshToken ?? credentials.RefreshToken,
            refreshed.TokenExpiresAtUtc, refreshed.Scopes);
    }

    private async Task RecordFailureAsync(
        ScheduledPost post,
        PublishAttempt attempt,
        SocialPublisherException exception,
        CancellationToken cancellationToken)
    {
        var retryable = exception.Kind is SocialPublishFailureKind.RateLimited or SocialPublishFailureKind.Transient;
        var canRetry = retryable && post.AttemptCount < options.PublishMaxRetryCount;
        post.Status = canRetry ? ContentStatus.Scheduled : ContentStatus.Failed;
        post.NextRetryAtUtc = canRetry ? DateTime.UtcNow.Add(exception.RetryAfter ?? Backoff(post.AttemptCount)) : null;
        post.LastErrorCode = exception.ProviderErrorCode ?? exception.Kind.ToString();
        post.LastErrorMessage = Limit(exception.Message, 2000);
        post.UpdatedAtUtc = DateTime.UtcNow;
        attempt.Success = false;
        attempt.CompletedAtUtc = DateTime.UtcNow;
        attempt.HttpStatusCode = exception.HttpStatusCode;
        attempt.PlatformErrorCode = exception.ProviderErrorCode;
        attempt.SanitizedResponse = exception.SanitizedResponse;
        attempt.ErrorMessage = Limit(exception.Message, 2000);
        if (!canRetry)
        {
            post.GeneratedContent.Status = ContentStatus.Failed;
            await UpdateCampaignItemAsync(post.Id, ContentStatus.Failed, post.LastErrorMessage, cancellationToken);
        }
        if (exception.Kind == SocialPublishFailureKind.Unauthorized && post.SocialAccount is not null)
            post.SocialAccount.Status = "ReconnectRequired";
        logger.LogWarning("Sosyal yayın başarısız. Post: {PostId}, tür: {FailureKind}, tekrar: {WillRetry}",
            post.Id, exception.Kind, canRetry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateCampaignItemAsync(Guid postId, ContentStatus status, string? error, CancellationToken cancellationToken)
    {
        var item = await dbContext.CampaignItems.Include(x => x.Campaign)
            .FirstOrDefaultAsync(x => x.ScheduledPostId == postId, cancellationToken);
        if (item is null) return;
        item.Status = status;
        item.ErrorMessage = error;
        item.UpdatedAtUtc = DateTime.UtcNow;
        var campaign = item.Campaign;
        var statuses = await dbContext.CampaignItems.Where(x => x.CampaignId == campaign.Id)
            .Select(x => new { x.Id, x.Status }).ToListAsync(cancellationToken);
        var effectiveStatuses = statuses.Select(x => x.Id == item.Id ? status : x.Status).ToList();
        if (effectiveStatuses.All(x => x is ContentStatus.Published or ContentStatus.Failed or ContentStatus.Cancelled))
        {
            campaign.Status = effectiveStatuses.Any(x => x == ContentStatus.Failed) ? CampaignStatus.PartiallyFailed : CampaignStatus.Completed;
            campaign.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private SocialAccountCredentials Credentials(SocialAccount account) => new(account.ExternalAccountId,
        tokenProtector.Unprotect(account.EncryptedAccessToken),
        account.EncryptedRefreshToken is null ? null : tokenProtector.Unprotect(account.EncryptedRefreshToken),
        account.TokenExpiresAtUtc,
        string.IsNullOrWhiteSpace(account.Scopes) ? [] : account.Scopes.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries));
    private static string BuildCaption(GeneratedContent content)
    {
        var hashtags = JsonSerializer.Deserialize<string[]>(content.HashtagsJson) ?? [];
        return hashtags.Length == 0 ? content.Caption : $"{content.Caption}\n\n{string.Join(' ', hashtags.Select(x => x.StartsWith('#') ? x : $"#{x}"))}";
    }
    private static TimeSpan Backoff(int attempt) => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Max(0, attempt - 1))));
    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
}
