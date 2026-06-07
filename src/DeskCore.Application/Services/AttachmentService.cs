using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Abstractions.Storage;
using DeskCore.Application.Common;
using DeskCore.Application.Mapping;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Domain.Tickets;
using DeskCore.Shared.Contracts.Attachments;
using Microsoft.EntityFrameworkCore;

using DeskCore.Shared.Localization;

namespace DeskCore.Application.Services;

/// <summary>Conteúdo de um anexo para download (não é contrato serializável).</summary>
public sealed record AttachmentDownload(Stream Content, string ContentType, string FileName);

public interface IAttachmentService
{
    Task<Result<AttachmentResponse>> UploadAsync(long ticketId, string fileName, string contentType, long length, Stream content, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AttachmentResponse>>> ListAsync(long ticketId, CancellationToken ct = default);
    Task<Result<AttachmentDownload>> DownloadAsync(long ticketId, long attachmentId, CancellationToken ct = default);
    Task<Result> DeleteAsync(long ticketId, long attachmentId, CancellationToken ct = default);
}

public sealed class AttachmentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IFileStorage fileStorage,
    IAuditService audit) : IAttachmentService
{
    public async Task<Result<AttachmentResponse>> UploadAsync(long ticketId, string fileName, string contentType, long length, Stream content, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
            return Result<AttachmentResponse>.Forbidden(Msg.T("Usuário não autenticado.", "User not authenticated."));

        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<AttachmentResponse>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        if (TicketStateMachine.IsTerminal(ticket.Status))
            return Result<AttachmentResponse>.Validation(Msg.T("Ticket finalizado não aceita novos anexos.", "A finalized ticket does not accept new attachments."));

        var extension = Path.GetExtension(fileName);
        if (!UploadConstraints.IsAllowed(extension, contentType, length))
            return Result<AttachmentResponse>.Validation(Msg.T("Arquivo não permitido (extensão, tipo de conteúdo ou tamanho).", "File not allowed (extension, content type, or size)."));

        var count = await db.Attachments.CountAsync(a => a.TicketId == ticketId, ct);
        if (count >= UploadConstraints.MaxFilesPerTicket)
            return Result<AttachmentResponse>.Validation($"Limite de {UploadConstraints.MaxFilesPerTicket} arquivos por ticket atingido.");

        var stored = await fileStorage.SaveAsync(content, ticket.Number, extension, ct);

        // Defesa adicional: confere o tamanho real gravado.
        if (stored.FileSize <= 0 || stored.FileSize > UploadConstraints.MaxFileSizeBytes)
        {
            await fileStorage.DeleteAsync(stored.StoragePath, ct);
            return Result<AttachmentResponse>.Validation(Msg.T("Arquivo excede o tamanho máximo permitido.", "File exceeds the maximum allowed size."));
        }

        var now = DateTime.UtcNow;
        var attachment = new TicketAttachment
        {
            TicketId = ticketId,
            UploadedByUserId = currentUser.UserId!,
            OriginalFileName = Path.GetFileName(fileName), // apenas metadado
            StoredFileName = stored.StoredFileName,
            StoragePath = stored.StoragePath,
            ContentType = contentType,
            FileSize = stored.FileSize,
            Sha256Hash = stored.Sha256Hash,
            CreatedAt = now
        };
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(ticketId, AuditActions.AttachmentUploaded, null, attachment.OriginalFileName, ct);

        var saved = await db.Attachments.AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstAsync(a => a.Id == attachment.Id, ct);

        return Result<AttachmentResponse>.Success(saved.ToResponse());
    }

    public async Task<Result<IReadOnlyList<AttachmentResponse>>> ListAsync(long ticketId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<IReadOnlyList<AttachmentResponse>>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        var items = await db.Attachments.AsNoTracking()
            .Include(a => a.UploadedBy)
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<AttachmentResponse>>.Success(items.Select(a => a.ToResponse()).ToList());
    }

    public async Task<Result<AttachmentDownload>> DownloadAsync(long ticketId, long attachmentId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result<AttachmentDownload>.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        var attachment = await db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId, ct);
        if (attachment is null)
            return Result<AttachmentDownload>.NotFound(Msg.T("Anexo não encontrado.", "Attachment not found."));

        var stream = await fileStorage.OpenReadAsync(attachment.StoragePath, ct);
        await audit.LogTicketActionAsync(ticketId, AuditActions.AttachmentDownloaded, null, attachment.OriginalFileName, ct);

        return Result<AttachmentDownload>.Success(
            new AttachmentDownload(stream, attachment.ContentType, attachment.OriginalFileName));
    }

    public async Task<Result> DeleteAsync(long ticketId, long attachmentId, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !currentUser.CanAccessTicket(ticket))
            return Result.NotFound(Msg.T("Ticket não encontrado.", "Ticket not found."));

        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId, ct);
        if (attachment is null)
            return Result.NotFound(Msg.T("Anexo não encontrado.", "Attachment not found."));

        // Agent/Admin ou o próprio autor do upload.
        if (!currentUser.IsAgentOrAdmin() && attachment.UploadedByUserId != currentUser.UserId)
            return Result.Forbidden(Msg.T("Sem permissão para remover este anexo.", "You don't have permission to remove this attachment."));

        await fileStorage.DeleteAsync(attachment.StoragePath, ct);
        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);

        await audit.LogTicketActionAsync(ticketId, AuditActions.AttachmentDeleted, attachment.OriginalFileName, null, ct);
        return Result.Success();
    }
}
