using StajEvrak.Domain.Common;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Domain.Dataset;

/// <summary>Admin'in yüklediği test veri setindeki bir öğrenci satırı (Bölüm 4.3).</summary>
public class DatasetStudent : Entity
{
    private DatasetStudent() { }

    public DatasetStudent(DatasetStudentData data, DateTime importedAt)
    {
        if (string.IsNullOrWhiteSpace(data.TcKimlikNo)) throw new DomainException("TC kimlik no boş olamaz.");
        TcKimlikNo = data.TcKimlikNo.Trim();
        Update(data, importedAt);
    }

    public string AdSoyad { get; private set; } = string.Empty;
    public string TcKimlikNo { get; private set; } = string.Empty;
    public string Uyruk { get; private set; } = string.Empty;
    public string OgrenciNo { get; private set; } = string.Empty;
    public DateOnly? DogumTarihi { get; private set; }
    public string Adres { get; private set; } = string.Empty;
    public string Bolum { get; private set; } = string.Empty;
    public string TeslimYili { get; private set; } = string.Empty;
    public int? OncekiStajGun { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Telefon { get; private set; } = string.Empty;
    public string SaglikStatusu { get; private set; } = string.Empty;
    public DateTime ImportedAt { get; private set; }

    /// <summary>Veri setinden gelen yeni değerlerle kaydı günceller (upsert). TC kimlik no anahtardır, değişmez.</summary>
    public void Update(DatasetStudentData data, DateTime importedAt)
    {
        AdSoyad = data.AdSoyad.Trim();
        Uyruk = data.Uyruk.Trim();
        OgrenciNo = data.OgrenciNo.Trim();
        DogumTarihi = data.DogumTarihi;
        Adres = data.Adres.Trim();
        Bolum = data.Bolum.Trim();
        TeslimYili = data.TeslimYili.Trim();
        OncekiStajGun = data.OncekiStajGun;
        Email = data.Email.Trim().ToLowerInvariant();
        Telefon = data.Telefon.Trim();
        SaglikStatusu = data.SaglikStatusu.Trim();
        ImportedAt = importedAt;
    }

    /// <summary>Karşılaştırma raporu için Ek-16 alan kodlarıyla eşlenmiş değerler.</summary>
    public IReadOnlyDictionary<string, string?> ToFieldValues() => new Dictionary<string, string?>
    {
        [Ek16Fields.AdSoyad] = AdSoyad,
        [Ek16Fields.TcKimlikNo] = TcKimlikNo,
        [Ek16Fields.Uyruk] = Uyruk,
        [Ek16Fields.OgrenciNo] = OgrenciNo,
        [Ek16Fields.DogumTarihi] = DogumTarihi?.ToString("dd.MM.yyyy"),
        [Ek16Fields.Adres] = Adres,
        [Ek16Fields.Bolum] = Bolum,
        [Ek16Fields.OgretimYili] = TeslimYili,
        [Ek16Fields.OncekiStajGun] = OncekiStajGun?.ToString(),
        [Ek16Fields.Email] = Email,
        [Ek16Fields.Telefon] = Telefon,
        [Ek16Fields.SaglikStatusu] = SaglikStatusu,
    };
}

/// <summary>Veri seti satırının ayrıştırılmış hali (parser çıktısı).</summary>
public sealed record DatasetStudentData(
    string AdSoyad,
    string TcKimlikNo,
    string Uyruk,
    string OgrenciNo,
    DateOnly? DogumTarihi,
    string Adres,
    string Bolum,
    string TeslimYili,
    int? OncekiStajGun,
    string Email,
    string Telefon,
    string SaglikStatusu);
