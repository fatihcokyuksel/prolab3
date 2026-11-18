using System.IO.Compression;

namespace StajEvrak.Application.Files;

/// <summary>
/// Dosya türü imzası (magic bytes). Her alt sınıf kendi türünü tanır (kalıtım + çok biçimlilik).
/// Uzantısı değiştirilmiş dosyaların (ör. .txt → .pdf) kabul edilmesini engeller.
/// </summary>
public abstract class FileSignature
{
    public abstract string DisplayName { get; }
    public abstract IReadOnlyCollection<string> Extensions { get; }

    /// <summary>Dosyanın ilk baytları bu türe uyuyor mu?</summary>
    public abstract bool MatchesHeader(ReadOnlySpan<byte> header);

    /// <summary>Gerekirse içeriği daha ayrıntılı doğrular (varsayılan: başlık yeterli).</summary>
    public virtual bool ValidateContent(Stream content) => true;

    public bool SupportsExtension(string extension) =>
        Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

    protected static bool StartsWith(ReadOnlySpan<byte> header, params byte[] magic) =>
        header.Length >= magic.Length && header[..magic.Length].SequenceEqual(magic);
}

public sealed class PdfSignature : FileSignature
{
    public override string DisplayName => "PDF";
    public override IReadOnlyCollection<string> Extensions => [".pdf"];
    public override bool MatchesHeader(ReadOnlySpan<byte> header) => StartsWith(header, 0x25, 0x50, 0x44, 0x46); // %PDF
}

public sealed class PngSignature : FileSignature
{
    public override string DisplayName => "PNG";
    public override IReadOnlyCollection<string> Extensions => [".png"];
    public override bool MatchesHeader(ReadOnlySpan<byte> header) =>
        StartsWith(header, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
}

public sealed class JpegSignature : FileSignature
{
    public override string DisplayName => "JPEG";
    public override IReadOnlyCollection<string> Extensions => [".jpg", ".jpeg"];
    public override bool MatchesHeader(ReadOnlySpan<byte> header) => StartsWith(header, 0xFF, 0xD8, 0xFF);
}

/// <summary>Eski Word (.doc) — OLE2 Compound File.</summary>
public sealed class DocSignature : FileSignature
{
    public override string DisplayName => "Word 97-2003 (.doc)";
    public override IReadOnlyCollection<string> Extensions => [".doc"];
    public override bool MatchesHeader(ReadOnlySpan<byte> header) =>
        StartsWith(header, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1);
}

/// <summary>Word (.docx) — ZIP arşivi ve içinde word/document.xml bulunmalı.</summary>
public sealed class DocxSignature : FileSignature
{
    public override string DisplayName => "Word (.docx)";
    public override IReadOnlyCollection<string> Extensions => [".docx"];
    public override bool MatchesHeader(ReadOnlySpan<byte> header) => StartsWith(header, 0x50, 0x4B, 0x03, 0x04);

    public override bool ValidateContent(Stream content)
    {
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            return zip.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
