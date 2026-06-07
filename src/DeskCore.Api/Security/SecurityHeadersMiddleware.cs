namespace DeskCore.Api.Security;

public static class SecurityHeadersMiddleware
{
    /// <summary>Adiciona headers de segurança a todas as respostas da API.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            // API serve apenas JSON/arquivos via download autenticado — CSP restritiva.
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            await next();
        });
}
