using DeskCore.Application.Abstractions.Identity;
using DeskCore.Application.Abstractions.Persistence;
using DeskCore.Application.Abstractions.Storage;
using DeskCore.Application.Abstractions.Tickets;
using DeskCore.Application.DependencyInjection;
using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Infrastructure.Persistence;
using DeskCore.Infrastructure.Tickets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeskCore.Application.Tests.Infrastructure;

/// <summary>Identidade pré-semeada usada nos testes.</summary>
public sealed record TestUser(string Id, string Email, params string[] Roles);

/// <summary>
/// Sobe um PostgreSQL real via Testcontainers (exige Docker), aplica as migrations
/// da Infrastructure, semeia roles/usuários/categorias e monta o contêiner de DI
/// dos services da Application. Compartilhada por toda a coleção de testes.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string SeedPassword = "TestPass@123456";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("deskcore_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private ServiceProvider _provider = null!;

    public TestUser User { get; private set; } = null!;
    public TestUser User2 { get; private set; } = null!;
    public TestUser Agent { get; private set; } = null!;
    public TestUser Admin { get; private set; } = null!;

    /// <summary>Categoria ativa para criação de tickets.</summary>
    public long ActiveCategoryId { get; private set; }

    /// <summary>Categoria inativa (criação de ticket deve falhar).</summary>
    public long InactiveCategoryId { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<DeskCoreDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString(), npg =>
                npg.MigrationsAssembly(typeof(DeskCoreDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DeskCoreDbContext>());

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<DeskCoreDbContext>();

        // ICurrentUser scoped: cada escopo de teste configura sua própria identidade.
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());

        // Storage fake (em memória) e gerador real de número (sequence PG).
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();

        services.AddApplication();

        _provider = services.BuildServiceProvider();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeskCoreDbContext>();
            await db.Database.MigrateAsync();

            await SeedRolesAsync(scope.ServiceProvider);
            User = await SeedUserAsync(scope.ServiceProvider, "user@deskcore.test", "Usuário Comum", Roles.User);
            User2 = await SeedUserAsync(scope.ServiceProvider, "user2@deskcore.test", "Outro Usuário", Roles.User);
            Agent = await SeedUserAsync(scope.ServiceProvider, "agent@deskcore.test", "Atendente", Roles.Agent);
            Admin = await SeedUserAsync(scope.ServiceProvider, "admin@deskcore.test", "Administrador", Roles.Admin);

            var active = new TicketCategory { Name = "Suporte", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var inactive = new TicketCategory { Name = "Arquivada", IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Categories.AddRange(active, inactive);
            await db.SaveChangesAsync();
            ActiveCategoryId = active.Id;
            InactiveCategoryId = inactive.Id;
        }
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Cria um escopo de DI já autenticado como <paramref name="user"/>. Resolva os
    /// services a partir de <c>scope.ServiceProvider</c>.
    /// </summary>
    public AsyncServiceScope NewScope(TestUser user)
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>()
            .SignIn(user.Id, user.Email, user.Roles);
        return scope;
    }

    /// <summary>Escopo anônimo (sem usuário autenticado).</summary>
    public AsyncServiceScope NewAnonymousScope() => _provider.CreateAsyncScope();

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    private static async Task<TestUser> SeedUserAsync(IServiceProvider sp, string email, string fullName, string role)
    {
        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
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

        var created = await userManager.CreateAsync(user, SeedPassword);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao semear usuário {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);
        return new TestUser(user.Id, email, role);
    }
}

/// <summary>
/// Coleção única: serializa todos os testes que tocam o banco (um único container),
/// evitando concorrência indevida sobre DbContext e ruído entre casos.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
