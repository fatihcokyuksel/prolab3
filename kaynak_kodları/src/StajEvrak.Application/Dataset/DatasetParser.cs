using StajEvrak.Application.Validation;
using StajEvrak.Application.Common;
using StajEvrak.Domain.Dataset;

namespace StajEvrak.Application.Dataset;

public sealed record DatasetRowError(int RowNumber, string Message);

public sealed record DatasetParseResult(
    IReadOnlyList<DatasetStudentData> Rows,
    IReadOnlyList<DatasetRowError> Errors,
    int TotalRows,
    string? FatalError = null)
{
    public static DatasetParseResult Fatal(string error) => new([], [], 0, error);
}

/// <summary>
/// Veri seti ayrıştırıcıların soyut atası (Template Method deseni).
/// Satırları okuma işi alt sınıflara bırakılır (Excel, CSV); başlık eşleme ve satır doğrulama
/// bu sınıfta tek bir yerde yapılır.
/// </summary>
public abstract class DatasetParser
{
    /// <summary>Veri setinde bulunması gereken sütunlar ve kabul edilen başlık adları.</summary>
    private static readonly Dictionary<DatasetColumn, string[]> HeaderAliases = new()
    {
        [DatasetColumn.AdSoyad] = ["Ad Soyad", "Öğrenci Ad Soyad", "Adı Soyadı", "AdSoyad"],
        [DatasetColumn.TcKimlikNo] = ["TC Kimlik No", "T.C. Kimlik No", "TC", "TC No", "TcKimlikNo", "Kimlik No"],
        [DatasetColumn.Uyruk] = ["Uyruk", "Uyruğu"],
        [DatasetColumn.OgrenciNo] = ["Öğrenci No", "Öğrenci Numarası", "OgrenciNo", "Numara"],
        [DatasetColumn.DogumTarihi] = ["Doğum Tarihi", "DogumTarihi"],
        [DatasetColumn.Adres] = ["İkametgah Adresi", "İkametgâh Adresi", "Adres", "Açık Adres"],
        [DatasetColumn.Bolum] = ["Bölüm", "Bölümü", "Okuduğu Bölüm"],
        [DatasetColumn.TeslimYili] = ["Teslim Yılı", "Ek-16 Teslim Yılı", "Öğretim Yılı", "TeslimYili"],
        [DatasetColumn.OncekiStajGun] = ["Önceki Staj Gün Sayısı", "Daha Önce Yapılan Staj Gün Sayısı", "Staj Gün Sayısı", "OncekiStajGun"],
        [DatasetColumn.Email] = ["E-posta", "Eposta", "Email", "E-mail", "Mail"],
        [DatasetColumn.Telefon] = ["Telefon", "Telefon No", "Tel"],
        [DatasetColumn.SaglikStatusu] = ["Sağlık Yardımı Statüsü", "Sağlık Yardım Statüsü", "Sağlık Statüsü", "SaglikStatusu"],
    };

    private static readonly DatasetColumn[] RequiredColumns =
        [DatasetColumn.AdSoyad, DatasetColumn.TcKimlikNo, DatasetColumn.OgrenciNo];

    public abstract string FormatName { get; }
    public abstract IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Alt sınıf: ilk satır başlık olacak şekilde tüm satırları hücre metinleri olarak döndürür.</summary>
    protected abstract IEnumerable<IReadOnlyList<string>> ReadRows(Stream content);

    public bool Supports(string extension) => SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public DatasetParseResult Parse(Stream content)
    {
        List<IReadOnlyList<string>> rows;
        try
        {
            rows = ReadRows(content).ToList();
        }
        catch (Exception ex)
        {
            return DatasetParseResult.Fatal($"{FormatName} dosyası okunamadı: {ex.Message}");
        }

        if (rows.Count == 0) return DatasetParseResult.Fatal("Dosya boş.");

        var columnIndex = MapHeaders(rows[0]);
        var missing = RequiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            return DatasetParseResult.Fatal("Zorunlu sütunlar bulunamadı: " + string.Join(", ", missing.Select(m => HeaderAliases[m][0])));

        var data = new List<DatasetStudentData>();
        var errors = new List<DatasetRowError>();
        var seenTc = new HashSet<string>();
        var total = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            total++;
            var rowNumber = i + 1; // Excel satır numarası (başlık 1. satır)

            string Cell(DatasetColumn col) =>
                columnIndex.TryGetValue(col, out var idx) && idx < row.Count ? (row[idx] ?? string.Empty).Trim() : string.Empty;

            var rowErrors = new List<string>();
            var adSoyad = Cell(DatasetColumn.AdSoyad);
            var tc = TextHelper.DigitsOnly(Cell(DatasetColumn.TcKimlikNo));
            var ogrNo = Cell(DatasetColumn.OgrenciNo);

            if (adSoyad.Length == 0) rowErrors.Add("Ad soyad boş");
            // Uyruğa göre kimlik kontrolü (T.C.: ilk hane 0 olamaz, yabancı: 99 ile başlar).
            // Okul kayıtları için isteğe bağlı kontrol hanesi algoritması uygulanmaz.
            if (NationalIdRules.Validate(tc, Cell(DatasetColumn.Uyruk), checkAlgorithm: false) is { } tcError) rowErrors.Add(tcError);
            if (ogrNo.Length == 0) rowErrors.Add("Öğrenci no boş");

            var dogumText = Cell(DatasetColumn.DogumTarihi);
            var dogum = TextHelper.ParseDate(dogumText);
            if (dogumText.Length > 0 && dogum is null) rowErrors.Add($"Doğum tarihi okunamadı ('{dogumText}')");

            var stajText = Cell(DatasetColumn.OncekiStajGun);
            int? staj = null;
            if (stajText.Length > 0)
            {
                if (int.TryParse(stajText, out var s) && s >= 0) staj = s;
                else rowErrors.Add($"Önceki staj gün sayısı sayı olmalı ('{stajText}')");
            }

            if (tc.Length == 11 && !seenTc.Add(tc)) rowErrors.Add("Aynı TC kimlik no dosyada birden fazla kez geçiyor");

            if (rowErrors.Count > 0)
            {
                errors.Add(new DatasetRowError(rowNumber, string.Join("; ", rowErrors)));
                continue;
            }

            data.Add(new DatasetStudentData(
                adSoyad, tc, Cell(DatasetColumn.Uyruk), ogrNo, dogum, Cell(DatasetColumn.Adres),
                Cell(DatasetColumn.Bolum), Cell(DatasetColumn.TeslimYili), staj, Cell(DatasetColumn.Email),
                Cell(DatasetColumn.Telefon), Cell(DatasetColumn.SaglikStatusu)));
        }

        return new DatasetParseResult(data, errors, total);
    }

    private static Dictionary<DatasetColumn, int> MapHeaders(IReadOnlyList<string> headerRow)
    {
        var lookup = HeaderAliases
            .SelectMany(kv => kv.Value.Select(alias => (Key: TextHelper.NormalizeKey(alias), Column: kv.Key)))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().Column);

        var result = new Dictionary<DatasetColumn, int>();
        for (var i = 0; i < headerRow.Count; i++)
        {
            if (lookup.TryGetValue(TextHelper.NormalizeKey(headerRow[i]), out var col) && !result.ContainsKey(col))
                result[col] = i;
        }
        return result;
    }

    private enum DatasetColumn
    {
        AdSoyad, TcKimlikNo, Uyruk, OgrenciNo, DogumTarihi, Adres, Bolum, TeslimYili, OncekiStajGun, Email, Telefon, SaglikStatusu
    }
}
