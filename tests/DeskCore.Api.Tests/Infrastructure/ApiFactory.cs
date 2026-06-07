using System.Net.Http.Json;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeskCore.Api.Tests.Infrastructure;

/// <summary>
/// Sobe a API real (pipeline completo: auth por cookie, autorização, rate limit,
/// security headers) sobre um PostgreSQL de container. O migrate + seed roda no
/// startup pelo <c>DatabaseInitializerHostedService</c>; usuários Agent/User extras
/// e o login dos clientes são preparados aqui.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "TestPass@123456";
    public const string AdminEmail = "admin@deskcore.test";
    public const string AgentEmail = "agent@deskcore.test";
    public const string UserEmail = "user@deskcore.test";
    public const string User2Email = "user2@deskcore.test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("deskcore_api_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), "deskcore-api-tests", Guid.NewGuid().ToString("N"));

    public HttpClient Anonymous { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;
    public HttpClient AgentClient { get; private set; } = null!;
    public HttpClient UserClient { get; private set; } = null!;
    public HttpClient User2Client { get; private set; } = null!;

    public string UserId { get; private set; } = null!;
    public long CategoryId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // No minimal hosting, ConfigureAppConfiguration não altera o builder.Configuration
        // já lido pelo Program (AddInfrastructure). UseSetting injeta direto na configuração
        // do host, que é encadeada em builder.Configuration.
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

        Anonymous = CreateClient(); // dispara o build do host → migrate + seed admin/roles/categorias

        // Usuários extras direto pelo Identity (sem depender do login do admin para setup).
        using (var scope = Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await CreateUserAsync(userManager, AgentEmail, "Atendente", Roles.Agent);
            UserId = (await CreateUserAsync(userManager, UserEmail, "Usuário", Roles.User)).Id;
            await CreateUserAsync(userManager, User2Email, "Outro Usuário", Roles.User);

            var db = scope.ServiceProvider.GetRequiredService<DeskCoreDbContext>();
            CategoryId = (await db.Categories.OrderBy(c => c.Id).FirstAsync()).Id;
        }

        AdminClient = await LoginAsync(AdminEmail);
        AgentClient = await LoginAsync(AgentEmail);
        UserClient = await LoginAsync(UserEmail);
        User2Client = await LoginAsync(User2Email);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
        try { Directory.Delete(_uploadsPath, recursive: true); } catch { /* best-effort */ }
    }

    /// <summary>Faz login e devolve um client cujo cookie de auth persiste entre requisições.</summary>
    public async Task<HttpClient> LoginAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> userManager, string email, string fullName, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao criar {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
