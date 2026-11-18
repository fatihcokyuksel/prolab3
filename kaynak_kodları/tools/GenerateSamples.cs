#:package ClosedXML@0.105.1
#:package DocumentFormat.OpenXml@3.5.1
#:package PdfPig@0.1.16
#:project ../src/StajEvrak.Application/StajEvrak.Application.csproj

// Demo için örnek dosyalar üretir:
//   samples/test-dataset.xlsx, samples/test-dataset.csv  → admin veri seti (bilerek hatalı satırlar dahil)
//   samples/ek16-dogru.docx / ek16-hatali.docx          → doldurulmuş Ek-16 (samples/ek16-sablon.docx şablonundan)
//   samples/ek16-dogru.pdf                               → metin içeren PDF Ek-16
//   samples/sahte-belge.pdf, samples/not.txt, samples/ornek-gorsel.png → yükleme kontrolü denemeleri
// Çalıştırma (depo kökünden):  dotnet run tools/GenerateSamples.cs

using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using StajEvrak.Application.Common;
using StajEvrak.Domain.Ek16;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using W14 = DocumentFormat.OpenXml.Office2010.Word;

var root = FindRepoRoot();
var samples = Path.Combine(root, "samples");
Directory.CreateDirectory(samples);
var template = Path.Combine(samples, "ek16-sablon.docx");

// ---------------------------------------------------------------- veri seti
string[] headers = ["Ad Soyad", "TC Kimlik No", "Uyruk", "Öğrenci No", "Doğum Tarihi", "İkametgah Adresi", "Bölüm",
    "Teslim Yılı", "Önceki Staj Gün Sayısı", "E-posta", "Telefon", "Sağlık Yardımı Statüsü"];

var rand = new Random(42);
string[] names = ["Ali Yılmaz", "Ayşe Demir", "Mehmet Kaya", "Zeynep Çelik", "Mustafa Şahin", "Elif Yıldız", "Emre Öztürk",
    "Fatma Aydın", "Burak Arslan", "Merve Doğan", "Can Kılıç", "Selin Aslan", "Oğuzhan Koç", "İrem Kurt", "Hakan Özdemir",
    "Büşra Şimşek", "Kerem Polat", "Gizem Erdoğan", "Serkan Güneş", "Ece Aktaş"];
string[] departments = ["Bilgisayar Mühendisliği", "Yazılım Mühendisliği", "Elektrik Mühendisliği", "Makine Mühendisliği"];
string[] districts = ["Umuttepe Mah., Kocaeli Üniversitesi Cad., No: 12, Daire: 3, İzmit/Kocaeli",
    "Yahya Kaptan Mah., Ankara Cad., No: 45, Daire: 8, İzmit/Kocaeli",
    "Kemalpaşa Mah., Ali Rıza Sok., No: 7, Daire: 2, Başiskele/Kocaeli",
    "Cumhuriyet Mah., Atatürk Blv., No: 101, Daire: 14, Gebze/Kocaeli"];

var rows = new List<string[]>();
for (var i = 0; i < names.Length; i++)
{
    var foreign = i == 19;
    var tc = foreign ? ForeignId(rand) : TurkishId(rand);
    var no = $"2201{(i + 1):D5}";
    var mail = TextHelper.NormalizeKey(names[i].Split(' ')[0]) + "." + TextHelper.NormalizeKey(names[i].Split(' ')[1]) + "@kocaeli.edu.tr";
    var phone = $"+90 5{rand.Next(30, 56)} {rand.Next(100, 999)} {rand.Next(10, 99)} {rand.Next(10, 99)}";
    var birth = new DateTime(2002, 1, 1).AddDays(rand.Next(0, 1100)).ToString("dd.MM.yyyy");
    rows.Add([names[i], tc, foreign ? "Azerbaycan" : "T.C.", no, birth, districts[i % districts.Length], departments[i % departments.Length],
        "2025-2026", (i % 3 == 0 ? 0 : 20).ToString(), mail, phone, (1 + i % 2).ToString()]);
}
// Bilerek hatalı satırlar (içe aktarma raporunda görünmeli)
rows.Add(["Hatalı TC Örneği", "1234567890", "T.C.", "220199901", "01.01.2003", districts[0], departments[0], "2025-2026", "0", "hatali1@kocaeli.edu.tr", "+90 532 000 00 01", "1"]);
rows.Add(["", TurkishId(rand), "T.C.", "220199902", "01.01.2003", districts[0], departments[0], "2025-2026", "0", "hatali2@kocaeli.edu.tr", "+90 532 000 00 02", "1"]);
rows.Add(["Hatalı Tarih Örneği", TurkishId(rand), "T.C.", "220199903", "31.02.2003", districts[0], departments[0], "2025-2026", "on", "hatali3@kocaeli.edu.tr", "+90 532 000 00 03", "1"]);
rows.Add(["Tekrar Eden TC", rows[0][1], "T.C.", "220199904", "01.01.2003", districts[0], departments[0], "2025-2026", "0", "hatali4@kocaeli.edu.tr", "+90 532 000 00 04", "1"]);

