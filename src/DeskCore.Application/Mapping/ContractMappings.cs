using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Attachments;
using DeskCore.Shared.Contracts.Audit;
using DeskCore.Shared.Contracts.Categories;
using DeskCore.Shared.Contracts.Comments;
using DeskCore.Shared.Contracts.Tickets;

namespace DeskCore.Application.Mapping;

/// <summary>
/// Mapeamentos entidade → contrato público (Shared). Aplicados em memória,
/// após materializar as entidades com as navegações necessárias.
/// </summary>
internal static class ContractMappings
{
    public static TicketResponse ToResponse(this Ticket t) => new(
        t.Id, t.Number, t.Title, t.Description, t.Status, t.Priority,
        t.CategoryId, t.Category?.Name ?? string.Empty,
        t.CreatedByUserId, t.CreatedBy?.FullName,
        t.AssignedToUserId, t.AssignedTo?.FullName,
        t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ClosedAt, t.CanceledAt);

    public static TicketListItemResponse ToListItem(this Ticket t, bool hasUnseenActivity = false) => new(
        t.Id, t.Number, t.Title, t.Status, t.Priority,
        t.CategoryId, t.Category?.Name ?? string.Empty,
        t.CreatedByUserId, t.CreatedBy?.FullName,
        t.AssignedToUserId, t.AssignedTo?.FullName,
        t.CreatedAt, t.UpdatedAt, hasUnseenActivity);

    public static CommentResponse ToResponse(this TicketComment c) => new(
        c.Id, c.TicketId, c.UserId, c.User?.FullName, c.Message, c.IsInternal, c.CreatedAt);

    public static AttachmentResponse ToResponse(this TicketAttachment a) => new(
        a.Id, a.TicketId, a.OriginalFileName, a.ContentType, a.FileSize,
        a.UploadedByUserId, a.UploadedBy?.FullName, a.CreatedAt);

    public static CategoryResponse ToResponse(this TicketCategory c) => new(
        c.Id, c.Name, c.Description, c.IsActive, c.CreatedAt, c.UpdatedAt);

    public static TicketAuditLogResponse ToResponse(this TicketAuditLog l) => new(
        l.Id, l.TicketId, l.Action, l.OldValue, l.NewValue, l.UserId, l.User?.FullName, l.IpAddress, l.CreatedAt);

    public static LoginAuditLogResponse ToResponse(this LoginAuditLog l) => new(
        l.Id, l.UserId, l.Email, l.IpAddress, l.UserAgent, l.Success, l.FailureReason, l.CreatedAt);
}
