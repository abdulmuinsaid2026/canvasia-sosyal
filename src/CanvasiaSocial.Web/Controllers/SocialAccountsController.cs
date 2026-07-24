using System.Security.Claims;
using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
[Route("SocialAccounts")]
public sealed class SocialAccountsController(ISocialAccountService accounts) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await accounts.GetCardsAsync(cancellationToken));

    [HttpPost("{platform}/Connect")]
    public async Task<IActionResult> Connect(Platform platform, CancellationToken cancellationToken)
    {
        try
        {
            var result = await accounts.BeginAuthorizationAsync(platform, UserId(), cancellationToken);
            return Redirect(result.AuthorizationUrl.ToString());
        }
        catch (InvalidOperationException exception)
        {
            SetResult(false, exception.Message);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("{platform}/Callback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback(
        Platform platform,
        string? state,
        string? code,
        string? error,
        CancellationToken cancellationToken)
    {
        var result = await accounts.CompleteAuthorizationAsync(platform, state ?? string.Empty, code, error, UserId(), cancellationToken);
        SetResult(result.Succeeded, result.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{accountId:guid}/Validate")]
    public async Task<IActionResult> Validate(Guid accountId, CancellationToken cancellationToken) =>
        Back(await accounts.ValidateAsync(accountId, cancellationToken));

    [HttpPost("{accountId:guid}/Refresh")]
    public async Task<IActionResult> Refresh(Guid accountId, CancellationToken cancellationToken) =>
        Back(await accounts.RefreshAsync(accountId, cancellationToken));

    [HttpPost("{accountId:guid}/Disconnect")]
    public async Task<IActionResult> Disconnect(Guid accountId, CancellationToken cancellationToken) =>
        Back(await accounts.DisconnectAsync(accountId, cancellationToken));

    private IActionResult Back(SocialOperationResult result)
    {
        SetResult(result.Succeeded, result.Message);
        return RedirectToAction(nameof(Index));
    }
    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Kullanıcı kimliği bulunamadı.");
    private void SetResult(bool succeeded, string message)
    {
        TempData["SocialResultSucceeded"] = succeeded;
        TempData["SocialResultMessage"] = message;
    }
}