using (var wb = new XLWorkbook())
{
    var ws = wb.AddWorksheet("Ogrenciler");
    for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
    for (var r = 0; r < rows.Count; r++)
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(r + 2, c + 1).SetValue(rows[r][c]); // metin olarak yaz (TC/telefon bozulmasın)
    ws.Row(1).Style.Font.Bold = true;
    ws.Columns().AdjustToContents();
    wb.SaveAs(Path.Combine(samples, "test-dataset.xlsx"));
}

var csv = new StringBuilder();
csv.AppendLine(string.Join(';', headers));
foreach (var r in rows.Take(10)) csv.AppendLine(string.Join(';', r.Select(v => v.Contains(';') ? $"\"{v}\"" : v)));
File.WriteAllText(Path.Combine(samples, "test-dataset.csv"), csv.ToString(), new UTF8Encoding(true));

// ---------------------------------------------------------------- Ek-16 (doğru)
var student = rows[0];
var correct = new Dictionary<string, string?>
{
    [Ek16Fields.TcKimlikNo] = student[1], [Ek16Fields.Uyruk] = student[2], [Ek16Fields.Bolum] = student[6],
    [Ek16Fields.AdSoyad] = student[0], [Ek16Fields.OncekiStajGun] = student[8], [Ek16Fields.DogumTarihi] = student[4],
    [Ek16Fields.OgretimYili] = "2025-2026 Bahar", [Ek16Fields.OgrenciNo] = student[3], [Ek16Fields.Email] = student[9],
    [Ek16Fields.Telefon] = student[10], [Ek16Fields.Adres] = student[5], [Ek16Fields.SaglikStatusu] = student[11],
    [Ek16Fields.FirmaAdi] = "Örnek Yazılım Teknolojileri A.Ş.", [Ek16Fields.Yurtdisi] = "H", [Ek16Fields.HizmetAlani] = "Yazılım Geliştirme",
    [Ek16Fields.StajBaslama] = "06.07.2026", [Ek16Fields.FirmaTelefon] = "+90 262 555 10 20", [Ek16Fields.StajBitis] = "14.08.2026",
    [Ek16Fields.FirmaFaks] = null, [Ek16Fields.StajGunSayisi] = "30", [Ek16Fields.FirmaEmail] = "ik@ornekyazilim.com.tr",
    [Ek16Fields.HaftaSonu] = "H", [Ek16Fields.FirmaWeb] = "www.ornekyazilim.com.tr", [Ek16Fields.CalisanSayisi] = "45",
    [Ek16Fields.MuhendisSayisi] = "12", [Ek16Fields.UcretAliyor] = "E",
    [Ek16Fields.FirmaAdres] = "Kuruçeşme Mah., Teknopark Cad., No: 3, Daire: 5, İzmit/Kocaeli",
    [Ek16Fields.YetkiliAdSoyad] = "Deniz Karaca", [Ek16Fields.YetkiliUnvan] = "İnsan Kaynakları Müdürü",
    [Ek16Fields.YetkiliEmail] = "deniz.karaca@ornekyazilim.com.tr", [Ek16Fields.YetkiliTelefon] = "+90 262 555 10 21",
};
FillDocx(template, Path.Combine(samples, "ek16-dogru.docx"), correct);

// ---------------------------------------------------------------- Ek-16 (hatalı: uyuşmazlık + eksik)
var wrong = new Dictionary<string, string?>(correct)
{
    [Ek16Fields.TcKimlikNo] = "11111111110",                // veri setiyle uyuşmaz
    [Ek16Fields.DogumTarihi] = "15.05.1999",                // veri setiyle uyuşmaz
    [Ek16Fields.Bolum] = "Makine Mühendisliği",             // veri setiyle uyuşmaz
    [Ek16Fields.YetkiliTelefon] = null,                     // eksik
    [Ek16Fields.MuhendisSayisi] = null,                     // eksik
};
FillDocx(template, Path.Combine(samples, "ek16-hatali.docx"), wrong);

