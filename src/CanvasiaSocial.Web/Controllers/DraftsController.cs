using System.Security.Claims;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Policy = ApplicationPolicies.ApproveContent)]
[Route("Drafts")]
public sealed class DraftsController(IDraftService drafts) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken token) => View(await drafts.GetPendingAsync(token));

    [HttpPost("Review")]
    public async Task<IActionResult> Review(List<Guid> contentIds, bool approve, CancellationToken token)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
        await drafts.ReviewAsync(contentIds, approve, userId, token);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Schedule"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Schedule(List<Guid> contentIds, CancellationToken token)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
        await drafts.ReviewAsync(contentIds, true, userId, token);
        await drafts.ScheduleApprovedAsync(contentIds, token);
        return RedirectToAction(nameof(Index));
    }
}
