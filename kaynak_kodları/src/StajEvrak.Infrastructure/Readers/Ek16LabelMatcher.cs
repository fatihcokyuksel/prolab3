using StajEvrak.Application.Common;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Infrastructure.Readers;

/// <summary>Word tablosundaki etiket hücresi metnini Ek-16 alan tanımlarıyla eşleştirir.</summary>
internal static class Ek16LabelMatcher
{
    /// <summary>
    /// Etiket metnine uyan alanları döndürür. En uzun eşleşen etiket seçilir
    /// (ör. "Adı Soyadı" hücresi "Adı" yerine "Adı Soyadı" alanına eşlenir).
    /// </summary>
    public static IReadOnlyList<Ek16FieldDefinition> Match(string cellText, Ek16Section section)
    {
        var key = TextHelper.NormalizeKey(cellText);
        if (key.Length == 0) return [];

        var candidates = Ek16Fields.InSection(section)
            .Where(f => key.StartsWith(TextHelper.NormalizeKey(f.DocxLabel), StringComparison.Ordinal))
            .ToList();
        if (candidates.Count == 0) return [];

        var longest = candidates.Max(f => TextHelper.NormalizeKey(f.DocxLabel).Length);
        return candidates.Where(f => TextHelper.NormalizeKey(f.DocxLabel).Length == longest).ToList();
    }

    /// <summary>Tablodaki etiketlere bakarak tablonun hangi bölüme ait olduğunu tahmin eder.</summary>
    public static Ek16Section? DetectSection(IEnumerable<string> cellTexts)
    {
        var texts = cellTexts.ToList();
        var scores = Enum.GetValues<Ek16Section>()
            .Select(s => (Section: s, Score: texts.Count(t => Match(t, s).Count > 0)))
            .OrderByDescending(x => x.Score)
            .ToList();
        return scores[0].Score >= 2 ? scores[0].Section : null;
    }
}