// ---------------------------------------------------------------- PDF (metin içeren)
WritePdf(Path.Combine(samples, "ek16-dogru.pdf"), correct);

// ---------------------------------------------------------------- reddedilmesi gereken dosyalar
File.WriteAllText(Path.Combine(samples, "sahte-belge.pdf"), "Bu bir PDF değil, uzantısı değiştirilmiş düz metin dosyasıdır.");
File.WriteAllText(Path.Combine(samples, "not.txt"), "Kabul edilmeyen format örneği.");
File.WriteAllBytes(Path.Combine(samples, "ornek-gorsel.png"), Convert.FromBase64String(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="));

Console.WriteLine($"Örnek dosyalar üretildi: {samples}");
foreach (var f in Directory.GetFiles(samples).Order()) Console.WriteLine("  " + Path.GetFileName(f));

// ================================================================= yardımcılar
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StajEvrak.sln"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("StajEvrak.sln bulunamadı; depo kökünden çalıştırın.");
}

static string TurkishId(Random r)
{
    var d = new int[11];
    d[0] = r.Next(1, 10);
    for (var i = 1; i < 9; i++) d[i] = r.Next(0, 10);
    var odd = d[0] + d[2] + d[4] + d[6] + d[8];
    var even = d[1] + d[3] + d[5] + d[7];
    d[9] = ((odd * 7 - even) % 10 + 10) % 10;
    d[10] = d.Take(10).Sum() % 10;
    return string.Concat(d);
}

// Yabancı kimlik no: 99 ile başlar ve T.C. kimlik no ile aynı kontrol hanesi algoritmasına uyar.
static string ForeignId(Random r)
{
    var d = new int[11];
    d[0] = 9; d[1] = 9;
    for (var i = 2; i < 11; i++) d[i] = r.Next(0, 10); // eski sürümle aynı sayıda rastgele değer çekilir
    var odd = d[0] + d[2] + d[4] + d[6] + d[8];
    var even = d[1] + d[3] + d[5] + d[7];
    d[9] = ((odd * 7 - even) % 10 + 10) % 10;
    d[10] = d.Take(10).Sum() % 10;
    return string.Concat(d);
}

static void FillDocx(string templatePath, string outputPath, Dictionary<string, string?> values)
{
    File.Copy(templatePath, outputPath, overwrite: true);
    using var doc = WordprocessingDocument.Open(outputPath, true);
    var body = doc.MainDocumentPart!.Document!.Body!;
    var sections = new[] { Ek16Section.Student, Ek16Section.Company, Ek16Section.Employer };
    var tables = body.Descendants<Table>().ToList();

    for (var t = 0; t < sections.Length && t < tables.Count; t++)
    {
        var section = sections[t];
        foreach (var row in tables[t].Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            for (var i = 0; i < cells.Count - 1; i++)
            {
                var label = TextHelper.NormalizeKey(string.Concat(cells[i].Descendants<Text>().Select(x => x.Text)));
                var defs = Ek16Fields.InSection(section)
                    .Where(f => label.Length > 0 && label.StartsWith(TextHelper.NormalizeKey(f.DocxLabel)))
                    .ToList();
                if (defs.Count == 0) continue;
                var longest = defs.Max(f => TextHelper.NormalizeKey(f.DocxLabel).Length);
                defs = defs.Where(f => TextHelper.NormalizeKey(f.DocxLabel).Length == longest).OrderBy(f => f.DocxPart ?? 0).ToList();

                if (defs[0].Kind == FieldKind.Choice)
                {
                    SetCheckbox(cells[i + 1], values.GetValueOrDefault(defs[0].Code));
                }
                else
                {
                    var parts = defs.Select(f => values.GetValueOrDefault(f.Code)).Where(v => !string.IsNullOrWhiteSpace(v));
                    SetCellText(cells[i + 1], string.Join(" / ", parts));
                }
                i++;
            }
        }
    }
    doc.MainDocumentPart.Document.Save();
}

static void SetCellText(TableCell cell, string text)
{
    var paragraph = cell.Elements<Paragraph>().FirstOrDefault() ?? cell.AppendChild(new Paragraph());
    foreach (var run in paragraph.Elements<Run>().ToList()) run.Remove();
    foreach (var extra in cell.Elements<Paragraph>().Skip(1).ToList()) extra.Remove();
    if (text.Length > 0)
        paragraph.AppendChild(new Run(new RunProperties(new FontSize { Val = "18" }), new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }));
}

