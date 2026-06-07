namespace DeskCore.Domain.Entities;

/// <summary>
/// Marca a última vez que um usuário visualizou um ticket — base do indicador de
/// "novidade não vista" (notificações). Um registro por usuário×ticket.
/// </summary>
public class TicketRead
{
    public long Id { get; set; }

    public long TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public DateTime LastSeenAt { get; set; }
}
