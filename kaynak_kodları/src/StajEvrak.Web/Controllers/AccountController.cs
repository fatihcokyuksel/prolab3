using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajEvrak.Application.Auth;
using StajEvrak.Web.Infrastructure;
using StajEvrak.Web.Models;

namespace StajEvrak.Web.Controllers;

public class AccountController(AuthService auth) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await auth.LoginAsync(model.Identifier, model.Password, ip);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            ViewData["LockedOut"] = result.Status == LoginStatus.LockedOut;
            return View(model);
        }

        // Önceki oturum varsa kapatılıp yeni kullanıcıyla açılır.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.User!.ToPrincipal());

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl.StartsWith(result.User!.HomePath, StringComparison.OrdinalIgnoreCase))
            return LocalRedirect(model.ReturnUrl);
        return LocalRedirect(result.User!.HomePath);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await auth.RegisterStudentAsync(
            new RegisterStudentRequest(model.UserName, model.Email, model.StudentNo, model.FullName, model.Password));
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        var linked = result.Value!.DatasetStudentId is not null;
        this.AlertSuccess(linked
            ? "Hesabınız oluşturuldu ve okul kayıtlarınızla (veri seti) eşleştirildi. Şimdi giriş yapabilirsiniz."
            : "Hesabınız oluşturuldu. Not: Veri setinde öğrenci numaranız/e-postanız bulunamadı; admin veri setini yüklediğinde otomatik eşleşecektir.");
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();
        if (userId is not null) await auth.LogoutAsync(userId.Value); // eski çerezleri geçersiz kılar
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        this.AlertSuccess("Oturumunuz kapatıldı.");
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Yetkisiz erişim: 403 döner ve birkaç saniye sonra giriş sayfasına yönlendirir.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }
}
