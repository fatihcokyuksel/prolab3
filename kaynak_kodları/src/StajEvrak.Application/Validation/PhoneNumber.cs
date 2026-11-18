using StajEvrak.Application.Common;

namespace StajEvrak.Application.Validation;

/// <summary>Ülke telefon kodu ve o ülkedeki ulusal numaranın izin verilen hane aralığı.</summary>
public sealed record CountryCode(string Code, string Country, int MinDigits, int MaxDigits);

/// <summary>
/// Telefon numaralarının tek standardı: "+{ülke kodu} {ulusal numara}".
/// Türkiye için ulusal numara 10 hanedir ve "532 123 45 67" biçiminde gruplanır.
/// </summary>
public sealed record PhoneNumber(CountryCode Country, string NationalNumber)
{
    public const string DefaultCode = "90";

    /// <summary>Formda seçilebilen ülke kodları (Türkiye ilk sırada, diğerleri ada göre).</summary>
    public static readonly IReadOnlyList<CountryCode> Countries = BuildCountries();

    private static IReadOnlyList<CountryCode> BuildCountries()
    {
        CountryCode[] list =
        [
            new("90", "Türkiye", 10, 10),
            new("49", "Almanya", 7, 12),
            new("1", "ABD / Kanada", 10, 10),
            new("355", "Arnavutluk", 8, 9),
            new("61", "Avustralya", 9, 9),
            new("43", "Avusturya", 7, 13),
            new("994", "Azerbaycan", 9, 9),
            new("971", "Birleşik Arap Emirlikleri", 8, 9),
            new("32", "Belçika", 8, 9),
            new("387", "Bosna-Hersek", 8, 9),
            new("55", "Brezilya", 10, 11),
            new("359", "Bulgaristan", 8, 9),
            new("213", "Cezayir", 9, 9),
            new("420", "Çekya", 9, 9),
            new("86", "Çin", 11, 11),
            new("45", "Danimarka", 8, 8),
            new("212", "Fas", 9, 9),
            new("358", "Finlandiya", 6, 11),
            new("33", "Fransa", 9, 9),
            new("995", "Gürcistan", 9, 9),
            new("82", "Güney Kore", 9, 10),
            new("91", "Hindistan", 10, 10),
            new("31", "Hollanda", 9, 9),
            new("964", "Irak", 10, 10),
            new("44", "İngiltere", 10, 10),
            new("98", "İran", 10, 10),
            new("353", "İrlanda", 7, 9),
            new("34", "İspanya", 9, 9),
            new("46", "İsveç", 7, 10),
            new("41", "İsviçre", 9, 9),
            new("39", "İtalya", 6, 11),
            new("81", "Japonya", 9, 10),
            new("974", "Katar", 8, 8),
            new("7", "Rusya / Kazakistan", 10, 10),
            new("996", "Kırgızistan", 9, 9),
            new("383", "Kosova", 8, 8),
            new("965", "Kuveyt", 8, 8),
            new("218", "Libya", 9, 9),
            new("961", "Lübnan", 7, 8),
            new("36", "Macaristan", 8, 9),
            new("389", "Kuzey Makedonya", 8, 8),
            new("20", "Mısır", 10, 10),
            new("47", "Norveç", 8, 8),
            new("998", "Özbekistan", 9, 9),
            new("92", "Pakistan", 10, 10),
            new("48", "Polonya", 9, 9),
            new("351", "Portekiz", 9, 9),
            new("40", "Romanya", 9, 9),
            new("966", "Suudi Arabistan", 9, 9),
            new("963", "Suriye", 9, 9),
            new("216", "Tunus", 8, 8),
            new("993", "Türkmenistan", 8, 8),
            new("380", "Ukrayna", 9, 9),
            new("962", "Ürdün", 8, 9),
            new("30", "Yunanistan", 10, 10),
        ];
        return list;
    }

