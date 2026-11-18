using Microsoft.AspNetCore.Mvc;
using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Applications;
using StajEvrak.Application.Common;
using StajEvrak.Application.Documents;
using StajEvrak.Application.Profiles;
using StajEvrak.Web.Areas.Student.Models;
using StajEvrak.Web.Infrastructure;

namespace StajEvrak.Web.Areas.Student.Controllers;

public class HomeController(ApplicationService applications, IUserRepository users, StudentProfileService profiles) : StudentControllerBase
{
    public async Task<IActionResult> Index() => View(new StudentHomeViewModel
    {
        Student = (await users.GetStudentAsync(CurrentUserId))!,
        Details = await applications.GetForStudentAsync(CurrentUserId),
        ProfileFilled = (await profiles.GetValuesAsync(CurrentUserId)).Count(v => !string.IsNullOrWhiteSpace(v.Value)) > 3
    });
}

/// <summary>Ek-16 bilgilerinin öğrenci tarafından sisteme manuel girilmesi.</summary>
public class ProfileController(StudentProfileService profiles) : StudentControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(new ProfileViewModel
    {
        Values = (await profiles.GetValuesAsync(CurrentUserId)).ToDictionary(k => k.Key, v => v.Value),
        CanEdit = await profiles.CanEditAsync(CurrentUserId)
    });

    /// <summary>
    /// Telefon alanları iki parça gelir: ülke kodu (phoneCodes) ve ulusal numara (values).
    /// Birleştirilip servise "+90 5321234567" biçiminde verilir; doğrulama ve standart biçim serviste yapılır.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Index(Dictionary<string, string?> values, Dictionary<string, string?> phoneCodes)
    {
        var posted = new Dictionary<string, string?>(values);
        foreach (var (code, countryCode) in phoneCodes)
        {
            if (posted.TryGetValue(code, out var number) && !string.IsNullOrWhiteSpace(number) && !number.TrimStart().StartsWith('+'))
                posted[code] = $"+{TextHelper.DigitsOnly(countryCode)} {number.Trim()}";
        }

        var result = await profiles.SaveAsync(CurrentUserId, posted);
        if (result.Succeeded)
        {
            this.AlertSuccess("Yüklediğiniz belge bu bilgilerle karşılaştırılacak.", "Bilgiler kaydedildi");
            return RedirectToAction(nameof(Index));
        }

        this.AlertError(result.Error!, "Bilgiler kaydedilmedi");
        if (result.FieldErrors.Count == 0) return RedirectToAction(nameof(Index));

        // Girilen değerler kaybolmasın: form, hatalı alanlar işaretli olarak yeniden gösterilir.
        return View(new ProfileViewModel { Values = posted, CanEdit = true, Errors = result.FieldErrors });
    }
}

public class DocumentsController(DocumentUploadService documents, ApplicationService applications, IUserRepository users) : StudentControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var details = await applications.GetForStudentAsync(CurrentUserId);
        var student = (await users.GetStudentAsync(CurrentUserId))!;
        return View(new DocumentsViewModel
        {
            Application = details?.Application,
            CanUpload = details is null || details.Application.CanBeModifiedBy(student),
            AllowedExtensions = documents.AllowedExtensions
        });
    }

    /// <summary>"Evrak Yükle": format/uzantı/imza kontrolünden geçen dosya kaydedilir ve okunur.</summary>
    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            this.AlertError("Lütfen yüklenecek bir dosya seçiniz.", "Dosya seçilmedi");
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var result = await documents.UploadAsync(CurrentUserId, file.FileName, file.Length, stream);
        if (!result.Succeeded)
        {
            this.AlertError(result.Error!, "Evrak reddedildi");
            return RedirectToAction(nameof(Index));
        }

        this.AlertSuccess($"{result.Value!.Version}. sürüm incelemeye alındı. {result.Value.ReadNote}", "Evrak yüklendi");
        return RedirectToAction("Index", "Report");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int applicationId, int documentId)
    {
        var result = await documents.DeleteDocumentAsync(CurrentUserId, applicationId, documentId);
        if (result.Succeeded) this.AlertSuccess("Sürüm listeden kaldırıldı.", "Belge silindi");
        else this.AlertError(result.Error!);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Download(int documentId)
    {
        var file = await documents.GetFileAsync(CurrentUserId, documentId);
        return file is null || !System.IO.File.Exists(file.FullPath)
            ? NotFound()
            : PhysicalFile(file.FullPath, file.ContentType, file.DownloadName);
    }
}

public class ReportController(ApplicationService applications) : StudentControllerBase
{
    public async Task<IActionResult> Index() => View(await applications.GetForStudentAsync(CurrentUserId));
}
