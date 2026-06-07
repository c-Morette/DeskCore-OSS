using System.Security.Cryptography;
using DeskCore.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace DeskCore.Infrastructure.Storage;

/// <summary>
/// Armazenamento local de anexos (escopo §14). Salva com nome interno seguro
/// (<c>GUID</c>), calcula SHA-256 durante a gravação e impede path traversal
/// na leitura/remoção.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string ticketNumber, string extension, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var relativeDir = Path.Combine("tickets", now.ToString("yyyy"), now.ToString("MM"), SanitizeSegment(ticketNumber));
        var absoluteDir = Path.Combine(_root, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var storedFileName = $"{Guid.NewGuid():N}{NormalizeExtension(extension)}";
        var relativePath = Path.Combine(relativeDir, storedFileName);
        var absolutePath = Path.Combine(_root, relativePath);

        long size;
        byte[] hash;

        await using (var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var sha = SHA256.Create())
        await using (var crypto = new CryptoStream(output, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(crypto, ct);
            await crypto.FlushFinalBlockAsync(ct);
            size = output.Length;
            hash = sha.Hash!;
        }

        return new StoredFile(
            storedFileName,
            relativePath.Replace('\\', '/'),
            size,
            Convert.ToHexStringLower(hash));
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var absolute = ResolveSafePath(storagePath);
        Stream stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var absolute = ResolveSafePath(storagePath);
        if (File.Exists(absolute))
            File.Delete(absolute);
        return Task.CompletedTask;
    }

    /// <summary>Resolve o caminho absoluto garantindo que permaneça sob a raiz (anti path traversal).</summary>
    private string ResolveSafePath(string storagePath)
    {
        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        var combined = Path.GetFullPath(Path.Combine(rootWithSep, storagePath));

        if (!combined.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Caminho de arquivo fora do diretório permitido.");

        return combined;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return ext.ToLowerInvariant();
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(segment.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
