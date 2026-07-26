using System.Security.Claims;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewDashboard)]
[Route("Campaigns")]
public sealed class CampaignsController(ICampaignService campaigns) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await campaigns.GetAllAsync(cancellationToken));

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetDetailsAsync(id, cancellationToken);
        return campaign is null ? NotFound() : View(campaign);
    }

    [HttpPost("{id:guid}/Pause"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken token) { await campaigns.PauseAsync(id, token); return Back(id); }

    [HttpPost("{id:guid}/Resume"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken token) { await campaigns.ResumeAsync(id, token); return Back(id); }

    [HttpPost("{id:guid}/Cancel"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken token) { await campaigns.CancelAsync(id, token); return Back(id); }

    [HttpPost("Items/{itemId:guid}/Retry"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Retry(Guid itemId, Guid campaignId, CancellationToken token) { await campaigns.RetryItemAsync(itemId, token); return Back(campaignId); }

    [HttpPost("{id:guid}/RetryFailed"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> RetryFailed(Guid id, CancellationToken token) { await campaigns.RetryFailedItemsAsync(id, token); return Back(id); }

    [HttpPost("{id:guid}/Approve"), Authorize(Policy = ApplicationPolicies.ApproveContent)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken token)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
        await campaigns.ApproveAsync(id, userId, token); return Back(id);
    }

    [HttpPost("{id:guid}/Schedule"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Schedule(Guid id, CancellationToken token) { await campaigns.ScheduleAsync(id, token); return Back(id); }

    private RedirectToActionResult Back(Guid id) => RedirectToAction(nameof(Details), new { id });
}
