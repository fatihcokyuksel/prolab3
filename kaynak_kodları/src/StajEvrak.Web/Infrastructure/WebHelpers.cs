using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using StajEvrak.Application.Auth;
using StajEvrak.Domain.Users;

namespace StajEvrak.Web.Infrastructure;

public static class AppClaimTypes
{
    public const string SecurityStamp = "stajevrak:stamp";
}

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static string HomePath(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) ? "/Admin" : user.IsInRole(Roles.Student) ? "/Student" : "/";

    public static ClaimsPrincipal ToPrincipal(this User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}

/// <summary>
/// Her istekte çerezdeki güvenlik damgasını veritabanıyla karşılaştırır.
/// Çıkış yapıldığında damga yenilendiği için eski çerez (kopyalanmış olsa bile) geçersiz olur.
/// </summary>
public sealed class SecurityStampCookieEvents(AuthService auth) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var userId = principal?.GetUserId();
        var stamp = principal?.FindFirstValue(AppClaimTypes.SecurityStamp);
        if (userId is null || !await auth.IsSessionValidAsync(userId.Value, stamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

public static class AlertExtensions
{
    public const string TypeKey = "Alert.Type";
    public const string TitleKey = "Alert.Title";
    public const string MessageKey = "Alert.Message";

    public static void AlertSuccess(this Controller c, string message, string title = "Başarılı") => Set(c, "success", title, message);
    public static void AlertError(this Controller c, string message, string title = "Hata") => Set(c, "danger", title, message);
    public static void AlertWarning(this Controller c, string message, string title = "Uyarı") => Set(c, "warning", title, message);

    private static void Set(Controller c, string type, string title, string message)
    {
        c.TempData[TypeKey] = type;
        c.TempData[TitleKey] = title;
        c.TempData[MessageKey] = message;
    }
}

public static class DisplayExtensions
{
    /// <summary>UTC zamanı Türkiye saatine (UTC+3) çevirip biçimlendirir.</summary>
    public static string ToTr(this DateTime utc) => utc.AddHours(3).ToString("dd.MM.yyyy HH:mm");

    public static string ToTr(this DateTime? utc) => utc?.ToTr() ?? "-";

    public static string FileSize(this long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}
