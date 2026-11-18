using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StajEvrak.Application.Common;

/// <summary>Türkçe metin karşılaştırma ve tarih ayrıştırma yardımcıları.</summary>
public static partial class TextHelper
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy",
        "dd.MM.yyyy HH:mm:ss", "d.M.yyyy HH:mm:ss", "M/d/yyyy", "M/d/yyyy h:mm:ss tt"
    ];

    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (DateTime.TryParseExact(v, DateFormats, Turkish, DateTimeStyles.None, out var dt) ||
            DateTime.TryParseExact(v, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return DateOnly.FromDateTime(dt);
        return null;
    }

    /// <summary>Türkçe büyük harfe çevirir ve fazla boşlukları tekler.</summary>
    public static string NormalizeText(string? value) =>
        MultiSpace().Replace((value ?? string.Empty).Trim().ToUpper(Turkish), " ");

    public static string DigitsOnly(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Sütun başlıklarını karşılaştırmak için: Türkçe karakterleri sadeleştirir, harf/rakam dışını atar.</summary>
    public static string NormalizeKey(string? value)
    {
        var sb = new StringBuilder();
        foreach (var ch in (value ?? string.Empty).ToLower(Turkish))
        {
            var mapped = ch switch
            {
                'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'İ' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                _ => ch
            };
            if (char.IsLetterOrDigit(mapped)) sb.Append(mapped);
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}
