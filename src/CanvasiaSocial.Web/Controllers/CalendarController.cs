using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewDashboard)]
[Route("Calendar")]
public sealed class CalendarController(ICalendarService calendar) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string view = "month", DateTime? date = null, Platform? platform = null, ContentStatus? status = null, CancellationToken token = default)
    {
        var center = (date ?? DateTime.Today).Date;
        var (fromLocal, toLocal) = view switch
        {
            "day" => (center, center.AddDays(1)),
            "week" => (center.AddDays(-(int)center.DayOfWeek + 1), center.AddDays(-(int)center.DayOfWeek + 8)),
            _ => (new DateTime(center.Year, center.Month, 1), new DateTime(center.Year, center.Month, 1).AddMonths(1))
        };
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var items = await calendar.GetAsync(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified), zone),
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(toLocal, DateTimeKind.Unspecified), zone), platform, status, token);
        ViewBag.View = view; ViewBag.Date = center; ViewBag.Platform = platform; ViewBag.Status = status;
        ViewBag.CanPublishNow = calendar.CanPublishNow;
        return View(items);
    }

    [HttpPost("{id:guid}/Reschedule"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Reschedule(Guid id, DateTime localTime, CancellationToken token)
    { await calendar.RescheduleAsync(id, localTime, token); return RedirectToAction(nameof(Index)); }

    [HttpPost("{id:guid}/Cancel"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken token)
    { await calendar.CancelAsync(id, token); return RedirectToAction(nameof(Index)); }

    [HttpPost("{id:guid}/PublishNow"), Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> PublishNow(Guid id, CancellationToken token)
    {
        try
        {
            await calendar.PublishNowAsync(id, token);
            TempData["SuccessMessage"] = "Gönderi publish kuyruğuna alındı.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
