using System.ComponentModel.DataAnnotations;
using DeskCore.Shared.Enums;
using DeskCore.Shared.Validation;

namespace DeskCore.Shared.Contracts.Tickets;

public sealed class CreateTicketRequest
{
    [LocRequired("Informe o título.", "Title is required."), LocStringLength(200, "O título deve ter de 3 a 200 caracteres.", "Title must be 3 to 200 characters.", MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [LocRequired("Informe a descrição.", "Description is required."), LocStringLength(10_000, "A descrição deve ter de 3 a 10.000 caracteres.", "Description must be 3 to 10,000 characters.", MinimumLength = 3)]
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    [LocRange(1, long.MaxValue, "Selecione uma categoria.", "Select a category.")]
    public long CategoryId { get; set; }
}

public sealed class UpdateTicketRequest
{
    [LocRequired("Informe o título.", "Title is required."), LocStringLength(200, "O título deve ter de 3 a 200 caracteres.", "Title must be 3 to 200 characters.", MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [LocRequired("Informe a descrição.", "Description is required."), LocStringLength(10_000, "A descrição deve ter de 3 a 10.000 caracteres.", "Description must be 3 to 10,000 characters.", MinimumLength = 3)]
    public string Description { get; set; } = string.Empty;
}

public sealed class ChangeStatusRequest
{
    [Required]
    public TicketStatus Status { get; set; }
}

public sealed class ChangePriorityRequest
{
    [Required]
    public TicketPriority Priority { get; set; }
}

public sealed class AssignTicketRequest
{
    [Required]
    public string AssignedToUserId { get; set; } = string.Empty;
}

public sealed record TicketListItemResponse(
    long Id,
    string Number,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    long CategoryId,
    string CategoryName,
    string CreatedByUserId,
    string? CreatedByName,
    string? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool HasUnseenActivity = false);

public sealed record TicketResponse(
    long Id,
    string Number,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    long CategoryId,
    string CategoryName,
    string CreatedByUserId,
    string? CreatedByName,
    string? AssignedToUserId,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    DateTime? CanceledAt);
