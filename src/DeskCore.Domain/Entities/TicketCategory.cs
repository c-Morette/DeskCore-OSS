namespace DeskCore.Domain.Entities;

/// <summary>
/// Categoria administrável de tickets. Nunca é deletada fisicamente se houver
/// tickets vinculados — apenas desativada (escopo §11).
/// </summary>
public class TicketCategory
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegação
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
