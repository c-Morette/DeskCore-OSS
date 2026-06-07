using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeskCore.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada apenas em design-time (ex.: <c>dotnet ef migrations</c>).
/// A connection string aqui é placeholder — <c>migrations add</c> não conecta
/// ao banco, apenas constrói o modelo.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DeskCoreDbContext>
{
    public DeskCoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DeskCoreDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=deskcore;Username=postgres;Password=postgres",
                npg => npg.MigrationsAssembly(typeof(DeskCoreDbContext).Assembly.FullName))
            .Options;

        return new DeskCoreDbContext(options);
    }
}
