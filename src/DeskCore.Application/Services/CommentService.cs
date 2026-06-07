using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Notifications;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Common;
using DeskCore.Application.Mapping;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Domain.Tickets;
using DeskCore.Shared.Contracts.Comments;
using Microsoft.EntityFrameworkCore;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

public interface ICommentService
{
    Task<Result<CommentResponse>> AddAsync(long ticketId, CreateCommentRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CommentResponse>>> ListAsync(long ticketId, CancellationToken ct = default);
}

public sealed class CommentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    INotificationService notifications) : ICommentService
{
    public async Task<Result<CommentResponse>> AddAsync(long ticketId, CreateCommentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            return Result<CommentResponse>.Forbidden(Msg.T("Usuário não autenticado.", "User not authenticated."));

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<CommentResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        if (TicketStateMachine.IsTerminal(ticket.Status))
            return Result<CommentResponse>.Validation(Msg.T("Ticket finalizado não aceita novos comentários.", "A finalized ticket does not accept new comments."));

        // User comum nunca cria comentário interno (escopo §13).
        var isInternal = request.IsInternal && currentUser.IsAgentOrAdmin();

        var now = DateTime.UtcNow;
        var comment = new TicketComment
        {
            TicketId = ticketId,
            UserId = currentUser.UserId!,
            Message = request.Message,
            IsInternal = isInternal,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(
            ticketId,
            isInternal ? AuditActions.InternalCommentAdded : AuditActions.CommentAdded,
            null, null, ct);
        await notifications.TicketCommentedAsync(ticket, comment, ct);

        var saved = await db.Comments.AsNoTracking()
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id, ct);

        return Result<CommentResponse>.Success(saved.ToResponse());
    }

    public async Task<Result<IReadOnlyList<CommentResponse>>> ListAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<IReadOnlyList<CommentResponse>>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        var query = db.Comments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.TicketId == ticketId);

        // User comum não enxerga comentários internos.
        if (!currentUser.IsAgentOrAdmin())
            query = query.Where(c => !c.IsInternal);

        var comments = await query.OrderBy(c => c.CreatedAt).ToListAsync(ct);
        return Result<IReadOnlyList<CommentResponse>>.Success(comments.Select(c => c.ToResponse()).ToList());
    }
}
