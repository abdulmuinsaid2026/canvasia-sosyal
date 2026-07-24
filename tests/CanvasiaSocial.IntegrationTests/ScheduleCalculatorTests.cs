using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Infrastructure.Campaigns;

namespace CanvasiaSocial.IntegrationTests;

public sealed class ScheduleCalculatorTests
{
    private readonly ScheduleCalculator calculator = new();

    [Fact]
    public void Respects_interval_allowed_hours_and_daily_limit()
    {
        var result = calculator.Calculate(new ScheduleRequest(
            new DateTime(2026, 7, 22, 8, 30, 0), "Europe/Istanbul", new TimeOnly(9, 0),
            new TimeOnly(11, 0), 60, 2, 3));

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2026, 7, 22, 6, 0, 0, DateTimeKind.Utc), result[0]);
        Assert.Equal(new DateTime(2026, 7, 22, 7, 0, 0, DateTimeKind.Utc), result[1]);
        Assert.Equal(new DateTime(2026, 7, 23, 6, 0, 0, DateTimeKind.Utc), result[2]);
    }

    [Fact]
    public void Rejects_invalid_allowed_window()
    {
        Assert.Throws<ArgumentException>(() => calculator.Calculate(new ScheduleRequest(
            DateTime.Today, "Europe/Istanbul", new TimeOnly(21, 0), new TimeOnly(9, 0), 60, 10, 1)));
    }
}
