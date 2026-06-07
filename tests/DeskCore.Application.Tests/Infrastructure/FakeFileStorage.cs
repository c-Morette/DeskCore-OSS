using System.Security.Cryptography;
using DeskCore.Application.Abstractions.Storage;

namespace DeskCore.Application.Tests.Infrastructure;

/// <summary>
/// <see cref="IFileStorage"/> em memória. Não toca disco; preserva o conteúdo,
/// tamanho real e SHA-256 para que o <c>AttachmentService</c> exerça suas
/// checagens (tamanho gravado, anti path-traversal é responsabilidade do storage
/// real e não é alvo deste tier).
/// </summary>
public sealed class FakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new();
    private int _counter;

    public int SavedCount => _files.Count;

    public async Task<StoredFile> SaveAsync(Stream content, string ticketNumber, string extension, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var storedName = $"{ticketNumber}-{Interlocked.Increment(ref _counter)}{extension}";
        var path = $"{ticketNumber}/{storedName}";
        _files[path] = bytes;

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StoredFile(storedName, path, bytes.LongLength, hash);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(storagePath, out var bytes))
            throw new FileNotFoundException(storagePath);
        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        _files.Remove(storagePath);
        return Task.CompletedTask;
    }
}
