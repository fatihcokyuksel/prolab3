using StajEvrak.Application.Documents;

namespace StajEvrak.Infrastructure.Readers;

/// <summary>
/// JPEG/PNG: El yazısı/görüntü okuma (OCR) final aşamasında eklenecek.
/// Vizede dosya kabul edilir; karşılaştırma öğrencinin girdiği bilgiler ile veri seti arasında yapılır.
/// </summary>
public sealed class ImageDocumentReader : DocumentReader
{
    public override IReadOnlyCollection<string> SupportedExtensions => [".jpg", ".jpeg", ".png"];

    public override DocumentReadResult Read(string fullPath) => DocumentReadResult.NotSupported(
        "Görüntü dosyaları (JPEG/PNG) final aşamasında OCR ile okunacaktır. Şimdilik sisteme girdiğiniz bilgiler veri seti ile karşılaştırıldı.");

    protected override Dictionary<string, string?> Extract(string fullPath) => [];
}

/// <summary>Word 97-2003 (.doc) ikili formatı otomatik okunmuyor; kullanıcı .docx olarak kaydedip yükleyebilir.</summary>
public sealed class LegacyDocReader : DocumentReader
{
    public override IReadOnlyCollection<string> SupportedExtensions => [".doc"];

    public override DocumentReadResult Read(string fullPath) => DocumentReadResult.NotSupported(
        "Eski Word (.doc) formatı otomatik okunamıyor. Otomatik kontrol için belgeyi .docx veya PDF olarak kaydedip yükleyebilirsiniz.");

    protected override Dictionary<string, string?> Extract(string fullPath) => [];
}

/// <summary>Uzantıya göre uygun okuyucuyu seçer (çok biçimlilik: çağıran taraf somut sınıfı bilmez).</summary>
public sealed class DocumentReaderFactory(IEnumerable<IDocumentReader> readers) : IDocumentReaderFactory
{
    private readonly IReadOnlyList<IDocumentReader> _readers = readers.ToList();

    public IDocumentReader GetReader(string extension) =>
        _readers.FirstOrDefault(r => r.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"'{extension}' için okuyucu yok.");
}
