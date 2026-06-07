namespace DeskCore.Domain.Entities;

/// <summary>
/// Anexo de um ticket. O nome original é apenas metadado; o arquivo é salvo
/// com um nome interno seguro, fora da pasta pública (escopo §14).
/// </summary>
public class TicketAttachment
{
    public long Id { get; set; }

    public long TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;
    public AppUser? UploadedBy { get; set; }

    /// <summary>Nome enviado pelo cliente. Apenas metadado — nunca usado para abrir/escrever no disco.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Nome interno seguro gerado pelo sistema.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Caminho relativo dentro do volume de uploads.</summary>
    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    /// <summary>Hash SHA-256 do conteúdo (integridade/deduplicação).</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
