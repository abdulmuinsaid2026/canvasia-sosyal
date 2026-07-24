using CanvasiaSocial.Application.Campaigns;

namespace CanvasiaSocial.Infrastructure.Campaigns;

public sealed class ScheduleCalculator : IScheduleCalculator
{
    public IReadOnlyList<DateTime> Calculate(ScheduleRequest request)
    {
        if (request.Count < 0 || request.IntervalMinutes < 1 || request.DailyLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
        if (request.AllowedStart >= request.AllowedEnd)
        {
            throw new ArgumentException("İzin verilen başlangıç saati bitiş saatinden önce olmalıdır.", nameof(request));
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId);
        var candidate = DateTime.SpecifyKind(request.StartLocal, DateTimeKind.Unspecified);
        var result = new List<DateTime>(request.Count);
        var day = candidate.Date;
        var usedToday = 0;

        while (result.Count < request.Count)
        {
            var windowStart = day.Add(request.AllowedStart.ToTimeSpan());
            var windowEnd = day.Add(request.AllowedEnd.ToTimeSpan());
            if (candidate < windowStart) candidate = windowStart;

            if (candidate > windowEnd || usedToday >= request.DailyLimit)
            {
                day = day.AddDays(1);
                candidate = day.Add(request.AllowedStart.ToTimeSpan());
                usedToday = 0;
                continue;
            }

            while (zone.IsInvalidTime(candidate)) candidate = candidate.AddMinutes(1);
            result.Add(TimeZoneInfo.ConvertTimeToUtc(candidate, zone));
            usedToday++;
            candidate = candidate.AddMinutes(request.IntervalMinutes);
        }

        return result;
    }
}
