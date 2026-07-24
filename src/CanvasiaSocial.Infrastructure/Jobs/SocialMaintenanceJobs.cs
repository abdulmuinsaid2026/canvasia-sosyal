using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanvasiaSocial.Infrastructure.Jobs;

public sealed class RecoverPublishingPostsJob(
    ApplicationDbContext dbContext,
    ILogger<RecoverPublishingPostsJob> logger)
{
    [Queue("publish")]
    [DisableConcurrentExecution(55)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var posts = await dbContext.ScheduledPosts.Include(x => x.GeneratedContent)
            .Where(x => x.Status == ContentStatus.Publishing && x.LastAttemptAtUtc < cutoff)
            .ToListAsync(cancellationToken);
        foreach (var post in posts)
        {
            post.Status = ContentStatus.Failed;
            post.LastErrorCode = "OutcomeUnknown";
            post.LastErrorMessage = "Yayın işlemi kesildi; olası çift gönderiyi önlemek için otomatik tekrar durduruldu. Platform hesabını kontrol edin.";
            post.NextRetryAtUtc = null;
            post.UpdatedAtUtc = DateTime.UtcNow;
            post.GeneratedContent.Status = ContentStatus.Failed;
            var attempt = await dbContext.PublishAttempts.FirstOrDefaultAsync(
                x => x.ScheduledPostId == post.Id && x.AttemptNumber == post.AttemptCount, cancellationToken);
            if (attempt is not null && !attempt.CompletedAtUtc.HasValue)
            {
                attempt.CompletedAtUtc = DateTime.UtcNow;
                attempt.Success = false;
                attempt.PlatformErrorCode = "OutcomeUnknown";
                attempt.ErrorMessage = post.LastErrorMessage;
            }
            logger.LogWarning("Kesintiye uğramış sosyal yayın kullanıcı incelemesine alındı. Post: {PostId}", post.Id);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CleanupOAuthStatesJob(ApplicationDbContext dbContext)
{
    [Queue("default")]
    [DisableConcurrentExecution(55)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-1);
        var query = dbContext.OAuthStates.Where(x => x.ExpiresAtUtc < cutoff || x.ConsumedAtUtc < cutoff);
        if (dbContext.Database.IsRelational())
            await query.ExecuteDeleteAsync(cancellationToken);
        else
        {
            dbContext.OAuthStates.RemoveRange(await query.ToListAsync(cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
