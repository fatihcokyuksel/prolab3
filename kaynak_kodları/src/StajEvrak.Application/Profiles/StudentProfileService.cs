using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Common;
using StajEvrak.Application.Validation;
using StajEvrak.Domain.Ek16;
using StajEvrak.Domain.Profiles;

namespace StajEvrak.Application.Profiles;

/// <summary>Kaydetme sonucu: genel hata veya alan bazında doğrulama hataları.</summary>
public sealed record ProfileSaveResult(bool Succeeded, string? Error, IReadOnlyDictionary<string, string> FieldErrors)
{
    public static ProfileSaveResult Ok() => new(true, null, new Dictionary<string, string>());
    public static ProfileSaveResult Fail(string error) => new(false, error, new Dictionary<string, string>());
}

/// <summary>Öğrencinin elle girdiği Ek-16 bilgilerini okur/kaydeder.</summary>
public sealed class StudentProfileService(
    IProfileRepository profiles,
    IApplicationRepository applications,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<IReadOnlyDictionary<string, string?>> GetValuesAsync(int studentId, CancellationToken ct = default)
    {
        var profile = await profiles.GetAsync(studentId, ct);
        if (profile is not null) return profile.ToFieldValues();

        // İlk açılışta, öğrenci veri setiyle eşleşmişse ortak alanlar ön doldurulur.
        var student = await users.GetStudentAsync(studentId, ct);
        var defaults = new Dictionary<string, string?>
        {
            [Ek16Fields.OgrenciNo] = student?.StudentNo,
            [Ek16Fields.Email] = student?.Email,
            [Ek16Fields.AdSoyad] = string.IsNullOrWhiteSpace(student?.FullName) ? null : student.FullName,
        };
        return defaults;
    }

    public async Task<bool> CanEditAsync(int studentId, CancellationToken ct = default)
    {
        var student = await users.GetStudentAsync(studentId, ct);
        var application = await applications.GetByStudentAsync(studentId, ct);
        return student is not null && (application is null || application.CanBeModifiedBy(student));
    }

    public async Task<ProfileSaveResult> SaveAsync(int studentId, IReadOnlyDictionary<string, string?> values, CancellationToken ct = default)
    {
        if (!await CanEditAsync(studentId, ct))
            return ProfileSaveResult.Fail("Başvurunuz onaylandığı/tamamlandığı için bilgiler değiştirilemez.");

        // Her alan kendi türünün kuralıyla doğrulanır ve standart biçime getirilir (TC, telefon, e-posta...).
        var normalized = new Dictionary<string, string?>();
        var errors = new Dictionary<string, string>();
        foreach (var (code, raw) in values)
        {
            if (!Ek16Fields.IsKnown(code)) continue;
            var field = Ek16Fields.Get(code);
            var check = FieldValidator.For(field).Validate(raw, values);
            if (check.IsValid) normalized[field.Code] = check.Value;
            else errors[field.Code] = check.Error!;
        }
        if (errors.Count > 0)
            return new ProfileSaveResult(false, $"{errors.Count} alan hatalı olduğu için bilgiler kaydedilmedi.", errors);

        var profile = await profiles.GetAsync(studentId, ct);
        if (profile is null)
        {
            profile = new StudentProfile(studentId, clock.UtcNow);
            profiles.Add(profile);
        }
        profile.SetValues(normalized, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return ProfileSaveResult.Ok();
    }
}
