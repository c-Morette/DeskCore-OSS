using DeskCore.Domain.Entities;

namespace DeskCore.Application.Abstractions.Notifications;

/// <summary>
/// Notificações de eventos de ticket. Na V1 existe apenas a implementação nula
/// (<c>NullNotificationService</c>); o envio real fica para versões futuras.
/// </summary>
public interface INotificationService
{
    Task TicketCreatedAsync(Ticket ticket, CancellationToken ct = default);
    Task TicketAssignedAsync(Ticket ticket, CancellationToken ct = default);
    Task TicketStatusChangedAsync(Ticket ticket, CancellationToken ct = default);
    Task TicketCommentedAsync(Ticket ticket, TicketComment comment, CancellationToken ct = default);
}
