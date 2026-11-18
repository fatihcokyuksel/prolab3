using System.Globalization;
using StajEvrak.Application.Common;
using StajEvrak.Domain.Ek16;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace StajEvrak.Infrastructure.Readers;

/// <summary>
/// Metin içeren PDF (Word'den "PDF olarak kaydet") Ek-16 okuyucu.
/// Kelimeler satırlara gruplanır; her satırda form etiketleri bulunur ve iki etiket arasındaki metin değer kabul edilir.
/// Taranmış (görüntü) PDF'lerde metin olmadığından okuma final aşamasındaki OCR'a bırakılır.
/// </summary>
public sealed class PdfEk16Reader : DocumentReader
{
    // Ek-16 formundaki tam etiket metinleri (bölüm bazında). "null" alan kodu = değeri okunmayacak etiket.
    private static readonly Dictionary<Ek16Section, (string Label, string[] Codes)[]> Labels = new()
    {
        [Ek16Section.Student] =
        [
            ("T.C. KİMLİK NO/ UYRUĞU", [Ek16Fields.TcKimlikNo, Ek16Fields.Uyruk]),
            ("BÖLÜMÜ", [Ek16Fields.Bolum]),
            ("ADI SOYADI", [Ek16Fields.AdSoyad]),
            ("DAHA ÖNCE YAPTIĞI STAJ GÜN SAYISI", [Ek16Fields.OncekiStajGun]),
            ("DOĞUM TARİHİ", [Ek16Fields.DogumTarihi]),
            ("ÖĞRETİM YILI", [Ek16Fields.OgretimYili]),
            ("ÖĞRENCİ NUMARASI", [Ek16Fields.OgrenciNo]),
            ("E-POSTA ADRESİ /TELEFON NO", [Ek16Fields.Email, Ek16Fields.Telefon]),
            ("İKAMETGÂH ADRESİ", [Ek16Fields.Adres]),
            ("SAĞLIK YARDIM STATÜSÜ (*)", [Ek16Fields.SaglikStatusu]),
        ],
        [Ek16Section.Company] =
        [
            ("Adı", [Ek16Fields.FirmaAdi]),
            ("Yurtdışı Stajımı? (E/H)", [Ek16Fields.Yurtdisi]),
            ("(E İse Öğrenci Kendi İmkanlarıyla SGK Yaptırır)", []),
            ("Hizmet Alanı", [Ek16Fields.HizmetAlani]),
            ("Staj Başlama Tarihi", [Ek16Fields.StajBaslama]),
            ("Telefon No", [Ek16Fields.FirmaTelefon]),
            ("Staj Bitiş Tarihi", [Ek16Fields.StajBitis]),
            ("Faks No", [Ek16Fields.FirmaFaks]),
            ("Staj Gün Sayısı", [Ek16Fields.StajGunSayisi]),
            ("E-Posta Adresi", [Ek16Fields.FirmaEmail]),
            ("Hafta Sonu Çalışılıyor mu? (E / H)", [Ek16Fields.HaftaSonu]),
            ("Web Adresi", [Ek16Fields.FirmaWeb]),
            ("Çalışan Sayısı", [Ek16Fields.CalisanSayisi]),
            ("Mühendis Sayısı", [Ek16Fields.MuhendisSayisi]),
            ("Ücret Alıyor mu? (E / H) - (**)", [Ek16Fields.UcretAliyor]),
            ("Adres Bilgisi", [Ek16Fields.FirmaAdres]),
        ],
        [Ek16Section.Employer] =
        [
            ("Adı Soyadı", [Ek16Fields.YetkiliAdSoyad]),
            ("Görev / Unvanı", [Ek16Fields.YetkiliUnvan]),
            ("İmza / Kaşe", []),
            ("E-Posta adresi", [Ek16Fields.YetkiliEmail]),
            ("Telefon No", [Ek16Fields.YetkiliTelefon]),
        ],
    };

    private static readonly CompareInfo Compare = TextHelper.Turkish.CompareInfo;
    private const CompareOptions IgnoreCase = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public override IReadOnlyCollection<string> SupportedExtensions => [".pdf"];

    protected override Dictionary<string, string?> Extract(string fullPath)
    {
        var lines = ReadLines(fullPath);
        if (lines.Count == 0)
            throw new InvalidDataException("PDF'te metin bulunamadı (taranmış belge olabilir). Taranmış belgeler final aşamasında OCR ile okunacaktır.");

        var result = new Dictionary<string, string?>();
        Ek16Section? section = null;

        foreach (var line in lines)
        {
            var key = TextHelper.NormalizeKey(line);
            if (key.StartsWith("ogrencinin") && !key.Contains("onay")) { section = Ek16Section.Student; continue; }
            if (key.StartsWith("stajyapilanyerin")) { section = Ek16Section.Company; continue; }
            if (key.StartsWith("isverenveyayetkilinin")) { section = Ek16Section.Employer; continue; }
            if (key.Contains("ogrencininonayi") || key.StartsWith("onemlinot")) { section = null; continue; }
            if (section is null) continue;

            foreach (var (label, value) in SplitByLabels(line, Labels[section.Value]))
            {
                if (label.Codes.Length == 0 || label.Codes.Any(result.ContainsKey)) continue;
                var defs = label.Codes.Select(Ek16Fields.Get).ToList();
                Assign(result, defs, value);
            }
        }
        return result;
    }

    /// <summary>Satırdaki etiketleri bulur ve her etiketten sonraki metni (bir sonraki etikete kadar) değer olarak döndürür.</summary>
    private static IEnumerable<((string Label, string[] Codes) Label, string Value)> SplitByLabels(
        string line, (string Label, string[] Codes)[] labels)
    {
        var hits = new List<(int Start, int End, (string, string[]) Label)>();
        foreach (var label in labels.OrderByDescending(l => l.Label.Length))
        {
            var from = 0;
            while (from < line.Length)
            {
                var idx = Compare.IndexOf(line, label.Label, from, IgnoreCase);
                if (idx < 0) break;
                var end = idx + label.Label.Length;
                var boundary = (idx == 0 || !char.IsLetterOrDigit(line[idx - 1])) &&
                               (end >= line.Length || !char.IsLetterOrDigit(line[end]));
                var overlaps = hits.Any(h => idx < h.End && end > h.Start);
                if (boundary && !overlaps) { hits.Add((idx, end, label)); break; }
                from = idx + 1;
            }
        }

        hits.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 0; i < hits.Count; i++)
        {
            var valueEnd = i + 1 < hits.Count ? hits[i + 1].Start : line.Length;
            yield return (hits[i].Label, line[hits[i].End..valueEnd].Trim());
        }
    }

    /// <summary>Sayfadaki kelimeleri dikey konuma göre satırlara gruplar.</summary>
    private static List<string> ReadLines(string fullPath)
    {
        var lines = new List<string>();
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var pdf = PdfDocument.Open(stream);
        foreach (var page in pdf.GetPages())
        {
            var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
            var groups = new List<List<Word>>();
            foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].BoundingBox.Bottom - word.BoundingBox.Bottom) < 3);
                if (group is null) groups.Add([word]);
                else group.Add(word);
            }
            lines.AddRange(groups.Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))));
        }
        return lines;
    }
}
