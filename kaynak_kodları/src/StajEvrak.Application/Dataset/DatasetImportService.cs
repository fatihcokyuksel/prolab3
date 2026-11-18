using StajEvrak.Application.Abstractions;
using StajEvrak.Domain.Dataset;

namespace StajEvrak.Application.Dataset;

public sealed record DatasetImportResult(
    bool Succeeded,
    string? Error,
    int TotalRows,
    int Added,
    int Updated,
    int LinkedStudents,
    IReadOnlyList<DatasetRowError> RowErrors);

/// <summary>Admin'in yüklediği veri setini (xlsx/csv) okuyup veritabanına aktarır (TC kimlik no'ya göre ekle/güncelle).</summary>
public sealed class DatasetImportService(
    IEnumerable<DatasetParser> parsers,
    IDatasetRepository dataset,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public IReadOnlyCollection<string> SupportedExtensions => parsers.SelectMany(p => p.SupportedExtensions).ToList();

    public async Task<DatasetImportResult> ImportAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var parser = parsers.FirstOrDefault(p => p.Supports(extension));
        if (parser is null)
            return Failed($"'{extension}' formatı desteklenmiyor. Desteklenen: {string.Join(", ", SupportedExtensions)}");

        var parsed = parser.Parse(content);
        if (parsed.FatalError is not null) return Failed(parsed.FatalError);

        var now = clock.UtcNow;
        var existing = await dataset.GetByTcNumbersAsync(parsed.Rows.Select(r => r.TcKimlikNo), ct);
        int added = 0, updated = 0;

        foreach (var row in parsed.Rows)
        {
            if (existing.TryGetValue(row.TcKimlikNo, out var record))
            {
                record.Update(row, now);
                updated++;
            }
            else
            {
                dataset.Add(new DatasetStudent(row, now));
                added++;
            }
        }
        await unitOfWork.SaveChangesAsync(ct);

        // Daha önce kayıt olmuş ama veri setiyle eşleşmemiş öğrencileri bağla.
        var linked = 0;
        foreach (var student in await users.GetUnlinkedStudentsAsync(ct))
        {
            var match = await dataset.FindByStudentNoOrEmailAsync(student.StudentNo, student.Email, ct);
            if (match is null) continue;
            student.LinkToDataset(match);
            linked++;
        }
        if (linked > 0) await unitOfWork.SaveChangesAsync(ct);

        return new DatasetImportResult(true, null, parsed.TotalRows, added, updated, linked, parsed.Errors);
    }

    private static DatasetImportResult Failed(string error) => new(false, error, 0, 0, 0, 0, []);
}
