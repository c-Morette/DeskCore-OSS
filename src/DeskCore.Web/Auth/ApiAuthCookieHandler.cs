using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace DeskCore.Web.Auth;

/// <summary>
/// Reenvia o cookie de autenticação da API (guardado como claim do usuário Web)
/// em cada chamada HTTP. Usa o <c>HttpContext</c> quando disponível (SSR/endpoints)
/// e o <see cref="AuthenticationStateProvider"/> dentro do circuito Blazor.
/// </summary>
public sealed class ApiAuthCookieHandler(
    AuthenticationStateProvider authState,
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            user = (await authState.GetAuthenticationStateAsync()).User;

        var cookie = user?.FindFirst(DeskCoreClaims.ApiAuthCookie)?.Value;
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

        // Propaga o idioma da UI para a API localizar as mensagens.
        request.Headers.AcceptLanguage.ParseAdd(CultureInfo.CurrentUICulture.Name);

        return await base.SendAsync(request, cancellationToken);
    }
}
