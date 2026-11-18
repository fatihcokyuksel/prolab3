using Microsoft.AspNetCore.Mvc;
using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Applications;
using StajEvrak.Application.Dataset;
using StajEvrak.Application.Documents;
using StajEvrak.Application.Users;
using StajEvrak.Domain.Applications;
using StajEvrak.Web.Areas.Admin.Models;
using StajEvrak.Web.Infrastructure;

namespace StajEvrak.Web.Areas.Admin.Controllers;

public class HomeController(ApplicationService applications) : AdminControllerBase
{
    /// <summary>Panel: adminin asıl işi olan inceleme kuyruğu en üstte.</summary>
    public async Task<IActionResult> Index() => View(new AdminHomeViewModel
    {
        Stats = await applications.GetStatsAsync(),
        Queue = await applications.ListAsync(new ApplicationFilter(ApplicationStatus.Reviewing)),
        Recent = (await applications.ListAsync(new ApplicationFilter())).Where(a => a.Status != ApplicationStatus.Reviewing).Take(6).ToList()
    });
}

public class DatasetController(DatasetImportService importer, IDatasetRepository dataset) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? department) =>
        View(await BuildModelAsync(q, department, null));

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            this.AlertError("Lütfen bir veri seti dosyası (.xlsx veya .csv) seçiniz.");
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var result = await importer.ImportAsync(file.FileName, stream);
        if (!result.Succeeded)
            this.AlertError(result.Error!, "Veri seti yüklenemedi");
        else if (result.RowErrors.Count > 0)
            this.AlertWarning($"{result.Added} kayıt eklendi, {result.Updated} kayıt güncellendi. {result.RowErrors.Count} satır hatalı olduğu için atlandı (ayrıntılar tabloda).", "Veri seti kısmen yüklendi");
        else
            this.AlertSuccess($"{result.Added} kayıt eklendi, {result.Updated} kayıt güncellendi, {result.LinkedStudents} öğrenci hesabı eşleştirildi.", "Veri seti yüklendi");

        return View(nameof(Index), await BuildModelAsync(null, null, result));
    }

    private async Task<DatasetIndexViewModel> BuildModelAsync(string? q, string? department, DatasetImportResult? result) => new()
    {
        Students = await dataset.SearchAsync(q, department),
        Departments = await dataset.GetDepartmentsAsync(),
        Query = q,
        Department = department,
        ImportResult = result,
        SupportedExtensions = importer.SupportedExtensions
    };
}

