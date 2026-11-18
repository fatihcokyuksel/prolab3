using StajEvrak.Application.Common;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Application.Comparison;

/// <summary>
/// Alan değerlerini türüne uygun biçimde karşılaştıran soyut sınıf.
/// Her alt sınıf yalnızca kendi normalleştirme kuralını tanımlar (çok biçimlilik).
/// </summary>
public abstract class FieldComparer
{
    /// <summary>Değeri karşılaştırılabilir kanonik biçime getirir. Anlamsızsa null döner.</summary>
    public abstract string? Normalize(string? value);

    public bool AreEqual(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    public static FieldComparer For(FieldKind kind) => kind switch
    {
        FieldKind.Number or FieldKind.NationalId or FieldKind.Choice => new NumberFieldComparer(),
        FieldKind.Date => new DateFieldComparer(),
        FieldKind.Email => new EmailFieldComparer(),
        FieldKind.Phone => new PhoneFieldComparer(),
        FieldKind.YesNo => new YesNoFieldComparer(),
        FieldKind.AcademicYear => new AcademicYearFieldComparer(),
        _ => new TextFieldComparer()
    };
}

/// <summary>
/// Öğretim yılını "YYYY-YYYY" aralığına göre karşılaştırır. Veri setinde yalnızca yıl ("2025-2026"),
/// belgede yıl + yarıyıl ("2025-2026 Bahar") bulunabildiği için yarıyıl eki karşılaştırmaya katılmaz.
/// </summary>
public sealed partial class AcademicYearFieldComparer : FieldComparer
{
    public override string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = YearRange().Match(value);
        return match.Success ? $"{match.Groups[1].Value}-{match.Groups[2].Value}" : TextHelper.NormalizeText(value);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d{4})\s*[-/–]\s*(\d{4})")]
    private static partial System.Text.RegularExpressions.Regex YearRange();
}

/// <summary>Büyük/küçük harf (Türkçe) ve fazla boşluk farklarını yok sayar.</summary>
public class TextFieldComparer : FieldComparer
{
    public override string? Normalize(string? value)
    {
        var text = TextHelper.NormalizeText(value);
        return text.Length == 0 ? null : text;
    }
}

public sealed class EmailFieldComparer : TextFieldComparer
{
    public override string? Normalize(string? value) => base.Normalize(value?.Replace(" ", string.Empty));
}

/// <summary>Yalnızca rakamları karşılaştırır (TC no, öğrenci no, gün sayısı...).</summary>
public sealed class NumberFieldComparer : FieldComparer
{
    public override string? Normalize(string? value)
    {
        var digits = TextHelper.DigitsOnly(value);
        return digits.Length == 0 ? null : digits;
    }
}

/// <summary>Telefonları son 10 haneye göre karşılaştırır (+90 / 0 öneki farkı yok sayılır).</summary>
public sealed class PhoneFieldComparer : FieldComparer
{
    public override string? Normalize(string? value)
    {
        var digits = TextHelper.DigitsOnly(value);
        if (digits.Length == 0) return null;
        return digits.Length > 10 ? digits[^10..] : digits;
    }
}

/// <summary>Farklı tarih yazımlarını (01.02.2026, 1/2/2026, 2026-02-01) aynı kabul eder.</summary>
public sealed class DateFieldComparer : FieldComparer
{
    public override string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var date = TextHelper.ParseDate(value);
        return date?.ToString("yyyy-MM-dd") ?? TextHelper.NormalizeText(value);
    }
}

/// <summary>E / Evet / H / Hayır değerlerini eşler.</summary>
public sealed class YesNoFieldComparer : FieldComparer
{
    public override string? Normalize(string? value)
    {
        var text = TextHelper.NormalizeText(value);
        if (text.Length == 0) return null;
        return text.StartsWith('E') ? "E" : text.StartsWith('H') ? "H" : text;
    }
}
