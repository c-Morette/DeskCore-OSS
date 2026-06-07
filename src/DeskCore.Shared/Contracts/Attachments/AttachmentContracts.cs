namespace DeskCore.Shared.Contracts.Attachments;

public sealed record AttachmentResponse(
    long Id,
    long TicketId,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    string UploadedByUserId,
    string? UploadedByName,
    DateTime CreatedAt);
