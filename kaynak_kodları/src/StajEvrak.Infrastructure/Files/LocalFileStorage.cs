using StajEvrak.Application.Abstractions;

namespace StajEvrak.Infrastructure.Files;

/// <summary>Dosyaları sunucu diskinde (web kökü dışında) rastgele adlarla saklar.</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string rootPath)
    {
        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct = default)
    {
        var safeFolder = string.Concat(folder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        var safeExt = "." + string.Concat(extension.TrimStart('.').Where(char.IsLetterOrDigit)).ToLowerInvariant();
        var relative = Path.Combine(safeFolder, $"{Guid.NewGuid():N}{safeExt}");
        var full = GetFullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return relative.Replace('\\', '/');
    }

    public string GetFullPath(string storedPath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storedPath));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Geçersiz dosya yolu.");
        return full;
    }

    public void Delete(string storedPath)
    {
        var full = GetFullPath(storedPath);
        if (File.Exists(full)) File.Delete(full);
    }
}
