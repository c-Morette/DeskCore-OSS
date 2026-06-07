namespace DeskCore.Shared.Contracts.Audit;

public sealed record TicketAuditLogResponse(
    long Id,
    long TicketId,
    string Action,
    string? OldValue,
    string? NewValue,
    string? UserId,
    string? UserName,
    string? IpAddress,
    DateTime CreatedAt);

/// <summary>Item do feed global de atividade recente (atendimento/admin).</summary>
public sealed record RecentActivityResponse(
    long Id,
    long TicketId,
    string TicketNumber,
    string TicketTitle,
    string Action,
    string? UserName,
    DateTime CreatedAt);

public sealed record LoginAuditLogResponse(
    long Id,
    string? UserId,
    string Email,
    string? IpAddress,
    string? UserAgent,
    bool Success,
    string? FailureReason,
    DateTime CreatedAt);
