namespace DeskCore.Domain.Constants;

/// <summary>
/// Ações registradas na auditoria de tickets (escopo §15).
/// </summary>
public static class AuditActions
{
    public const string TicketCreated = "TicketCreated";
    public const string TicketUpdated = "TicketUpdated";
    public const string StatusChanged = "StatusChanged";
    public const string PriorityChanged = "PriorityChanged";
    public const string CategoryChanged = "CategoryChanged";
    public const string AssigneeChanged = "AssigneeChanged";
    public const string CommentAdded = "CommentAdded";
    public const string InternalCommentAdded = "InternalCommentAdded";
    public const string AttachmentUploaded = "AttachmentUploaded";
    public const string AttachmentDownloaded = "AttachmentDownloaded";
    public const string AttachmentDeleted = "AttachmentDeleted";
    public const string TicketResolved = "TicketResolved";
    public const string TicketClosed = "TicketClosed";
    public const string TicketCanceled = "TicketCanceled";
}
