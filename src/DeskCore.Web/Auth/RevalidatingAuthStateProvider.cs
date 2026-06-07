using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace DeskCore.Web.Auth;

/// <summary>
/// Fornece o estado de autenticação para o circuito Blazor Server a partir do
/// cookie do Web. A expiração do cookie cuida da revalidação na V1.
/// </summary>
public sealed class RevalidatingAuthStateProvider(ILoggerFactory loggerFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
