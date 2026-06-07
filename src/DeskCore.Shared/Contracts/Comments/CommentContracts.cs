using System.ComponentModel.DataAnnotations;

namespace DeskCore.Shared.Contracts.Comments;

public sealed class CreateCommentRequest
{
    [Required, StringLength(5_000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Apenas Agent/Admin podem marcar como interno; ignorado/rejeitado para User.</summary>
    public bool IsInternal { get; set; }
}

public sealed record CommentResponse(
    long Id,
    long TicketId,
    string UserId,
    string? UserName,
    string Message,
    bool IsInternal,
    DateTime CreatedAt);
