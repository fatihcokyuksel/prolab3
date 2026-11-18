namespace StajEvrak.Domain.Ek16;

/// <summary>
/// KOÜ Mühendislik Fakültesi Ek-16 (Staj Kabul Formu) alan kataloğu.
/// Manuel giriş formu, belge okuyucular ve karşılaştırma raporu bu tek listeyi kullanır.
/// </summary>
public static class Ek16Fields
{
    // Öğrenci bölümü
    public const string TcKimlikNo = "TcKimlikNo";
    public const string Uyruk = "Uyruk";
    public const string Bolum = "Bolum";
    public const string AdSoyad = "AdSoyad";
    public const string OncekiStajGun = "OncekiStajGun";
    public const string DogumTarihi = "DogumTarihi";
    public const string OgretimYili = "OgretimYili";
    public const string OgrenciNo = "OgrenciNo";
    public const string Email = "Email";
    public const string Telefon = "Telefon";
    public const string Adres = "Adres";
    public const string SaglikStatusu = "SaglikStatusu";

    // Staj yapılan yer
    public const string FirmaAdi = "FirmaAdi";
    public const string Yurtdisi = "Yurtdisi";
    public const string HizmetAlani = "HizmetAlani";
    public const string StajBaslama = "StajBaslama";
    public const string FirmaTelefon = "FirmaTelefon";
    public const string StajBitis = "StajBitis";
    public const string FirmaFaks = "FirmaFaks";
    public const string StajGunSayisi = "StajGunSayisi";
    public const string FirmaEmail = "FirmaEmail";
    public const string HaftaSonu = "HaftaSonu";
    public const string FirmaWeb = "FirmaWeb";
    public const string CalisanSayisi = "CalisanSayisi";
    public const string MuhendisSayisi = "MuhendisSayisi";
    public const string UcretAliyor = "UcretAliyor";
    public const string FirmaAdres = "FirmaAdres";

    // İşveren / yetkili
    public const string YetkiliAdSoyad = "YetkiliAdSoyad";
    public const string YetkiliUnvan = "YetkiliUnvan";
    public const string YetkiliEmail = "YetkiliEmail";
    public const string YetkiliTelefon = "YetkiliTelefon";

    public static readonly IReadOnlyList<Ek16FieldDefinition> All =
    [
        new(TcKimlikNo, "T.C. kimlik no", Ek16Section.Student, FieldKind.NationalId, "T.C. KİMLİK NO", 0),
        new(Uyruk, "Uyruğu", Ek16Section.Student, FieldKind.Text, "T.C. KİMLİK NO", 1),
        new(Bolum, "Bölümü", Ek16Section.Student, FieldKind.Text, "BÖLÜMÜ"),
        new(AdSoyad, "Adı soyadı", Ek16Section.Student, FieldKind.Text, "ADI SOYADI"),
        new(OncekiStajGun, "Daha önce yaptığı staj gün sayısı", Ek16Section.Student, FieldKind.Number, "DAHA ÖNCE YAPTIĞI STAJ"),
        new(DogumTarihi, "Doğum tarihi", Ek16Section.Student, FieldKind.Date, "DOĞUM TARİHİ"),
        new(OgretimYili, "Öğretim yılı", Ek16Section.Student, FieldKind.AcademicYear, "ÖĞRETİM YILI"),
        new(OgrenciNo, "Öğrenci numarası", Ek16Section.Student, FieldKind.Number, "ÖĞRENCİ NUMARASI"),
        new(Email, "E-posta adresi", Ek16Section.Student, FieldKind.Email, "E-POSTA ADRESİ", 0),
        new(Telefon, "Telefon no", Ek16Section.Student, FieldKind.Phone, "E-POSTA ADRESİ", 1),
        new(Adres, "İkametgâh adresi", Ek16Section.Student, FieldKind.Text, "İKAMETGÂH ADRESİ"),
        new(SaglikStatusu, "Sağlık yardım statüsü", Ek16Section.Student, FieldKind.Choice, "SAĞLIK YARDIM STATÜSÜ"),

        new(FirmaAdi, "Adı", Ek16Section.Company, FieldKind.Text, "Adı"),
        new(Yurtdisi, "Yurtdışı stajı mı?", Ek16Section.Company, FieldKind.YesNo, "Yurtdışı Staj"),
        new(HizmetAlani, "Hizmet alanı", Ek16Section.Company, FieldKind.Text, "Hizmet Alanı"),
        new(StajBaslama, "Staj başlama tarihi", Ek16Section.Company, FieldKind.Date, "Staj Başlama Tarihi"),
        new(FirmaTelefon, "Telefon no", Ek16Section.Company, FieldKind.Phone, "Telefon No"),
        new(StajBitis, "Staj bitiş tarihi", Ek16Section.Company, FieldKind.Date, "Staj Bitiş Tarihi"),
        new(FirmaFaks, "Faks no", Ek16Section.Company, FieldKind.Phone, "Faks No", Required: false),
        new(StajGunSayisi, "Staj gün sayısı", Ek16Section.Company, FieldKind.Number, "Staj Gün Sayısı"),
        new(FirmaEmail, "E-posta adresi", Ek16Section.Company, FieldKind.Email, "E-Posta Adresi"),
        new(HaftaSonu, "Hafta sonu çalışılıyor mu?", Ek16Section.Company, FieldKind.YesNo, "Hafta Sonu Çalışılıyor"),
        new(FirmaWeb, "Web adresi", Ek16Section.Company, FieldKind.Text, "Web Adresi", Required: false),
        new(CalisanSayisi, "Çalışan sayısı", Ek16Section.Company, FieldKind.Number, "Çalışan Sayısı"),
        new(MuhendisSayisi, "Mühendis sayısı", Ek16Section.Company, FieldKind.Number, "Mühendis Sayısı"),
        new(UcretAliyor, "Ücret alıyor mu?", Ek16Section.Company, FieldKind.YesNo, "Ücret Alıyor"),
        new(FirmaAdres, "Adres bilgisi", Ek16Section.Company, FieldKind.Text, "Adres Bilgisi"),

        new(YetkiliAdSoyad, "Adı soyadı", Ek16Section.Employer, FieldKind.Text, "Adı Soyadı"),
        new(YetkiliUnvan, "Görev / unvanı", Ek16Section.Employer, FieldKind.Text, "Görev / Unvanı"),
        new(YetkiliEmail, "E-posta adresi", Ek16Section.Employer, FieldKind.Email, "E-Posta adresi"),
        new(YetkiliTelefon, "Telefon no", Ek16Section.Employer, FieldKind.Phone, "Telefon No"),
    ];

    private static readonly Dictionary<string, Ek16FieldDefinition> ByCode =
        All.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnown(string code) => ByCode.ContainsKey(code);

    public static Ek16FieldDefinition Get(string code) =>
        ByCode.TryGetValue(code, out var def) ? def : throw new KeyNotFoundException($"Bilinmeyen Ek-16 alanı: {code}");

    public static IEnumerable<Ek16FieldDefinition> InSection(Ek16Section section) => All.Where(f => f.Section == section);

    public static string SectionTitle(Ek16Section section) => section switch
    {
        Ek16Section.Student => "Öğrencinin",
        Ek16Section.Company => "Staj yapılan yerin",
        Ek16Section.Employer => "İşveren veya yetkilinin",
        _ => section.ToString()
    };
}
