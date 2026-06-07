namespace DeskCore.Domain.Entities;

/// <summary>
/// Comentário em um ticket. Comentários internos (<see cref="IsInternal"/>) são
/// visíveis apenas para Agent/Admin (escopo §13).
/// </summary>
public class TicketComment
{
    public long Id { get; set; }

    public long TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Quando <c>true</c>, visível apenas para Agent/Admin.</summary>
    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
