using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajEvrak.Web.Infrastructure;
using StajEvrak.Web.Models;

namespace StajEvrak.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    /// <summary>Giriş yapmış kullanıcıyı rolüne göre kendi ana sayfasına, diğerlerini girişe yönlendirir.</summary>
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(User.HomePath()) : RedirectToAction("Login", "Account");

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [Route("Home/StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        ViewData["Code"] = code;
        return View("StatusCode");
    }
}
