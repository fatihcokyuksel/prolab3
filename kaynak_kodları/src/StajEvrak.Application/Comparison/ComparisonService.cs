using StajEvrak.Domain.Ek16;

namespace StajEvrak.Application.Comparison;

public enum ComparisonStatus
{
    Match,
    Mismatch,
    Missing,
    Warning,
    NotChecked
}

public sealed record ComparisonRow(
    Ek16FieldDefinition Field,
    string? DocumentValue,
    string? UserValue,
    string? DatasetValue,
    ComparisonStatus Status,
    IReadOnlyList<string> Messages);

public sealed record ComparisonReport(
    IReadOnlyList<ComparisonRow> Rows,
    bool DocumentRead,
    bool HasDatasetRecord,
    string? DocumentNote)
{
    public int MatchCount => Rows.Count(r => r.Status == ComparisonStatus.Match);
    public int MismatchCount => Rows.Count(r => r.Status == ComparisonStatus.Mismatch);
    public int MissingCount => Rows.Count(r => r.Status == ComparisonStatus.Missing);
    public int WarningCount => Rows.Count(r => r.Status == ComparisonStatus.Warning);
    public bool IsClean => MismatchCount == 0 && MissingCount == 0;
}

/// <summary>
/// Üç kaynağı alan alan karşılaştırır: belgeden okunan, öğrencinin sisteme girdiği ve admin veri seti.
/// </summary>
public sealed class ComparisonService
{
    public ComparisonReport Build(
        IReadOnlyDictionary<string, string?>? documentValues,
        bool documentRead,
        string? documentNote,
        IReadOnlyDictionary<string, string?>? userValues,
        IReadOnlyDictionary<string, string?>? datasetValues)
    {
        var rows = new List<ComparisonRow>();

        foreach (var field in Ek16Fields.All)
        {
            var comparer = FieldComparer.For(field.Kind);
            var doc = documentRead ? Get(documentValues, field.Code) : null;
            var user = Get(userValues, field.Code);
            var datasetHasField = datasetValues?.ContainsKey(field.Code) == true;
            var dataset = datasetHasField ? Get(datasetValues, field.Code) : null;

            var messages = new List<string>();
            var mismatch = false;

            void Compare(string? a, string? b, string label)
            {
                if (a is null || b is null) return;
                if (!comparer.AreEqual(a, b))
                {
                    mismatch = true;
                    messages.Add($"{label} uyuşmuyor");
                }
            }

            Compare(doc, user, "Belge ↔ Girilen bilgi");
            Compare(doc, dataset, "Belge ↔ Veri seti");
            Compare(user, dataset, "Girilen bilgi ↔ Veri seti");

            if (documentRead && doc is null && field.Required) messages.Add("Belgede boş");
            if (user is null && field.Required) messages.Add("Sisteme girilmemiş");
            if (datasetValues is not null && datasetHasField && dataset is null) messages.Add("Veri setinde boş");

            var available = new[] { doc, user, dataset }.Count(v => v is not null);
            ComparisonStatus status;
            if (mismatch) status = ComparisonStatus.Mismatch;
            // "Eksik" yalnızca belge okunduysa ve belgede boşsa anlamlıdır; belge okunamadıysa alan karşılaştırılamaz.
            else if (available == 0) status = field.Required && documentRead ? ComparisonStatus.Missing : ComparisonStatus.NotChecked;
            else if (documentRead && doc is null && field.Required) status = ComparisonStatus.Missing;
            else if (available == 1 || (user is null && field.Required)) status = ComparisonStatus.Warning;
            else status = ComparisonStatus.Match;

            rows.Add(new ComparisonRow(field, doc, user, dataset, status, messages));
        }

        return new ComparisonReport(rows, documentRead, datasetValues is not null, documentNote);
    }

    private static string? Get(IReadOnlyDictionary<string, string?>? values, string code) =>
        values is not null && values.TryGetValue(code, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}