    private static readonly Dictionary<string, CountryCode> ByCode = Countries.ToDictionary(c => c.Code);

    public static CountryCode? FindCountry(string? code) =>
        code is not null && ByCode.TryGetValue(TextHelper.DigitsOnly(code), out var c) ? c : null;

    /// <summary>Saklanan biçim: "+90 532 123 45 67" (diğer ülkeler: "+49 15123456789").</summary>
    public override string ToString() => $"+{Country.Code} {FormatNational()}";

    private string FormatNational() =>
        Country.Code == DefaultCode && NationalNumber.Length == 10
            ? $"{NationalNumber[..3]} {NationalNumber[3..6]} {NationalNumber[6..8]} {NationalNumber[8..]}"
            : NationalNumber;

    /// <summary>
    /// Formdaki ülke kodu düğmesi ve numara kutusu için değeri iki parçaya ayırır.
    /// Geçersiz bir değer olsa bile kullanıcının yazdığı rakamlar korunur.
    /// </summary>
    public static (CountryCode Country, string National) Split(string? value)
    {
        if (TryParse(value, out var phone, out _)) return (phone!.Country, phone.NationalNumber);
        var text = value?.Trim() ?? string.Empty;
        var digits = TextHelper.DigitsOnly(text);
        if (text.StartsWith('+'))
        {
            foreach (var n in new[] { 3, 2, 1 })
                if (digits.Length >= n && FindCountry(digits[..n]) is { } c) return (c, digits[n..]);
        }
        return (ByCode[DefaultCode], digits);
    }

    /// <summary>
    /// "+90 532 123 45 67", "0532 123 45 67", "5321234567" gibi yazımları çözümler.
    /// Ülke kodu yazılmamışsa Türkiye kabul edilir.
    /// </summary>
    public static bool TryParse(string? value, out PhoneNumber? phone, out string? error)
    {
        phone = null;
        error = null;
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0) { error = "Telefon numarası boş."; return false; }
        if (text.Any(ch => !(char.IsDigit(ch) || ch is ' ' or '+' or '-' or '(' or ')' or '.')))
        {
            error = "Telefon numarası yalnızca rakamlardan oluşmalıdır.";
            return false;
        }

        CountryCode country;
        string national;
        if (text.StartsWith('+') || text.StartsWith("00"))
        {
            var digits = TextHelper.DigitsOnly(text);
            if (text.StartsWith("00")) digits = digits[2..];
            // Ülke kodları önek-serbest olduğundan en uzun eşleşme aranır (1-3 hane).
            var match = Enumerable.Range(1, 3).Reverse()
                .Where(n => digits.Length > n)
                .Select(n => FindCountry(digits[..n]))
                .FirstOrDefault(c => c is not null);
            if (match is null) { error = "Ülke telefon kodu tanınmadı."; return false; }
            country = match;
            national = digits[country.Code.Length..];
        }
        else
        {
            country = ByCode[DefaultCode];
            national = TextHelper.DigitsOnly(text);
        }

        // Ulusal numaranın başındaki 0 (yurt içi arama öneki) standart biçime dahil edilmez.
        if (national.StartsWith('0') && national.Length == country.MaxDigits + 1) national = national[1..];

        if (national.StartsWith('0'))
        {
            error = $"Numara 0 ile başlamamalı; +{country.Code} kodundan sonraki kısmı yazın.";
            return false;
        }
        if (national.Length < country.MinDigits || national.Length > country.MaxDigits)
        {
            error = country.MinDigits == country.MaxDigits
                ? $"{country.Country} (+{country.Code}) numaraları ülke kodu hariç {country.MaxDigits} haneli olmalıdır."
                : $"{country.Country} (+{country.Code}) numaraları ülke kodu hariç {country.MinDigits}-{country.MaxDigits} haneli olmalıdır.";
            return false;
        }

        phone = new PhoneNumber(country, national);
        return true;
    }
}
