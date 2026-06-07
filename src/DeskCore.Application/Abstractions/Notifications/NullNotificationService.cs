using DeskCore.Domain.Entities;

namespace DeskCore.Application.Abstractions.Notifications;

/// <summary>Implementação no-op de <see cref="INotificationService"/> para a V1.</summary>
public sealed class NullNotificationService : INotificationService
{
    public Task TicketCreatedAsync(Ticket ticket, CancellationToken ct = default) => Task.CompletedTask;
    public Task TicketAssignedAsync(Ticket ticket, CancellationToken ct = default) => Task.CompletedTask;
    public Task TicketStatusChangedAsync(Ticket ticket, CancellationToken ct = default) => Task.CompletedTask;
    public Task TicketCommentedAsync(Ticket ticket, TicketComment comment, CancellationToken ct = default) => Task.CompletedTask;
}
