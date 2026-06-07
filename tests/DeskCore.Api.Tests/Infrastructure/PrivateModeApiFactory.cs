using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeskCore.Api.Tests.Infrastructure;

/// <summary>
/// Factory para o modo privado/corporativo: sobe a API com o auto-serviço do titular
/// DESLIGADO (SelfService:Enabled=false). Sem logins de setup — os testes só checam que
/// o auto-cadastro e a recuperação de senha públicos são bloqueados antes de processar.
/// </summary>
public sealed class PrivateModeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "TestPass@123456";
    public const string AdminEmail = "admin@private.test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("deskcore_private_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), "deskcore-private-tests", Guid.NewGuid().ToString("N"));

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("SEED_ADMIN_EMAIL", AdminEmail);
        builder.UseSetting("SEED_ADMIN_PASSWORD", Password);
        builder.UseSetting("SEED_ADMIN_FULLNAME", "Admin Teste");
        builder.UseSetting("FileStorage:RootPath", _uploadsPath);
        // Modo privado: auto-serviço do titular desligado.
        builder.UseSetting("SelfService:Enabled", "false");
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
}

[CollectionDefinition(Name)]
public sealed class PrivateModeApiCollection : ICollectionFixture<PrivateModeApiFactory>
{
    public const string Name = "PrivateModeApi";
}
