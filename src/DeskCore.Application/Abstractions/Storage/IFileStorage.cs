namespace DeskCore.Application.Abstractions.Storage;

/// <summary>
/// Persistência de anexos fora da pasta pública. A validação (extensão,
/// Content-Type, tamanho) é responsabilidade do serviço de aplicação;
/// aqui apenas grava/lê/remove o conteúdo de forma segura.
/// </summary>
public interface IFileStorage
{
    /// <summary>Grava o conteúdo e devolve metadados (nome interno, caminho relativo, tamanho e SHA-256).</summary>
    Task<StoredFile> SaveAsync(Stream content, string ticketNumber, string extension, CancellationToken ct = default);

    /// <summary>Abre o arquivo para leitura. Valida que o caminho está dentro do diretório raiz.</summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);

    /// <summary>Remove o arquivo, se existir.</summary>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

/// <summary>Resultado da gravação de um anexo.</summary>
public sealed record StoredFile(string StoredFileName, string StoragePath, long FileSize, string Sha256Hash);
