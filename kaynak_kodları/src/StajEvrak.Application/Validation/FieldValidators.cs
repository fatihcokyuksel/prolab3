using System.Text.RegularExpressions;
using StajEvrak.Application.Common;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Application.Validation;

/// <summary>Bir alan değerinin doğrulama sonucu: geçerliyse standart biçimi, değilse hata mesajı.</summary>
public sealed record FieldValidation(bool IsValid, string? Value, string? Error)
{
    public static FieldValidation Valid(string? value) => new(true, value, null);
    public static FieldValidation Invalid(string error) => new(false, null, error);
}

/// <summary>
/// Öğrencinin elle girdiği Ek-16 alanlarını türüne göre doğrulayıp standart biçime getiren soyut sınıf.
/// Boş değer her zaman kabul edilir (form parça parça kaydedilebilir); zorunlu alan kontrolü karşılaştırma raporundadır.
/// </summary>
public abstract class FieldValidator
{
    /// <param name="form">Formdaki diğer alanlar; kuralı başka bir alana bağlı olan doğrulayıcılar kullanır (örn. kimlik no → uyruk).</param>
    public FieldValidation Validate(string? value, IReadOnlyDictionary<string, string?>? form = null)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? FieldValidation.Valid(null) : ValidateValue(text, form ?? new Dictionary<string, string?>());
    }

    protected abstract FieldValidation ValidateValue(string value);

    protected virtual FieldValidation ValidateValue(string value, IReadOnlyDictionary<string, string?> form) => ValidateValue(value);

    public static FieldValidator For(Ek16FieldDefinition field) => field.Kind switch
    {
        FieldKind.NationalId => new NationalIdValidator(),
        FieldKind.Phone => new PhoneValidator(),
        FieldKind.Email => new EmailValidator(),
        FieldKind.Number => new NumberValidator(field.Code == Ek16Fields.OgrenciNo ? 15 : 6),
        FieldKind.Date => new DateValidator(),
        FieldKind.YesNo => new OptionValidator(["E", "H"]),
        FieldKind.Choice => new OptionValidator(["1", "2", "3", "4", "5", "6"]),
        _ => new TextValidator()
    };
}

public sealed class TextValidator : FieldValidator
{
    public const int MaxLength = 300;

    protected override FieldValidation ValidateValue(string value) =>
        value.Length > MaxLength
            ? FieldValidation.Invalid($"En fazla {MaxLength} karakter yazılabilir.")
            : FieldValidation.Valid(value);
}

/// <summary>
/// Kimlik numarası, formdaki uyruk alanına göre doğrulanır (proje dokümanı 6.2).
/// Kuralların kendisi <see cref="NationalIdRules"/> içindedir; veri seti içe aktarma da aynı kuralları kullanır.
/// </summary>
public sealed class NationalIdValidator : FieldValidator
{
    public const int Length = NationalIdRules.Length;

    protected override FieldValidation ValidateValue(string value) => ValidateValue(value, new Dictionary<string, string?>());

    protected override FieldValidation ValidateValue(string value, IReadOnlyDictionary<string, string?> form)
    {
        form.TryGetValue(Ek16Fields.Uyruk, out var nationality);
        var error = NationalIdRules.Validate(value, nationality, checkAlgorithm: true);
        return error is null ? FieldValidation.Valid(value) : FieldValidation.Invalid(error);
    }
}

/// <summary>
/// T.C. / yabancı kimlik numarası kuralları (proje dokümanı 6.2):
/// T.C. uyruklu: 11 hane, yalnızca rakam, ilk hane 0 olamaz (+ isteğe bağlı kontrol hanesi algoritması).
/// Yabancı uyruklu: 11 hane ve 99 ile başlamalı.
/// </summary>
public static class NationalIdRules
{
    public const int Length = 11;
    private static readonly HashSet<string> TurkishKeys = ["tc", "turkiye", "turk", "turkiyecumhuriyeti", "tcvatandasi"];

    /// <summary>Uyruk boşsa veya T.C./Türkiye yazılmışsa T.C. uyruklu kabul edilir.</summary>
    public static bool IsTurkish(string? nationality)
    {
        var key = TextHelper.NormalizeKey(nationality);
        return key.Length == 0 || TurkishKeys.Contains(key);
    }

    /// <returns>Geçerliyse null, değilse hata mesajı.</returns>
    public static string? Validate(string? id, string? nationality, bool checkAlgorithm)
    {
        var value = id?.Trim() ?? string.Empty;
        if (!value.All(char.IsDigit)) return "Kimlik numarası yalnızca rakamlardan oluşmalıdır.";
        if (value.Length != Length) return $"Kimlik numarası {Length} haneli olmalıdır ({value.Length} hane girildi).";

        if (!IsTurkish(nationality))
            return value.StartsWith("99") ? null : "Yabancı uyruklu öğrencilerin kimlik numarası 99 ile başlamalıdır.";

        if (value[0] == '0') return "T.C. kimlik numarası 0 ile başlayamaz.";
        if (checkAlgorithm && !HasValidChecksum(value)) return "T.C. kimlik numarası geçerli değil; lütfen rakamları kontrol edin.";
        return null;
    }

    public static bool HasValidChecksum(string id)
    {
        if (id.Length != Length || !id.All(char.IsDigit)) return false;
        var d = id.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var tenth = ((odd * 7 - even) % 10 + 10) % 10;
        var eleventh = d.Take(10).Sum() % 10;
        return d[9] == tenth && d[10] == eleventh;
    }
}

public sealed class PhoneValidator : FieldValidator
{
    protected override FieldValidation ValidateValue(string value) =>
        PhoneNumber.TryParse(value, out var phone, out var error)
            ? FieldValidation.Valid(phone!.ToString())
            : FieldValidation.Invalid(error!);
}

public sealed partial class EmailValidator : FieldValidator
{
    protected override FieldValidation ValidateValue(string value)
    {
        var email = value.ToLowerInvariant();
        return email.Length <= 200 && EmailRegex().IsMatch(email)
            ? FieldValidation.Valid(email)
            : FieldValidation.Invalid("Geçerli bir e-posta adresi yazın (örn. ad@alanadi.uzanti).");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailRegex();
}

public sealed class NumberValidator(int maxDigits) : FieldValidator
{
    protected override FieldValidation ValidateValue(string value)
    {
        if (!value.All(char.IsDigit)) return FieldValidation.Invalid("Yalnızca rakam yazılabilir.");
        if (value.Length > maxDigits) return FieldValidation.Invalid($"En fazla {maxDigits} haneli olabilir.");
        return FieldValidation.Valid(value);
    }
}

public sealed class DateValidator : FieldValidator
{
    protected override FieldValidation ValidateValue(string value) =>
        TextHelper.ParseDate(value) is { } date
            ? FieldValidation.Valid(date.ToString("dd.MM.yyyy"))
            : FieldValidation.Invalid("Geçerli bir tarih seçin.");
}

/// <summary>Yalnızca listedeki seçeneklerden biri kabul edilir (Evet/Hayır, sağlık statüsü 1-6).</summary>
public sealed class OptionValidator(IReadOnlyCollection<string> options) : FieldValidator
{
    protected override FieldValidation ValidateValue(string value) =>
        options.Contains(value) ? FieldValidation.Valid(value) : FieldValidation.Invalid("Listeden bir seçenek seçin.");
}
