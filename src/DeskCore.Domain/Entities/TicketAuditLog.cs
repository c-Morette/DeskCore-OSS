namespace DeskCore.Domain.Entities;

/// <summary>
/// Registro de auditoria de uma ação sobre um ticket (escopo §15).
/// <see cref="Action"/> usa as constantes de <c>AuditActions</c>.
/// </summary>
public class TicketAuditLog
{
    public long Id { get; set; }

    public long TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    /// <summary>Autor da ação. Nulo quando a ação parte do próprio sistema.</summary>
    public string? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}
