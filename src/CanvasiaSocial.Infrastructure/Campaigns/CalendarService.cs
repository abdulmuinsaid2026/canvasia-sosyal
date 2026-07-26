using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Jobs;
using CanvasiaSocial.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Campaigns;

public sealed class CalendarService(
    ApplicationDbContext dbContext,
    IBackgroundJobClient jobs,
    CampaignOptions options) : ICalendarService
{
    public bool CanPublishNow => options.AutoPublishEnabled;

    public async Task<IReadOnlyList<CalendarEntry>> GetAsync(
        DateTime fromUtc, DateTime toUtc, Platform? platform, ContentStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ScheduledPosts.AsNoTracking()
            .Where(x => x.ScheduledAtUtc >= fromUtc && x.ScheduledAtUtc < toUtc);
        if (platform.HasValue) query = query.Where(x => x.Platform == platform.Value);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        return await query.OrderBy(x => x.ScheduledAtUtc).Select(post => new CalendarEntry(
            post.Id,
            dbContext.CampaignItems.Where(item => item.ScheduledPostId == post.Id).Select(item => (Guid?)item.CampaignId).FirstOrDefault(),
            dbContext.CampaignItems.Where(item => item.ScheduledPostId == post.Id).Select(item => item.Campaign.Name).FirstOrDefault() ?? "Bağımsız",
            dbContext.CampaignItems.Where(item => item.ScheduledPostId == post.Id).Select(item => item.ProductCache.Title).FirstOrDefault() ?? "Ürün",
            post.Platform, post.Status, post.ScheduledAtUtc,
            dbContext.CampaignItems.Where(item => item.ScheduledPostId == post.Id).Select(item => item.Campaign.TimeZoneId).FirstOrDefault() ?? "Europe/Istanbul",
            post.AttemptCount, post.LastErrorCode, post.LastErrorMessage))
            .ToListAsync(cancellationToken);
    }

    public async Task RescheduleAsync(Guid scheduledPostId, DateTime localTime, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.CampaignItems.Include(x => x.Campaign).Include(x => x.ScheduledPost)
            .FirstOrDefaultAsync(x => x.ScheduledPostId == scheduledPostId, cancellationToken)
            ?? throw new InvalidOperationException("Takvim kaydı bulunamadı.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(item.Campaign.TimeZoneId);
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), zone);
        var interval = TimeSpan.FromMinutes(Math.Max(1, item.Campaign.IntervalMinutes));
        var hasConflict = await dbContext.ScheduledPosts.AsNoTracking().AnyAsync(x =>
            x.Id != scheduledPostId && x.Platform == item.Campaign.Platform && x.Status == ContentStatus.Scheduled &&
            x.ScheduledAtUtc > utc - interval && x.ScheduledAtUtc < utc + interval, cancellationToken);
        if (hasConflict) throw new InvalidOperationException("Seçilen zaman başka bir gönderiyle çakışıyor.");
        item.ScheduledPost!.ScheduledAtUtc = utc;
        item.ScheduledPost.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid scheduledPostId, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            var changed = await dbContext.ScheduledPosts
                .Where(x => x.Id == scheduledPostId && (x.Status == ContentStatus.Scheduled || x.Status == ContentStatus.Failed))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, ContentStatus.Cancelled)
                    .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);
            if (changed != 1) throw new InvalidOperationException("Yalnızca planlanmış veya başarısız gönderiler iptal edilebilir.");
        }
        else
        {
            var post = await dbContext.ScheduledPosts.FirstOrDefaultAsync(x => x.Id == scheduledPostId, cancellationToken)
                ?? throw new InvalidOperationException("Takvim kaydı bulunamadı.");
            if (post.Status is not (ContentStatus.Scheduled or ContentStatus.Failed))
                throw new InvalidOperationException("Yalnızca planlanmış veya başarısız gönderiler iptal edilebilir.");
            post.Status = ContentStatus.Cancelled;
            post.UpdatedAtUtc = DateTime.UtcNow;
        }
        var item = await dbContext.CampaignItems.FirstOrDefaultAsync(x => x.ScheduledPostId == scheduledPostId, cancellationToken);
        if (item is not null) item.Status = ContentStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishNowAsync(Guid scheduledPostId, CancellationToken cancellationToken = default)
    {
        if (!options.AutoPublishEnabled)
            throw new InvalidOperationException("Manuel yayın için AUTO_PUBLISH_ENABLED etkinleştirilmelidir.");

        var post = await dbContext.ScheduledPosts.FirstOrDefaultAsync(x => x.Id == scheduledPostId, cancellationToken)
            ?? throw new InvalidOperationException("Takvim kaydı bulunamadı.");
        if (post.Status != ContentStatus.Scheduled)
            throw new InvalidOperationException("Yalnızca planlanmış gönderiler hemen yayımlanabilir.");

        post.SocialAccountId = await ResolveActiveSocialAccountIdAsync(post.SocialAccountId, post.Platform, cancellationToken);
        post.ScheduledAtUtc = DateTime.UtcNow;
        post.NextRetryAtUtc = null;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<PublishScheduledPostJob>(job => job.ExecuteAsync(post.Id, CancellationToken.None));
    }

    public async Task RetryPublishAsync(Guid scheduledPostId, CancellationToken cancellationToken = default)
    {
        if (!options.AutoPublishEnabled)
            throw new InvalidOperationException("Yeniden yayınlamak için gerçek gönderim etkinleştirilmelidir.");

        var snapshot = await dbContext.ScheduledPosts.AsNoTracking()
            .Where(x => x.Id == scheduledPostId)
            .Select(x => new { x.Status, x.GeneratedContentId, x.LastErrorCode, x.SocialAccountId, x.Platform })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Takvim kaydı bulunamadı.");
        if (snapshot.Status != ContentStatus.Failed)
            throw new InvalidOperationException("Yalnızca başarısız gönderiler yeniden yayınlanabilir.");
        if (snapshot.LastErrorCode == "OutcomeUnknown")
            throw new InvalidOperationException("Yayın sonucu belirsiz. Çift gönderiyi önlemek için önce Instagram profilini kontrol edin; bu kayıt otomatik olarak yeniden yayınlanamaz.");
        if (await dbContext.ProductPublicationHistories.AsNoTracking()
            .AnyAsync(x => x.ScheduledPostId == scheduledPostId, cancellationToken))
            throw new InvalidOperationException("Bu gönderi platformda daha önce yayımlanmış. Çift gönderiyi önlemek için işlem durduruldu.");

        var now = DateTime.UtcNow;
        var socialAccountId = await ResolveActiveSocialAccountIdAsync(snapshot.SocialAccountId, snapshot.Platform, cancellationToken);
        if (dbContext.Database.IsRelational())
        {
            var changed = await dbContext.ScheduledPosts
                .Where(x => x.Id == scheduledPostId && x.Status == ContentStatus.Failed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, ContentStatus.Scheduled)
                    .SetProperty(x => x.ScheduledAtUtc, now)
                    .SetProperty(x => x.NextRetryAtUtc, (DateTime?)null)
                    .SetProperty(x => x.SocialAccountId, socialAccountId)
                    .SetProperty(x => x.LastErrorCode, (string?)null)
                    .SetProperty(x => x.LastErrorMessage, (string?)null)
                    .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);
            if (changed != 1) throw new InvalidOperationException("Gönderinin durumu değişti; sayfayı yenileyip tekrar kontrol edin.");
        }
        else
        {
            var post = await dbContext.ScheduledPosts.FirstAsync(x => x.Id == scheduledPostId, cancellationToken);
            post.Status = ContentStatus.Scheduled;
            post.ScheduledAtUtc = now;
            post.NextRetryAtUtc = null;
            post.SocialAccountId = socialAccountId;
            post.LastErrorCode = null;
            post.LastErrorMessage = null;
            post.UpdatedAtUtc = now;
        }
        var content = await dbContext.GeneratedContents.FirstAsync(x => x.Id == snapshot.GeneratedContentId, cancellationToken);
        content.Status = ContentStatus.Scheduled;
        content.UpdatedAtUtc = now;
        var item = await dbContext.CampaignItems.FirstOrDefaultAsync(x => x.ScheduledPostId == scheduledPostId, cancellationToken);
        if (item is not null)
        {
            item.Status = ContentStatus.Scheduled;
            item.ErrorMessage = null;
            item.UpdatedAtUtc = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<PublishScheduledPostJob>(job => job.ExecuteAsync(scheduledPostId, CancellationToken.None));
    }

    private async Task<Guid> ResolveActiveSocialAccountIdAsync(
        Guid? selectedAccountId, Platform platform, CancellationToken cancellationToken)
    {
        if (selectedAccountId.HasValue && await dbContext.SocialAccounts.AsNoTracking().AnyAsync(x =>
                x.Id == selectedAccountId.Value && x.Platform == platform && x.Status == "Active", cancellationToken))
        {
            return selectedAccountId.Value;
        }

        var activeAccountIds = await dbContext.SocialAccounts.AsNoTracking()
            .Where(x => x.Platform == platform && x.Status == "Active")
            .Select(x => x.Id).Take(2).ToListAsync(cancellationToken);
        return activeAccountIds.Count switch
        {
            1 => activeAccountIds[0],
            0 => throw new InvalidOperationException("Yayın için etkin ve platformla eşleşen bir sosyal hesap bulunamadı."),
            _ => throw new InvalidOperationException("Birden fazla etkin sosyal hesap bulundu. Gönderi için yayın hesabı seçilmelidir.")
        };
    }
}
