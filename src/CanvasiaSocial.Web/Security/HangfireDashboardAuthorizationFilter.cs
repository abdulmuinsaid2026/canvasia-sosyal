using CanvasiaSocial.Application.Common.Security;
using Hangfire.Dashboard;

namespace CanvasiaSocial.Web.Security;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(ApplicationRoles.Admin);
    }
}
