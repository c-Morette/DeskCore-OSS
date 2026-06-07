using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Notifications;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Abstractions.Tickets;
using DeskCore.Application.Common;
using DeskCore.Application.Mapping;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Domain.Tickets;
using DeskCore.Shared.Contracts.Common;
using DeskCore.Shared.Contracts.Tickets;
using DeskCore.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

public interface ITicketService
{
    Task<Result<TicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketResponse>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PagedResult<TicketListItemResponse>> ListAsync(
        TicketStatus? status, int page, int pageSize,
        string? number = null, string? title = null, string? description = null, string? requester = null,
        bool unseenOnly = false,
        CancellationToken ct = default);
    Task<Result<TicketResponse>> UpdateAsync(long id, UpdateTicketRequest request, CancellationToken ct = default);

    /// <summary>Marca o ticket como visto pelo usuário atual (upsert da última visita).</summary>
    Task MarkReadAsync(long id, CancellationToken ct = default);

    /// <summary>Quantidade de tickets (no escopo do usuário) com atividade não vista.</summary>
    Task<int> GetUnseenCountAsync(CancellationToken ct = default);
    Task<Result<TicketResponse>> AssignAsync(long id, AssignTicketRequest request, CancellationToken ct = default);
    Task<Result<TicketResponse>> ChangeStatusAsync(long id, TicketStatus newStatus, CancellationToken ct = default);
    Task<Result<TicketResponse>> ChangePriorityAsync(long id, TicketPriority newPriority, CancellationToken ct = default);
    Task<Result<TicketResponse>> CloseAsync(long id, CancellationToken ct = default);
    Task<Result<TicketResponse>> CancelAsync(long id, CancellationToken ct = default);
}

