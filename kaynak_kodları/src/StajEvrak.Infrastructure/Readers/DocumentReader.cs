using System.Text.RegularExpressions;
using StajEvrak.Application.Documents;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Infrastructure.Readers;

/// <summary>
/// Belge okuyucuların soyut atası (Template Method): hata yakalama ve sonuç üretimi burada,
/// asıl alan çıkarma işi alt sınıflarda (<see cref="Extract"/>).
/// </summary>
public abstract partial class DocumentReader : IDocumentReader
{
    public abstract IReadOnlyCollection<string> SupportedExtensions { get; }

    public virtual DocumentReadResult Read(string fullPath)
    {
        try
        {
            var fields = Extract(fullPath);
            var filled = fields.Count(f => !string.IsNullOrWhiteSpace(f.Value));
            var note = filled == 0
                ? "Belgede Ek-16 alanları bulunamadı veya tüm alanlar boş. Doğru formu yüklediğinizden emin olun."
                : $"Belgeden {filled} alan otomatik okundu.";
            return new DocumentReadResult(true, fields, note);
        }
        catch (Exception ex)
        {
            return DocumentReadResult.NotSupported($"Belge otomatik okunamadı: {ex.Message}");
        }
    }

    protected abstract Dictionary<string, string?> Extract(string fullPath);

    /// <summary>
    /// Bir etiketin değerini ilgili alan(lar)a yazar. Aynı hücrede birden çok değer varsa
    /// (TC/Uyruk, E-posta/Telefon) parçalara ayırır.
    /// </summary>
    protected static void Assign(Dictionary<string, string?> target, IReadOnlyList<Ek16FieldDefinition> fields, string? rawValue)
    {
        var value = CleanValue(rawValue);
        if (fields.Count == 1)
        {
            target[fields[0].Code] = fields[0].Kind == FieldKind.Choice ? ParseChoice(rawValue) : value;
            return;
        }

        var ordered = fields.OrderBy(f => f.DocxPart ?? 0).ToList();
        if (value is null)
        {
            foreach (var f in ordered) target[f.Code] = null;
            return;
        }

        var emailField = ordered.FirstOrDefault(f => f.Kind == FieldKind.Email);
        if (emailField is not null)
        {
            var match = EmailRegex().Match(value);
            target[emailField.Code] = match.Success ? match.Value : null;
            var rest = match.Success ? value.Remove(match.Index, match.Length) : value;
            foreach (var other in ordered.Where(f => f != emailField))
                target[other.Code] = CleanValue(rest.Trim(' ', '/', ',', ';', '-', '\n', '\r', '\t'));
            return;
        }

        var parts = value.Split(['/', '\n'], 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < ordered.Count; i++)
            target[ordered[i].Code] = i < parts.Length ? CleanValue(parts[i]) : null;
    }

    /// <summary>Boşluk, nokta, alt çizgi gibi doldurma karakterlerinden oluşan değerleri boş kabul eder.</summary>
    protected static string? CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().Trim(':').Trim();
        return trimmed.Any(char.IsLetterOrDigit) ? WhiteSpace().Replace(trimmed, " ") : null;
    }

    /// <summary>Sağlık statüsü: "3-☒" gibi işaretlenmiş seçeneği veya doğrudan yazılmış rakamı bulur.</summary>
    protected static string? ParseChoice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var marked = MarkedOption().Match(value);
        if (marked.Success) return marked.Groups[1].Value;
        var plain = value.Trim();
        return plain.Length == 1 && plain[0] is >= '1' and <= '6' ? plain : null;
    }

    [GeneratedRegex(@"[^\s/@,;]+@[^\s/@,;]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"([1-6])\s*-\s*[☒☑✓✔✗xX■]")]
    private static partial Regex MarkedOption();
}
