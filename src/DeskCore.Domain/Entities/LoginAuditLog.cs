namespace DeskCore.Domain.Entities;

/// <summary>
/// Registro de tentativa de login, bem-sucedida ou não (escopo §16).
/// </summary>
public class LoginAuditLog
{
    public long Id { get; set; }

    /// <summary>Nulo quando o e-mail informado não corresponde a um usuário.</summary>
    public string? UserId { get; set; }
    public AppUser? User { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool Success { get; set; }

    /// <summary>Motivo da falha (ex.: senha inválida, usuário inativo, lockout).</summary>
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
