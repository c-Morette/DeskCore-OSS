namespace DeskCore.Domain.Constants;

/// <summary>
/// Limites e regras de validação de anexos (escopo §14). A validação real
/// (extensão + Content-Type + tamanho) é feita na camada de aplicação.
/// </summary>
public static class UploadConstraints
{
    /// <summary>10 MB por arquivo.</summary>
    public const long MaxFileSizeBytes = 10L * 1024 * 1024;

    /// <summary>Máximo de 5 arquivos por ticket.</summary>
    public const int MaxFilesPerTicket = 5;

    /// <summary>Extensões permitidas (comparação case-insensitive, com ponto).</summary>
    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".pdf", ".txt", ".log"
        };

    /// <summary>Extensões explicitamente bloqueadas (defesa em profundidade).</summary>
    public static readonly IReadOnlySet<string> BlockedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".html", ".htm",
            ".php", ".dll", ".scr", ".msi", ".jar", ".com", ".pif", ".reg"
        };

    /// <summary>Content-Types aceitos por extensão permitida.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> AllowedContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = new[] { "image/png" },
            [".jpg"] = new[] { "image/jpeg" },
            [".jpeg"] = new[] { "image/jpeg" },
            [".pdf"] = new[] { "application/pdf" },
            [".txt"] = new[] { "text/plain" },
            [".log"] = new[] { "text/plain", "application/octet-stream" }
        };

    /// <summary>
    /// Valida extensão + Content-Type + tamanho de forma pura (sem I/O).
    /// </summary>
    public static bool IsAllowed(string extension, string contentType, long fileSize)
    {
        if (fileSize <= 0 || fileSize > MaxFileSizeBytes)
            return false;

        if (string.IsNullOrWhiteSpace(extension) || BlockedExtensions.Contains(extension))
            return false;

        if (!AllowedExtensions.Contains(extension))
            return false;

        return AllowedContentTypes.TryGetValue(extension, out var types)
            && types.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }
}
