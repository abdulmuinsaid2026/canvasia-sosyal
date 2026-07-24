using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Campaigns;

public sealed class CalendarService(ApplicationDbContext dbContext) : ICalendarService
{
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
            dbContext.CampaignItems.Where(item => item.ScheduledPostId == post.Id).Select(item => item.Campaign.TimeZoneId).FirstOrDefault() ?? "Europe/Istanbul"))
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
        var post = await dbContext.ScheduledPosts.FirstOrDefaultAsync(x => x.Id == scheduledPostId, cancellationToken)
            ?? throw new InvalidOperationException("Takvim kaydı bulunamadı.");
        post.Status = ContentStatus.Cancelled;
        post.UpdatedAtUtc = DateTime.UtcNow;
        var item = await dbContext.CampaignItems.FirstOrDefaultAsync(x => x.ScheduledPostId == scheduledPostId, cancellationToken);
        if (item is not null) item.Status = ContentStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
