using System.Diagnostics;
using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Application.Dashboard;
using CanvasiaSocial.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewDashboard)]
public sealed class HomeController(IDashboardService dashboardService) : Controller
{
    [HttpGet("/")]
    [HttpGet("/Dashboard")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var summary = await dashboardService.GetSummaryAsync(cancellationToken);
        return View(summary);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("hata")]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
