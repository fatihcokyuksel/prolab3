using StajEvrak.Domain.Common;
using StajEvrak.Domain.Ek16;

namespace StajEvrak.Domain.Applications;

/// <summary>Bir başvuruya yüklenmiş Ek-16 dosyasının bir versiyonu.</summary>
public class UploadedDocument : Entity
{
    private readonly List<ExtractedField> _extractedFields = [];

    private UploadedDocument() { }

    internal UploadedDocument(int version, string originalFileName, string storedPath, string extension, long sizeBytes, DateTime uploadedAt)
    {
        Version = version;
        OriginalFileName = originalFileName;
        StoredPath = storedPath;
        Extension = extension.ToLowerInvariant();
        SizeBytes = sizeBytes;
        UploadedAt = uploadedAt;
    }

    public int ApplicationId { get; private set; }
    public int Version { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoredPath { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    /// <summary>Okuyucunun belge hakkındaki notu (ör. "Görüntü dosyaları final aşamasında OCR ile okunacak").</summary>
    public string? ReadNote { get; private set; }
    public bool AutoReadSupported { get; private set; }

    public IReadOnlyCollection<ExtractedField> ExtractedFields => _extractedFields.AsReadOnly();

    public void SetExtraction(bool supported, IReadOnlyDictionary<string, string?> fields, string? note)
    {
        AutoReadSupported = supported;
        ReadNote = note;
        _extractedFields.Clear();
        foreach (var (code, value) in fields.Where(f => Ek16Fields.IsKnown(f.Key)))
            _extractedFields.Add(new ExtractedField(code, value));
    }

    public IReadOnlyDictionary<string, string?> ToFieldValues() =>
        _extractedFields.ToDictionary(f => f.FieldCode, f => f.Value);
}

public class ExtractedField : Entity
{
    private ExtractedField() { }

    public ExtractedField(string fieldCode, string? value)
    {
        FieldCode = fieldCode;
        Value = value;
    }

    public int DocumentId { get; private set; }
    public string FieldCode { get; private set; } = string.Empty;
    public string? Value { get; private set; }
}
