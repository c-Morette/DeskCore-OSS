using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Common;
using DeskCore.Application.Mapping;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Shared.Contracts.Audit;
using DeskCore.Shared.Contracts.Common;
using Microsoft.EntityFrameworkCore;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

public interface IAuditService
{
    /// <summary>Registra uma ação sobre um ticket (usa o usuário/IP/UA atuais).</summary>
    Task LogTicketActionAsync(long ticketId, string action, string? oldValue, string? newValue, CancellationToken ct = default);

    /// <summary>Registra uma tentativa de login.</summary>
    Task LogLoginAsync(string email, string? userId, bool success, string? failureReason, CancellationToken ct = default);

    Task<Result<IReadOnlyList<TicketAuditLogResponse>>> GetTicketAuditAsync(long ticketId, CancellationToken ct = default);

    /// <summary>Feed global de atividade recente sobre tickets (atendimento/admin).</summary>
    Task<Result<IReadOnlyList<RecentActivityResponse>>> GetRecentActivityAsync(int take, CancellationToken ct = default);

    /// <summary>Logins recentes (uso administrativo — autorização garantida na API).</summary>
    Task<PagedResult<LoginAuditLogResponse>> GetLoginAuditsAsync(int page, int pageSize, CancellationToken ct = default);
}

public sealed class AuditService(IAppDbContext db, ICurrentUser currentUser) : IAuditService
{
    public async Task LogTicketActionAsync(long ticketId, string action, string? oldValue, string? newValue, CancellationToken ct = default)
    {
        db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticketId,
            UserId = currentUser.UserId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task LogLoginAsync(string email, string? userId, bool success, string? failureReason, CancellationToken ct = default)
    {
        db.LoginAuditLogs.Add(new LoginAuditLog
        {
            Email = email,
            UserId = userId,
            Success = success,
            FailureReason = failureReason,
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result<IReadOnlyList<TicketAuditLogResponse>>> GetTicketAuditAsync(long ticketId, CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<IReadOnlyList<TicketAuditLogResponse>>.Forbidden(Msg.T("Apenas atendentes ou administradores podem ver a auditoria.", "Only agents or administrators can view the audit log."));

        if (!await db.Tickets.AnyAsync(t => t.Id == ticketId, ct))
            return Result<IReadOnlyList<TicketAuditLogResponse>>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        var logs = await db.TicketAuditLogs
            .AsNoTracking()
            .Include(l => l.User)
            .Where(l => l.TicketId == ticketId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<TicketAuditLogResponse>>.Success(logs.Select(l => l.ToResponse()).ToList());
    }

    public async Task<Result<IReadOnlyList<RecentActivityResponse>>> GetRecentActivityAsync(int take, CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<IReadOnlyList<RecentActivityResponse>>.Forbidden(Msg.T("Apenas atendentes ou administradores podem ver a atividade recente.", "Only agents or administrators can view recent activity."));

        take = Math.Clamp(take, 1, 50);

        // Downloads de anexo são ruído para um feed de "o que mudou" — excluídos.
        var items = await db.TicketAuditLogs
            .AsNoTracking()
            .Where(l => l.Action != AuditActions.AttachmentDownloaded)
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new RecentActivityResponse(
                l.Id,
                l.TicketId,
                l.Ticket!.Number,
                l.Ticket.Title,
                l.Action,
                l.User != null ? l.User.FullName : null,
                l.CreatedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RecentActivityResponse>>.Success(items);
    }

    public async Task<PagedResult<LoginAuditLogResponse>> GetLoginAuditsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.LoginAuditLogs.AsNoTracking().OrderByDescending(l => l.CreatedAt);
        var total = await query.LongCountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<LoginAuditLogResponse>(
            items.Select(l => l.ToResponse()).ToList(), page, pageSize, total);
    }
}
