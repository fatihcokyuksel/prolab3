using System.Net;
using Microsoft.AspNetCore.Html;
using StajEvrak.Application.Comparison;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Web.Infrastructure;

/// <summary>Bir rapor hücresinin arayüzdeki görünümü.</summary>
public sealed record ReportCell(string CellCss, string ValueCss, string Value, IHtmlContent? Note, bool QuietNote);

/// <summary>
/// Karşılaştırma raporunu Ek-16 formu düzeninde göstermek için yardımcılar:
/// form satırları, hücre notları ("Okul kaydında X") ve özet cümle.
/// </summary>
public static class ReportPresenter
{
    public static readonly IReadOnlyDictionary<string, string> HealthOptions = new Dictionary<string, string>
    {
        ["1"] = "1 - Genel sağlık sigortası kapsamındayım",
        ["2"] = "2 - Anne-baba üzerinden sağlık yardımı alıyorum",
        ["3"] = "3 - SSK'ya tabi çalıştığım için kendi sağlık yardımım var",
        ["4"] = "4 - Bağ-Kur'a tabi çalıştığım için kendi sağlık yardımım var",
        ["5"] = "5 - Emekli Sandığı'na tabi çalıştığım için kendi sağlık yardımım var",
        ["6"] = "6 - Sosyal güvencem yok (25 yaş üstü)"
    };

    private static readonly HashSet<string> WideFields = [Ek16Fields.Adres, Ek16Fields.FirmaAdres, Ek16Fields.SaglikStatusu];

    public static bool IsWide(string code) => WideFields.Contains(code);

    /// <summary>Bölümdeki alanları kâğıt formdaki gibi satır başına iki alan olacak şekilde gruplar.</summary>
    public static IEnumerable<IReadOnlyList<ComparisonRow>> FormRows(ComparisonReport report, Ek16Section section)
    {
        var buffer = new List<ComparisonRow>();
        foreach (var row in report.Rows.Where(r => r.Field.Section == section))
        {
            if (IsWide(row.Field.Code))
            {
                if (buffer.Count > 0) { yield return buffer; buffer = []; }
                yield return [row];
                continue;
            }
            buffer.Add(row);
            if (buffer.Count == 2) { yield return buffer; buffer = []; }
        }
        if (buffer.Count > 0) yield return buffer;
    }

    public static string Display(Ek16FieldDefinition field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return field.Kind switch
        {
            FieldKind.YesNo => new YesNoFieldComparer().Normalize(value) switch { "E" => "Evet", "H" => "Hayır", _ => value },
            FieldKind.Choice => HealthOptions.TryGetValue(value.Trim(), out var text) ? text : value,
            _ => value
        };
    }

    public static ReportCell Cell(ComparisonReport report, ComparisonRow row)
    {
        var field = row.Field;
        var primary = report.DocumentRead ? row.DocumentValue : row.UserValue;
        var shown = Display(field, primary);

        switch (row.Status)
        {
            case ComparisonStatus.Mismatch:
                return new ReportCell("bad", primary is null ? "v-none" : "v-bad", primary is null ? "—" : shown, MismatchNote(report, row, primary), false);

            case ComparisonStatus.Missing:
                return new ReportCell("empty", "v-empty", "Boş", new HtmlString("Bu alan zorunlu"), false);

            // Karşılaştırılamayan alanlar (tek kaynakta değer var veya hiç yok): sessizce değer gösterilir,
            // kaç alanın karşılaştırılamadığı özet cümlesinde bir kez söylenir.
            case ComparisonStatus.Warning:
            case ComparisonStatus.NotChecked:
                var fallback = primary ?? row.UserValue ?? row.DatasetValue;
                return fallback is null
                    ? new ReportCell("", "v-none", "—", null, false)
                    : new ReportCell("", primary is null ? "v-none" : "", Display(field, fallback), null, false);

            default:
                return new ReportCell("", "", shown, null, false);
        }
    }

    /// <summary>"Okul kaydında X" / "Girdiğiniz bilgide X" notunu oluşturur.</summary>
    private static IHtmlContent MismatchNote(ComparisonReport report, ComparisonRow row, string? primary)
    {
        var field = row.Field;
        var comparer = FieldComparer.For(field.Kind);
        string B(string? v) => $"<b>{WebUtility.HtmlEncode(Display(field, v))}</b>";

        if (primary is null)
        {
            // Belgede/girişte değer yok ama diğer iki kaynak birbiriyle uyuşmuyor.
            return new HtmlString($"Girdiğiniz bilgi {B(row.UserValue)}, okul kaydı {B(row.DatasetValue)}");
        }

        var dataset = row.DatasetValue is not null && !comparer.AreEqual(primary, row.DatasetValue) ? row.DatasetValue : null;
        var user = report.DocumentRead && row.UserValue is not null && !comparer.AreEqual(primary, row.UserValue) ? row.UserValue : null;

        if (dataset is not null && user is not null)
        {
            return comparer.AreEqual(dataset, user)
                ? new HtmlString($"Okul kaydında ve girdiğiniz bilgide {B(dataset)}")
                : new HtmlString($"Okul kaydında {B(dataset)}, girdiğiniz bilgide {B(user)}");
        }
        if (dataset is not null) return new HtmlString($"Okul kaydında {B(dataset)}");
        if (user is not null) return new HtmlString($"Girdiğiniz bilgide {B(user)}");

        // Belgeyle uyumlu ama girilen bilgi ile okul kaydı birbirini tutmuyor.
        return new HtmlString($"Girdiğiniz bilgi {B(row.UserValue)}, okul kaydı {B(row.DatasetValue)}");
    }

    /// <summary>Raporun tek cümlelik özeti.</summary>
    public static string Summary(ComparisonReport report)
    {
        var parts = new List<string>();
        if (report.DocumentRead)
        {
            var read = report.Rows.Count(r => r.DocumentValue is not null);
            parts.Add($"Belgeden {read} alan okundu.");
        }
        else
        {
            parts.Add("Belge otomatik okunamadı.");
        }

        var problems = new List<string>();
        if (report.MismatchCount > 0) problems.Add($"{report.MismatchCount} alan uyuşmuyor");
        if (report.MissingCount > 0) problems.Add($"{report.MissingCount} zorunlu alan boş");

        if (problems.Count > 0)
            parts.Add(Capitalize(string.Join(", ", problems)) + ".");
        else if (report.MatchCount > 0)
            parts.Add($"Karşılaştırılan {report.MatchCount} alanda uyuşmazlık yok.");
        else
            parts.Add("Karşılaştırılabilecek bilgi yok; öğrenci Ek-16 bilgilerini henüz girmemiş.");

        if (report.WarningCount > 0 && report.MatchCount + report.MismatchCount > 0)
            parts.Add($"{report.WarningCount} alan yalnızca tek kaynakta bulunduğu için karşılaştırılamadı.");
        return string.Join(" ", parts);
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], TextHelperCulture) + s[1..];
    private static readonly System.Globalization.CultureInfo TextHelperCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    public static bool HasProblems(ComparisonReport report) => report.MismatchCount > 0 || report.MissingCount > 0;

    public static string VerdictCss(ComparisonReport report) =>
        HasProblems(report) ? "" : report.DocumentRead ? "ok" : "info";
}