public sealed class TicketService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITicketNumberGenerator numbers,
    IAuditService audit,
    INotificationService notifications,
    UserManager<AppUser> userManager) : ITicketService
{
    public async Task<Result<TicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            return Result<TicketResponse>.Forbidden(Msg.T("Usuário não autenticado.", "User not authenticated."));

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
            return Result<TicketResponse>.Validation(Msg.T("Categoria não encontrada.", "Category not found."));
        if (!category.IsActive)
            return Result<TicketResponse>.Validation(Msg.T("Categoria inativa.", "Inactive category."));

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Number = await numbers.NextAsync(ct),
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = TicketStateMachine.InitialStatus,
            Priority = request.Priority,
            CategoryId = category.Id,
            CreatedByUserId = currentUser.UserId!,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(ticket.Id, AuditActions.TicketCreated, null, ticket.Number, ct);
        await notifications.TicketCreatedAsync(ticket, ct);

        return await GetByIdAsync(ticket.Id, ct);
    }

    public async Task<Result<TicketResponse>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var ticket = await QueryWithDetails().FirstOrDefaultAsync(t => t.Id == id, ct);

        // Não revela existência de tickets de outros usuários (anti-IDOR).
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        return Result<TicketResponse>.Success(ticket.ToResponse());
    }

    // Ações que NÃO contam como "novidade" no indicador: download é ruído; o cliente
    // ainda ignora comentário interno (que ele nem vê).
    private static readonly string[] StaffUnseenExcluded = [AuditActions.AttachmentDownloaded];
    private static readonly string[] ClientUnseenExcluded = [AuditActions.AttachmentDownloaded, AuditActions.InternalCommentAdded];

    /// <summary>
    /// Predicado "tem atividade não vista para este usuário": existe uma ação de auditoria
    /// feita por OUTRA pessoa (ou pelo sistema), de tipo relevante, que o usuário ainda não
    /// "viu" (não há registro de leitura com LastSeenAt &gt;= a data da ação).
    /// </summary>
    private Expression<Func<Ticket, bool>> HasUnseen(string userId, string[] excluded) =>
        t => db.TicketAuditLogs.Any(a =>
            a.TicketId == t.Id
            && (a.UserId == null || a.UserId != userId)
            && !excluded.Contains(a.Action)
            && !db.TicketReads.Any(r => r.UserId == userId && r.TicketId == t.Id && r.LastSeenAt >= a.CreatedAt));

    public async Task<PagedResult<TicketListItemResponse>> ListAsync(
        TicketStatus? status, int page, int pageSize,
        string? number = null, string? title = null, string? description = null, string? requester = null,
        bool unseenOnly = false,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = QueryWithDetails();

        // Escopo por papel: cliente só enxerga os próprios tickets.
        var isStaff = currentUser.IsAgentOrAdmin();
        var userId = currentUser.UserId ?? string.Empty;
        var excluded = isStaff ? StaffUnseenExcluded : ClientUnseenExcluded;
        if (!isStaff)
            query = query.Where(t => t.CreatedByUserId == currentUser.UserId);

        if (unseenOnly)
            query = query.Where(HasUnseen(userId, excluded));

        if (status is not null)
            query = query.Where(t => t.Status == status);

        // Busca textual case-insensitive (ToLower+Contains → lower() LIKE '%..%' parametrizado;
        // provider-agnostic, sem depender do Npgsql na camada Application).
        if (!string.IsNullOrWhiteSpace(number))
        {
            var n = number.Trim().ToLower();
            query = query.Where(t => t.Number.ToLower().Contains(n));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var ti = title.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(ti));
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            var d = description.Trim().ToLower();
            query = query.Where(t => t.Description.ToLower().Contains(d));
        }

        // Filtro por solicitante (nome ou e-mail) é exclusivo de Agent/Admin.
        if (isStaff && !string.IsNullOrWhiteSpace(requester))
        {
            var r = requester.Trim().ToLower();
            query = query.Where(t => t.CreatedBy != null &&
                (t.CreatedBy.FullName.ToLower().Contains(r) ||
                 (t.CreatedBy.Email != null && t.CreatedBy.Email.ToLower().Contains(r))));
        }

        var ordered = query.OrderByDescending(t => t.CreatedAt);
        var total = await ordered.LongCountAsync(ct);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // Quais dos tickets da página têm novidade não vista para o usuário atual.
        var ids = items.Select(i => i.Id).ToList();
        var unseenIds = ids.Count == 0
            ? new HashSet<long>()
            : (await db.Tickets.AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .Where(HasUnseen(userId, excluded))
                .Select(t => t.Id)
                .ToListAsync(ct)).ToHashSet();

        return new PagedResult<TicketListItemResponse>(
            items.Select(t => t.ToListItem(unseenIds.Contains(t.Id))).ToList(), page, pageSize, total);
    }

    public async Task MarkReadAsync(long id, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return;

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return;

        var read = await db.TicketReads.FirstOrDefaultAsync(r => r.UserId == userId && r.TicketId == id, ct);
        if (read is null)
            db.TicketReads.Add(new TicketRead { TicketId = id, UserId = userId, LastSeenAt = DateTime.UtcNow });
        else
            read.LastSeenAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnseenCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return 0;

        var isStaff = currentUser.IsAgentOrAdmin();
        var excluded = isStaff ? StaffUnseenExcluded : ClientUnseenExcluded;

        var scope = db.Tickets.AsNoTracking();
        if (!isStaff)
            scope = scope.Where(t => t.CreatedByUserId == userId);

        return await scope.CountAsync(HasUnseen(userId, excluded), ct);
    }

    public async Task<Result<TicketResponse>> UpdateAsync(long id, UpdateTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        if (TicketStateMachine.IsTerminal(ticket.Status))
            return Result<TicketResponse>.Validation(Msg.T("Ticket finalizado não pode ser editado.", "A finalized ticket cannot be edited."));

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(id, AuditActions.TicketUpdated, null, null, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<Result<TicketResponse>> AssignAsync(long id, AssignTicketRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<TicketResponse>.Forbidden(Msg.T("Apenas atendentes ou administradores podem atribuir tickets.", "Only agents or administrators can assign tickets."));

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));
        if (TicketStateMachine.IsTerminal(ticket.Status))
            return Result<TicketResponse>.Validation(Msg.T("Ticket finalizado não pode ser atribuído.", "A finalized ticket cannot be assigned."));

        var assignee = await userManager.FindByIdAsync(request.AssignedToUserId);
        if (assignee is null || !assignee.IsActive)
            return Result<TicketResponse>.Validation(Msg.T("Usuário de destino inválido.", "Invalid target user."));

        var assigneeRoles = await userManager.GetRolesAsync(assignee);
        if (!assigneeRoles.Contains(Roles.Agent) && !assigneeRoles.Contains(Roles.Admin))
            return Result<TicketResponse>.Validation(Msg.T("O ticket só pode ser atribuído a atendentes ou administradores.", "The ticket can only be assigned to agents or administrators."));

        var now = DateTime.UtcNow;
        var oldAssignee = ticket.AssignedToUserId;
        ticket.AssignedToUserId = assignee.Id;
        ticket.UpdatedAt = now;

        // Fluxo: ao atribuir um ticket aguardando atendente, ele entra em atendimento.
        var statusChanged = false;
        if (ticket.Status == TicketStatus.WaitingAgent)
        {
            ticket.Status = TicketStatus.InProgress;
            statusChanged = true;
        }

        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(id, AuditActions.AssigneeChanged, oldAssignee, assignee.Id, ct);
        if (statusChanged)
            await audit.LogTicketActionAsync(id, AuditActions.StatusChanged, TicketStatus.WaitingAgent.ToString(), TicketStatus.InProgress.ToString(), ct);
        await notifications.TicketAssignedAsync(ticket, ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result<TicketResponse>> ChangeStatusAsync(long id, TicketStatus newStatus, CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<TicketResponse>.Forbidden(Msg.T("Apenas atendentes ou administradores podem alterar o status.", "Only agents or administrators can change the status."));

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        if (!TicketStateMachine.CanTransition(ticket.Status, newStatus))
            return Result<TicketResponse>.Validation($"Transição inválida de {ticket.Status} para {newStatus}.");

        await ApplyStatusAsync(ticket, newStatus, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<Result<TicketResponse>> ChangePriorityAsync(long id, TicketPriority newPriority, CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<TicketResponse>.Forbidden(Msg.T("Apenas atendentes ou administradores podem alterar a prioridade.", "Only agents or administrators can change the priority."));

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));
        if (TicketStateMachine.IsTerminal(ticket.Status))
            return Result<TicketResponse>.Validation(Msg.T("Ticket finalizado não pode ter a prioridade alterada.", "A finalized ticket cannot have its priority changed."));

        var old = ticket.Priority;
        ticket.Priority = newPriority;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(id, AuditActions.PriorityChanged, old.ToString(), newPriority.ToString(), ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<Result<TicketResponse>> CloseAsync(long id, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        if (currentUser.IsAgentOrAdmin())
        {
            if (!TicketStateMachine.CanTransition(ticket.Status, TicketStatus.Closed))
                return Result<TicketResponse>.Validation(Msg.T("Não é possível fechar o ticket a partir do status atual.", "The ticket cannot be closed from its current status."));
        }
        else
        {
            // User só fecha o próprio ticket e apenas em status permitido (escopo §12).
            if (ticket.CreatedByUserId != currentUser.UserId)
                return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));
            if (!TicketStateMachine.UserClosableStatuses.Contains(ticket.Status))
                return Result<TicketResponse>.Validation(Msg.T("Você só pode fechar tickets resolvidos ou aguardando sua resposta.", "You can only close tickets that are resolved or awaiting your response."));
        }

        await ApplyStatusAsync(ticket, TicketStatus.Closed, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<Result<TicketResponse>> CancelAsync(long id, CancellationToken ct = default)
    {
        if (!currentUser.IsAgentOrAdmin())
            return Result<TicketResponse>.Forbidden(Msg.T("Apenas atendentes ou administradores podem cancelar tickets.", "Only agents or administrators can cancel tickets."));

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return Result<TicketResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        if (!TicketStateMachine.CanTransition(ticket.Status, TicketStatus.Canceled))
            return Result<TicketResponse>.Validation(Msg.T("Não é possível cancelar o ticket a partir do status atual.", "The ticket cannot be canceled from its current status."));

        await ApplyStatusAsync(ticket, TicketStatus.Canceled, ct);
        return await GetByIdAsync(id, ct);
    }

    private async Task ApplyStatusAsync(Ticket ticket, TicketStatus newStatus, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var old = ticket.Status;
        ticket.Status = newStatus;
        ticket.UpdatedAt = now;

        switch (newStatus)
        {
            case TicketStatus.Resolved: ticket.ResolvedAt = now; break;
            case TicketStatus.Closed: ticket.ClosedAt = now; break;
            case TicketStatus.Canceled: ticket.CanceledAt = now; break;
        }

        await db.SaveChangesAsync(ct);

        var action = newStatus switch
        {
            TicketStatus.Resolved => AuditActions.TicketResolved,
            TicketStatus.Closed => AuditActions.TicketClosed,
            TicketStatus.Canceled => AuditActions.TicketCanceled,
            _ => AuditActions.StatusChanged
        };
        await audit.LogTicketActionAsync(ticket.Id, action, old.ToString(), newStatus.ToString(), ct);
        await notifications.TicketStatusChangedAsync(ticket, ct);
    }

    private IQueryable<Ticket> QueryWithDetails() =>
        db.Tickets.AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo);
}
