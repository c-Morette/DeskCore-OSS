using DeskCore.Shared.Enums;

namespace DeskCore.Domain.Entities;

/// <summary>
/// Ticket de atendimento. Inicia em <see cref="TicketStatus.WaitingAgent"/>.
/// As transições de status são governadas por <c>TicketStateMachine</c>.
/// </summary>
public class Ticket
{
    public long Id { get; set; }

    /// <summary>Identificador amigável (ex.: <c>TK-000001</c>), gerado por sequence do PostgreSQL.</summary>
    public string Number { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.WaitingAgent;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public long CategoryId { get; set; }
    public TicketCategory? Category { get; set; }

    /// <summary>Obrigatório. Usuário que abriu o ticket.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;
    public AppUser? CreatedBy { get; set; }

    /// <summary>Nulo até um Agent assumir / Admin atribuir.</summary>
    public string? AssignedToUserId { get; set; }
    public AppUser? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    // Navegações
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
