using DeskCore.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeskCore.Api.Tests.Infrastructure;

/// <summary>
/// Factory dedicada ao fluxo de auto-cadastro. Ao contrário da <see cref="ApiFactory"/>,
/// NÃO faz logins de setup — assim a janela do rate limiter "login" (5/min por IP) começa
/// vazia e cabe a sequência register → confirm → login sem colidir com o teste de burst.
/// </summary>
public sealed class RegisterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "TestPass@123456";
    public const string AdminEmail = "admin@register.test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("deskcore_register_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), "deskcore-register-tests", Guid.NewGuid().ToString("N"));

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("SEED_ADMIN_EMAIL", AdminEmail);
        builder.UseSetting("SEED_ADMIN_PASSWORD", Password);
        builder.UseSetting("SEED_ADMIN_FULLNAME", "Admin Teste");
        builder.UseSetting("FileStorage:RootPath", _uploadsPath);
        // Auto-serviço explicitamente ligado (é o default, mas deixa o fluxo robusto à config).
        builder.UseSetting("SelfService:Enabled", "true");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_uploadsPath);
        await _postgres.StartAsync();
        Client = CreateClient(); // dispara o build do host → migrate + seed
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
        try { Directory.Delete(_uploadsPath, recursive: true); } catch { /* best-effort */ }
    }

    /// <summary>
    /// Gera o token de confirmação de e-mail do usuário (como o controller faz) já
    /// codificado em Base64Url, pronto para o endpoint confirm-email.
    /// </summary>
    public async Task<string> GenerateConfirmationTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuário {email} não encontrado.");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public async Task<string> GetUserIdAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuário {email} não encontrado.");
        return user.Id;
    }
}

[CollectionDefinition(Name)]
public sealed class RegisterApiCollection : ICollectionFixture<RegisterApiFactory>
{
    public const string Name = "RegisterApi";
}
