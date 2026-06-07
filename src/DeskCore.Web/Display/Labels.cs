using DeskCore.Shared.Enums;

namespace DeskCore.Web.Display;

/// <summary>Rótulos PT-BR e classes de badge para enums (apresentação).</summary>
public static class Labels
{
    public static string Status(TicketStatus status) => status switch
    {
        TicketStatus.Open => Loc.T("Aberto", "Open"),
        TicketStatus.WaitingAgent => Loc.T("Aguardando Atendente", "Waiting for Agent"),
        TicketStatus.InProgress => Loc.T("Em Atendimento", "In Progress"),
        TicketStatus.WaitingUser => Loc.T("Aguardando Usuário", "Waiting for User"),
        TicketStatus.Resolved => Loc.T("Resolvido", "Resolved"),
        TicketStatus.Closed => Loc.T("Fechado", "Closed"),
        TicketStatus.Canceled => Loc.T("Cancelado", "Canceled"),
        _ => status.ToString()
    };

    public static string StatusBadge(TicketStatus status) => status switch
    {
        TicketStatus.WaitingAgent => "dc-badge dc-badge--gold",
        TicketStatus.InProgress => "dc-badge dc-badge--green",
        TicketStatus.WaitingUser => "dc-badge dc-badge--blue",
        TicketStatus.Resolved => "dc-badge dc-badge--teal",
        TicketStatus.Closed => "dc-badge dc-badge--muted",
        TicketStatus.Canceled => "dc-badge dc-badge--red",
        _ => "dc-badge dc-badge--muted"
    };

    public static string Priority(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => Loc.T("Baixa", "Low"),
        TicketPriority.Medium => Loc.T("Média", "Medium"),
        TicketPriority.High => Loc.T("Alta", "High"),
        TicketPriority.Critical => Loc.T("Crítica", "Critical"),
        _ => priority.ToString()
    };

    public static string PriorityBadge(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "dc-badge dc-badge--muted",
        TicketPriority.Medium => "dc-badge dc-badge--blue",
        TicketPriority.High => "dc-badge dc-badge--orange",
        TicketPriority.Critical => "dc-badge dc-badge--red",
        _ => "dc-badge dc-badge--muted"
    };

    public static string Role(string role) => role switch
    {
        "User" => Loc.T("Usuário", "User"),
        "Agent" => Loc.T("Atendente", "Agent"),
        "Admin" => Loc.T("Administrador", "Administrator"),
        _ => role
    };

    /// <summary>Rótulo PT-BR para uma ação de auditoria (constantes de AuditActions).</summary>
    public static string AuditAction(string action) => action switch
    {
        "TicketCreated" => Loc.T("Ticket criado", "Ticket created"),
        "TicketUpdated" => Loc.T("Ticket editado", "Ticket edited"),
        "StatusChanged" => Loc.T("Status alterado", "Status changed"),
        "PriorityChanged" => Loc.T("Prioridade alterada", "Priority changed"),
        "CategoryChanged" => Loc.T("Categoria alterada", "Category changed"),
        "AssigneeChanged" => Loc.T("Responsável alterado", "Assignee changed"),
        "CommentAdded" => Loc.T("Comentário adicionado", "Comment added"),
        "InternalCommentAdded" => Loc.T("Comentário interno", "Internal comment"),
        "AttachmentUploaded" => Loc.T("Anexo enviado", "Attachment uploaded"),
        "AttachmentDownloaded" => Loc.T("Anexo baixado", "Attachment downloaded"),
        "AttachmentDeleted" => Loc.T("Anexo removido", "Attachment removed"),
        "TicketResolved" => Loc.T("Ticket resolvido", "Ticket resolved"),
        "TicketClosed" => Loc.T("Ticket fechado", "Ticket closed"),
        "TicketCanceled" => Loc.T("Ticket cancelado", "Ticket canceled"),
        _ => action
    };

    /// <summary>Classe de cor (badge/realce) para a ação de auditoria.</summary>
    public static string AuditActionColor(string action) => action switch
    {
        "TicketCreated" => "gold",
        "CommentAdded" or "InternalCommentAdded" => "blue",
        "AttachmentUploaded" => "teal",
        "AttachmentDeleted" => "red",
        "TicketResolved" => "green",
        "TicketClosed" => "muted",
        "TicketCanceled" => "red",
        "StatusChanged" => "green",
        "PriorityChanged" => "orange",
        "AssigneeChanged" => "blue",
        _ => "muted"
    };
}
