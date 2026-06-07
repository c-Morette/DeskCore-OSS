using System.ComponentModel.DataAnnotations;

namespace DeskCore.Shared.Contracts.Account;

/// <summary>Correção de dados do próprio titular (LGPD, Art. 18 — retificação).</summary>
public sealed class UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;
}

/// <summary>Pacote de dados pessoais do titular (LGPD, Art. 18 — acesso/portabilidade).</summary>
public sealed record AccountDataExport(
    DateTime ExportedAt,
    string UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? PrivacyConsentAt,
    string? PrivacyPolicyVersion,
    IReadOnlyList<ExportedTicket> Tickets,
    IReadOnlyList<ExportedComment> Comments);

public sealed record ExportedTicket(
    string Number,
    string Title,
    string Description,
    string Status,
    string Priority,
    DateTime CreatedAt);

public sealed record ExportedComment(
    string TicketNumber,
    string Message,
    bool IsInternal,
    DateTime CreatedAt);
