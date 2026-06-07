using DeskCore.Domain.Constants;
using DeskCore.Domain.Entities;
using DeskCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeskCore.Infrastructure.Seeding;

/// <summary>
/// No startup (single-tenant): aplica migrations e faz o seed de roles, do
/// primeiro Admin (via variáveis de ambiente) e das categorias iniciais.
/// </summary>
public sealed class DatabaseInitializerHostedService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DatabaseInitializerHostedService> logger) : IHostedService
{
    private static readonly string[] InitialCategories =
        ["Hardware", "Software", "Rede", "Acesso", "Impressora", "Sistema Interno", "Outros"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<DeskCoreDbContext>();
        logger.LogInformation("Aplicando migrations do banco...");
        await db.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(sp);
        await SeedAdminAsync(sp);
        await SeedCategoriesAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private async Task SeedAdminAsync(IServiceProvider sp)
    {
        var email = configuration["SEED_ADMIN_EMAIL"];
        var password = configuration["SEED_ADMIN_PASSWORD"];
        var fullName = configuration["SEED_ADMIN_FULLNAME"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SEED_ADMIN_EMAIL/SEED_ADMIN_PASSWORD não definidos — admin inicial não será criado.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();

        // Só cria se ainda não existir nenhum Admin (escopo §8).
        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
            return;

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var now = DateTime.UtcNow;
        var admin = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
            logger.LogInformation("Admin inicial criado: {Email}", email);
        }
        else
        {
            logger.LogError("Falha ao criar admin inicial: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedCategoriesAsync(DeskCoreDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        db.Categories.AddRange(InitialCategories.Select(name => new TicketCategory
        {
            Name = name,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        }));

        await db.SaveChangesAsync(ct);
    }
}
