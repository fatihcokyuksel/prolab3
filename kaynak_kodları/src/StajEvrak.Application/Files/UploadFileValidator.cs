using Microsoft.Extensions.Options;

namespace StajEvrak.Application.Files;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}

public sealed record FileValidationResult(bool IsValid, string? Error, string Extension, FileSignature? Signature)
{
    public static FileValidationResult Fail(string error, string extension = "") => new(false, error, extension, null);
}

public interface IUploadFileValidator
{
    IReadOnlyCollection<string> AllowedExtensions { get; }
    FileValidationResult Validate(string fileName, long length, Stream content);
}

/// <summary>
/// Evrak yükleme kontrolleri (vize kapsamı): uzantı, boyut ve dosya imzası.
/// Kabul: Word (.doc/.docx), PDF, JPEG, PNG. Diğerleri reddedilir.
/// </summary>
public sealed class UploadFileValidator(IEnumerable<FileSignature> signatures, IOptions<UploadOptions> options) : IUploadFileValidator
{
    private readonly IReadOnlyList<FileSignature> _signatures = signatures.ToList();
    private readonly long _maxBytes = options.Value.MaxFileSizeBytes;

    public IReadOnlyCollection<string> AllowedExtensions => _signatures.SelectMany(s => s.Extensions).ToList();

    public FileValidationResult Validate(string fileName, long length, Stream content)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var allowed = string.Join(", ", AllowedExtensions);

        if (string.IsNullOrEmpty(extension))
            return FileValidationResult.Fail($"Dosya uzantısı bulunamadı. Kabul edilen formatlar: {allowed}");

        var signature = _signatures.FirstOrDefault(s => s.SupportsExtension(extension));
        if (signature is null)
            return FileValidationResult.Fail($"'{extension}' formatı kabul edilmiyor. Kabul edilen formatlar: {allowed}", extension);

        if (length <= 0)
            return FileValidationResult.Fail("Dosya boş.", extension);

        if (length > _maxBytes)
            return FileValidationResult.Fail($"Dosya boyutu {_maxBytes / (1024 * 1024)} MB sınırını aşıyor.", extension);

        Span<byte> header = stackalloc byte[8];
        var start = content.CanSeek ? content.Position : 0;
        var read = content.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (content.CanSeek) content.Position = start;

        if (!signature.MatchesHeader(header[..read]))
            return FileValidationResult.Fail(
                $"Dosya içeriği '{extension}' uzantısıyla uyuşmuyor. Dosya gerçek bir {signature.DisplayName} belgesi değil.", extension);

        var contentOk = signature.ValidateContent(content);
        if (content.CanSeek) content.Position = start;
        if (!contentOk)
            return FileValidationResult.Fail($"Dosya geçerli bir {signature.DisplayName} belgesi değil (bozuk olabilir).", extension);

        return new FileValidationResult(true, null, extension, signature);
    }
}