static void SetCheckbox(TableCell cell, string? choice)
{
    var index = 1;
    foreach (var sdt in cell.Descendants<SdtRun>())
    {
        var box = sdt.SdtProperties?.GetFirstChild<W14.SdtContentCheckBox>();
        if (box is null) continue;
        var isChecked = choice == index.ToString();
        box.Checked ??= new W14.Checked();
        box.Checked.Val = isChecked ? W14.OnOffValues.One : W14.OnOffValues.Zero;
        foreach (var t in sdt.Descendants<Text>()) t.Text = isChecked ? "☒" : "☐";
        index++;
    }
}

static void WritePdf(string path, Dictionary<string, string?> v)
{
    string Get(string code) => v.GetValueOrDefault(code) ?? "";
    var lines = new List<string>
    {
        "T.C. KOCAELİ ÜNİVERSİTESİ MÜHENDİSLİK FAKÜLTESİ DEKANLIĞI STAJ KABUL FORMU",
        "EK-16",
        "ÖĞRENCİNİN",
        $"T.C. KİMLİK NO/ UYRUĞU {Get(Ek16Fields.TcKimlikNo)} / {Get(Ek16Fields.Uyruk)} BÖLÜMÜ {Get(Ek16Fields.Bolum)}",
        $"ADI SOYADI {Get(Ek16Fields.AdSoyad)} DAHA ÖNCE YAPTIĞI STAJ GÜN SAYISI {Get(Ek16Fields.OncekiStajGun)}",
        $"DOĞUM TARİHİ {Get(Ek16Fields.DogumTarihi)} ÖĞRETİM YILI {Get(Ek16Fields.OgretimYili)}",
        $"ÖĞRENCİ NUMARASI {Get(Ek16Fields.OgrenciNo)} E-POSTA ADRESİ /TELEFON NO {Get(Ek16Fields.Email)} / {Get(Ek16Fields.Telefon)}",
        $"İKAMETGÂH ADRESİ {Get(Ek16Fields.Adres)}",
        $"SAĞLIK YARDIM STATÜSÜ (*) " + string.Join(" ", Enumerable.Range(1, 6).Select(i => $"{i}-{(Get(Ek16Fields.SaglikStatusu) == i.ToString() ? "X" : "")}")),
        "STAJ YAPILAN YERİN",
        $"Adı {Get(Ek16Fields.FirmaAdi)} Yurtdışı Stajımı? (E/H) {Get(Ek16Fields.Yurtdisi)}",
        $"Hizmet Alanı {Get(Ek16Fields.HizmetAlani)} Staj Başlama Tarihi {Get(Ek16Fields.StajBaslama)}",
        $"Telefon No {Get(Ek16Fields.FirmaTelefon)} Staj Bitiş Tarihi {Get(Ek16Fields.StajBitis)}",
        $"Faks No {Get(Ek16Fields.FirmaFaks)} Staj Gün Sayısı {Get(Ek16Fields.StajGunSayisi)}",
        $"E-Posta Adresi {Get(Ek16Fields.FirmaEmail)} Hafta Sonu Çalışılıyor mu? (E / H) {Get(Ek16Fields.HaftaSonu)}",
        $"Web Adresi {Get(Ek16Fields.FirmaWeb)} Çalışan Sayısı {Get(Ek16Fields.CalisanSayisi)}",
        $"Mühendis Sayısı {Get(Ek16Fields.MuhendisSayisi)} Ücret Alıyor mu? (E / H) - (**) {Get(Ek16Fields.UcretAliyor)}",
        $"Adres Bilgisi {Get(Ek16Fields.FirmaAdres)}",
        "İŞVEREN VEYA YETKİLİNİN",
        $"Adı Soyadı {Get(Ek16Fields.YetkiliAdSoyad)}",
        $"Görev / Unvanı {Get(Ek16Fields.YetkiliUnvan)} İmza / Kaşe",
        $"E-Posta adresi {Get(Ek16Fields.YetkiliEmail)}",
        $"Telefon No {Get(Ek16Fields.YetkiliTelefon)}",
        "ÖĞRENCİNİN ONAYI STAJ DANIŞMANI ONAYI",
    };

    var builder = new PdfDocumentBuilder();
    var font = builder.AddTrueTypeFont(File.ReadAllBytes(@"C:\Windows\Fonts\arial.ttf"));
    var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
    var y = 800d;
    foreach (var line in lines)
    {
        page.AddText(line, 8, new PdfPoint(30, y), font);
        y -= 16;
    }
    File.WriteAllBytes(path, builder.Build());
}