public class ApplicationsController(
    ApplicationService applications,
    DocumentUploadService documents,
    IDatasetRepository dataset) : AdminControllerBase
{
    /// <summary>Tüm başvurular (filtreli). "Tamamlandı" sekmesi: <see cref="Completed"/>.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(ApplicationStatus? status, string? department, string? q)
    {
        if (status == ApplicationStatus.Completed) return RedirectToAction(nameof(Completed), new { department, q });
        var filter = new ApplicationFilter(status, department, q);
        return View(new ApplicationsIndexViewModel
        {
            Applications = (await applications.ListAsync(filter)).Where(a => a.Status != ApplicationStatus.Completed).ToList(),
            Filter = filter,
            Departments = await dataset.GetDepartmentsAsync(),
            Stats = await applications.GetStatsAsync(),
            CompletedTab = false
        });
    }

    [HttpGet]
    public async Task<IActionResult> Completed(string? department, string? q)
    {
        var filter = new ApplicationFilter(ApplicationStatus.Completed, department, q);
        return View(nameof(Index), new ApplicationsIndexViewModel
        {
            Applications = await applications.ListAsync(filter),
            Filter = filter,
            Departments = await dataset.GetDepartmentsAsync(),
            Stats = await applications.GetStatsAsync(),
            CompletedTab = true
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var details = await applications.GetAsync(CurrentUserId, id);
        return details is null ? NotFound() : View(details);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id, string? note) =>
        Handle(await applications.ApproveAsync(CurrentUserId, id, note), "Öğrencinin başvurusu onaylandı.", id);

    [HttpPost]
    public async Task<IActionResult> MarkInvalid(int id, string note) =>
        Handle(await applications.MarkInvalidAsync(CurrentUserId, id, note), "Başvuru hatalı / eksik olarak işaretlendi; öğrenci notu görecek.", id);

    [HttpPost]
    public async Task<IActionResult> Complete(int id) =>
        Handle(await applications.CompleteAsync(CurrentUserId, id), "Başvuru tamamlandı; öğrenci artık değişiklik yapamaz.", id);

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await applications.DeleteAsync(CurrentUserId, id);
        if (!result.Succeeded) { this.AlertError(result.Error!); return RedirectToAction(nameof(Details), new { id }); }
        this.AlertSuccess("Tüm belge sürümleri de silindi.", "Başvuru silindi");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDocument(int id, int documentId) =>
        Handle(await documents.DeleteDocumentAsync(CurrentUserId, id, documentId), "Belge sürümü silindi.", id);

    [HttpGet]
    public async Task<IActionResult> Download(int documentId)
    {
        var file = await documents.GetFileAsync(CurrentUserId, documentId);
        return file is null || !System.IO.File.Exists(file.FullPath)
            ? NotFound()
            : PhysicalFile(file.FullPath, file.ContentType, file.DownloadName);
    }

    private IActionResult Handle(Application.Common.OperationResult result, string success, int id)
    {
        if (result.Succeeded) this.AlertSuccess(success);
        else this.AlertError(result.Error!);
        return RedirectToAction(nameof(Details), new { id });
    }
}

/// <summary>Hesap yönetimi: listeleme, bilgileri düzenleme, şifre sıfırlama, kilit açma, oturum kapatma, silme.</summary>
public class UsersController(UserAdministrationService admin, ILoginAttemptRepository attempts, IUserRepository users, Application.Abstractions.IClock clock) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? role)
    {
        var all = await users.ListAsync();
        return View(new UsersIndexViewModel
        {
            Users = await admin.SearchAsync(q, role),
            RecentAttempts = await attempts.RecentAsync(30),
            Now = clock.UtcNow,
            Query = q,
            Role = role,
            StudentCount = all.Count(u => u is Domain.Users.Student),
            AdminCount = all.Count(u => u is Domain.Users.Admin)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var details = await admin.GetDetailsAsync(id);
        if (details is null) return NotFound();
        var viewer = await users.FindByIdAsync(CurrentUserId);
        return View(new UserDetailsViewModel
        {
            Details = details,
            Now = clock.UtcNow,
            IsSelf = id == CurrentUserId,
            CanDelete = viewer is not null && details.User.CanBeDeletedBy(viewer) // çok biçimli kural: Admin false, Student admin'e true
        });
    }

    [HttpPost]
    public async Task<IActionResult> Update(int id, string? userName, string? email, string? studentNo, string? fullName) =>
        Handle(await admin.UpdateAccountAsync(CurrentUserId, id, new UpdateAccountRequest(userName, email, studentNo, fullName)),
            "Hesap bilgileri güncellendi", "Değişiklikler kaydedildi.", id);

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, string? newPassword, string? confirmPassword)
    {
        if (newPassword != confirmPassword)
            return Handle(Application.Common.OperationResult.Fail("Girilen iki şifre aynı değil."), "", "", id);
        var result = await admin.ResetPasswordAsync(CurrentUserId, id, newPassword);
        if (result.Succeeded && id == CurrentUserId)
        {
            // Kendi şifresini değiştiren admin, damgası yenilendiği için yeniden giriş yapar.
            this.AlertSuccess("Yeni şifrenizle tekrar giriş yapın.", "Şifre değiştirildi");
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        return Handle(result, "Şifre sıfırlandı", "Kullanıcının açık oturumları kapatıldı; yeni şifreyi kendisine iletin.", id);
    }

    [HttpPost]
    public async Task<IActionResult> Unlock(int id, string? returnTo) =>
        Handle(await admin.UnlockAsync(CurrentUserId, id), "Kilit açıldı", "Kullanıcı yeniden giriş yapabilir.", id, returnTo);

    [HttpPost]
    public async Task<IActionResult> EndSessions(int id) =>
        Handle(await admin.EndSessionsAsync(CurrentUserId, id), "Oturumlar kapatıldı", "Kullanıcı tüm cihazlarda yeniden giriş yapmak zorunda.", id);

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await admin.DeleteAsync(CurrentUserId, id);
        if (!result.Succeeded) { this.AlertError(result.Error!, "Hesap silinemedi"); return RedirectToAction(nameof(Details), new { id }); }
        this.AlertSuccess("Başvurusu, belgeleri ve Ek-16 bilgileri de silindi. Giriş kayıtları log olarak saklanıyor.", "Hesap silindi");
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Handle(Application.Common.OperationResult result, string title, string message, int id, string? returnTo = null)
    {
        if (result.Succeeded) this.AlertSuccess(message, title);
        else this.AlertError(result.Error!, "İşlem yapılamadı");
        return returnTo == "list" ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id });
    }
}
