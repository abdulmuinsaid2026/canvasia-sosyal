using System.Security.Claims;
using CanvasiaSocial.Application.Campaigns;
using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Policy = ApplicationPolicies.ManageContent)]
[Route("BulkPrepare")]
public sealed class BulkPrepareController(IProductCacheService products, ICampaignService campaigns) : Controller
{
    [HttpPost("")]
    public async Task<IActionResult> Index(List<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().Take(101).ToList();
        if (ids.Count is 0 or > 100 || ids.Count != productIds.Count) return BadRequest("1 ile 100 arasında benzersiz ürün seçilmelidir.");
        var productsList = await products.GetByIdsAsync(ids, cancellationToken);
        var model = new BulkPrepareViewModel
        {
            ProductIds = ids,
            Products = productsList,
            Name = $"Kampanya {DateTime.Now:dd.MM.yyyy}"
        };
        model.SocialAccounts = await GetAllSocialAccountsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost("Start")]
    public async Task<IActionResult> Start(BulkPrepareViewModel model, CancellationToken cancellationToken)
    {
        if (model.ProductIds.Count is 0 or > 100) return BadRequest("Ürün sınırı aşıldı.");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
        try
        {
            var id = await campaigns.CreateAsync(new CreateCampaignRequest(model.Name, model.Platform, model.SocialAccountId,
                model.Mode, model.StartLocal, model.IntervalMinutes, model.DailyLimit, model.AllowedStartTime,
                model.AllowedEndTime, model.IncludePrice, model.IncludeProductLink, model.ProductIds, userId), cancellationToken);
            return RedirectToAction("Details", "Campaigns", new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            model.Products = await products.GetByIdsAsync(model.ProductIds, cancellationToken);
            model.SocialAccounts = await GetAllSocialAccountsAsync(cancellationToken);
            return View("Index", model);
        }
    }

    private async Task<IReadOnlyList<SocialAccountOption>> GetAllSocialAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new List<SocialAccountOption>();
        foreach (var platform in Enum.GetValues<CanvasiaSocial.Domain.Enums.Platform>())
        {
            accounts.AddRange(await campaigns.GetSocialAccountsAsync(platform, cancellationToken));
        }
        return accounts;
    }
}
