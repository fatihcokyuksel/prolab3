namespace StajEvrak.Application.Documents;

/// <summary>Belge okuyucunun çıktısı: okunabildiyse alan kodu → değer sözlüğü.</summary>
public sealed record DocumentReadResult(bool Supported, IReadOnlyDictionary<string, string?> Fields, string? Note)
{
    public static DocumentReadResult NotSupported(string note) => new(false, new Dictionary<string, string?>(), note);
}

/// <summary>Ek-16 belgesinden alan değerlerini çıkaran okuyucu soyutlaması.</summary>
public interface IDocumentReader
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    DocumentReadResult Read(string fullPath);
}

public interface IDocumentReaderFactory
{
    IDocumentReader GetReader(string extension);
}
