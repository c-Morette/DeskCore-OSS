using DeskCore.Application.Abstractions.Identity;

namespace DeskCore.Application.Tests.Infrastructure;

/// <summary>
/// <see cref="ICurrentUser"/> mutável usado nos testes. Registrado como scoped:
/// cada escopo (cada caso de teste) tem o seu, então não há estado compartilhado
/// entre testes.
/// </summary>
public sealed class TestCurrentUser : ICurrentUser
{
    private HashSet<string> _roles = new(StringComparer.Ordinal);

    public string? UserId { get; private set; }
    public string? Email { get; private set; }
    public bool IsAuthenticated => UserId is not null;
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "xunit";

    public bool IsInRole(string role) => _roles.Contains(role);

    public void SignIn(string userId, string email, params string[] roles)
    {
        UserId = userId;
        Email = email;
        _roles = new HashSet<string>(roles, StringComparer.Ordinal);
    }
}
