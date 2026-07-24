namespace CanvasiaSocial.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
