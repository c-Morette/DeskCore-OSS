using System.Security.Claims;
using DeskCore.Application.Abstractions.Identity;

namespace DeskCore.Api.Identity;

/// <summary>
/// Implementação de <see cref="ICurrentUser"/> a partir do <c>HttpContext</c>.
/// Mantém a camada de aplicação desacoplada do ASP.NET Core.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private HttpContext? Context => accessor.HttpContext;
    private ClaimsPrincipal? Principal => Context?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = Context?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? null : ua;
        }
    }
}
