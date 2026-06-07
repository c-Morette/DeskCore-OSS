using System.Net;
using System.Threading.RateLimiting;
using DeskCore.Api.Authorization;
using DeskCore.Api.Identity;
using DeskCore.Api.Security;
using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.DependencyInjection;
using DeskCore.Domain.Constants;
using DeskCore.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Resposta de validação do model-binding localizada (PT/EN conforme Accept-Language).
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
        {
            Title = DeskCore.Shared.Localization.Msg.T("Há erros de validação na requisição.", "There are validation errors in the request."),
            Detail = DeskCore.Shared.Localization.Msg.T("Verifique os campos informados.", "Please check the submitted fields."),
            Status = StatusCodes.Status400BadRequest
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Camadas de aplicação e infraestrutura (DbContext, Identity, storage, seed no startup).
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Real IP atrás do Nginx (auditoria/login log). Em prod, restringir a rede do proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        if (IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);

    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        if (System.Net.IPNetwork.TryParse(network, out var ipNetwork))
            options.KnownIPNetworks.Add(ipNetwork);
});

// Cookie de autenticação como API: 401/403 em vez de redirect de login.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AgentOrAdmin, p => p.RequireRole(Roles.Agent, Roles.Admin));
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
});

// Rate limiting (escopo §17): login por IP; demais por usuário autenticado.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", ctx => FixedByKey(KeyByIp(ctx), permit: 5, windowSeconds: 60));
    options.AddPolicy("create-ticket", ctx => FixedByKey(KeyByUser(ctx), permit: 20, windowSeconds: 60));
    options.AddPolicy("upload", ctx => FixedByKey(KeyByUser(ctx), permit: 20, windowSeconds: 60));
    options.AddPolicy("download", ctx => FixedByKey(KeyByUser(ctx), permit: 60, windowSeconds: 60));
    options.AddPolicy("admin", ctx => FixedByKey(KeyByUser(ctx), permit: 60, windowSeconds: 60));

    static string KeyByIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    static string KeyByUser(HttpContext ctx) =>
        ctx.User.Identity?.IsAuthenticated == true
            ? ctx.User.Identity!.Name ?? KeyByIp(ctx)
            : KeyByIp(ctx);

    static RateLimitPartition<string> FixedByKey(string key, int permit, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
});

var app = builder.Build();

app.UseForwardedHeaders();

// Idioma das respostas (mensagens): lê o Accept-Language enviado pelo BFF (idioma da UI).
var supportedCultures = new[] { "pt-BR", "en" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>
/// Exposto como <c>public partial</c> para que os testes de integração
/// (<c>WebApplicationFactory&lt;Program&gt;</c>) referenciem o entrypoint da API.
/// </summary>
public partial class Program { }
