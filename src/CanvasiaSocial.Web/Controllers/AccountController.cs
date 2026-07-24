using CanvasiaSocial.Application.Authentication;
using CanvasiaSocial.Infrastructure.Identity;
using CanvasiaSocial.Web.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CanvasiaSocial.Web.Controllers;

public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IValidator<LoginCommand> validator) : Controller
{
    [AllowAnonymous]
    [HttpGet("giris")]
    [HttpGet("Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost("giris")]
    [HttpPost("Account/Login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var command = new LoginCommand(model.Email, model.Password, model.RememberMe);
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.ErrorMessage);
            }

            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            command.Email,
            command.Password,
            command.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty,
                result.IsLockedOut
                    ? "Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin."
                    : "E-posta veya parola hatalı.");
            return View(model);
        }

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost("cikis")]
    [HttpPost("Account/Logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet("parola-degistir")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("parola-degistir")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Parolanız değiştirildi.";
        return RedirectToAction(nameof(ChangePassword));
    }

    [AllowAnonymous]
    [HttpGet("erisim-reddedildi")]
    [HttpGet("Account/AccessDenied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
