using System.ComponentModel.DataAnnotations;

namespace DeskCore.Shared.Contracts.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record CurrentUserResponse(
    string UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

/// <summary>Auto-cadastro público de cliente (role User). Sem escolha de papel.</summary>
public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Aceite da Política de Privacidade (LGPD) — obrigatório no auto-cadastro.</summary>
    public bool AcceptedPrivacyPolicy { get; set; }
}

/// <summary>Confirmação de e-mail via link enviado no cadastro (token Base64Url).</summary>
public sealed class ConfirmEmailRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}

/// <summary>Solicitação de redefinição de senha (envio do link por e-mail). Resposta sempre genérica.</summary>
public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>Redefinição de senha via link enviado por e-mail (token Base64Url, expira em 30 min).</summary>
public sealed class ResetPasswordRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string NewPassword { get; set; } = string.Empty;
}
