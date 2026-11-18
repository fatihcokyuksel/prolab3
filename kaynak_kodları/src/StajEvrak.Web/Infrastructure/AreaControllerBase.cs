using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StajEvrak.Domain.Users;

namespace StajEvrak.Web.Infrastructure;

/// <summary>Oturum açmış kullanıcının kimliğine erişim sağlayan ortak taban (kalıtım).</summary>
public abstract class AuthenticatedController : Controller
{
    protected int CurrentUserId => User.GetUserId() ?? throw new UnauthorizedAccessException();
}

/// <summary>Admin paneli: yalnızca ROLE_ADMIN erişebilir; diğer roller 403 + login yönlendirmesi alır.</summary>
[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public abstract class AdminControllerBase : AuthenticatedController;

/// <summary>Öğrenci ekranları: yalnızca ROLE_STUDENT erişebilir.</summary>
[Area("Student")]
[Authorize(Roles = Roles.Student)]
public abstract class StudentControllerBase : AuthenticatedController;
