namespace DeskCore.Application.Abstractions.Identity;

/// <summary>
/// Identidade e metadados da requisição do usuário autenticado. Implementada
/// na API (Fase 4) a partir do <c>HttpContext</c>. Mantém a Application
/// desacoplada do ASP.NET Core.
/// </summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);

    string? IpAddress { get; }
    string? UserAgent { get; }
}
