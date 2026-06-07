using DeskCore.Domain.Constants;
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
/// Factory dedicada ao fluxo de "esqueci minha senha". Como a <see cref="RegisterApiFactory"/>,
/// NÃO faz logins de setup, deixando a janela do rate limiter "login" (5/min por IP) vazia para
/// caber a sequência forgot → reset → login. O usuário de teste é criado direto pelo UserManager
/// (não conta no rate limiter), já ativo e confirmado.
/// </summary>
public sealed class PasswordResetApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "TestPass@123456";
    public const string AdminEmail = "admin@reset.test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("deskcore_reset_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), "deskcore-reset-tests", Guid.NewGuid().ToString("N"));

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("SEED_ADMIN_EMAIL", AdminEmail);
        builder.UseSetting("SEED_ADMIN_PASSWORD", Password);
        builder.UseSetting("SEED_ADMIN_FULLNAME", "Admin Teste");
        builder.UseSetting("FileStorage:RootPath", _uploadsPath);
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

    /// <summary>Cria um usuário comum já ativo e com e-mail confirmado (apto a logar).</summary>
    public async Task CreateActiveUserAsync(string email, string password, string fullName = "Usuário Teste")
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException("Falha ao criar usuário de teste: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, Roles.User);
    }

    /// <summary>Gera o token de reset (como o controller faz) já em Base64Url.</summary>
    public async Task<string> GenerateResetTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuário {email} não encontrado.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
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

    /// <summary>Confere a senha em processo (não passa pelo rate limiter de login).</summary>
    public async Task<bool> CheckPasswordAsync(string email, string password)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Usuário {email} não encontrado.");
        return await userManager.CheckPasswordAsync(user, password);
    }
}

[CollectionDefinition(Name)]
public sealed class PasswordResetApiCollection : ICollectionFixture<PasswordResetApiFactory>
{
    public const string Name = "PasswordResetApi";
}
