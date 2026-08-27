using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data;

/// <summary>
/// Bruges kun af <c>dotnet ef</c> til at oprette migrationer. Læser forbindelsen
/// fra miljøvariablen <c>ConnectionStrings__DefaultConnection</c> hvis den findes,
/// ellers en lokal standard – ingen database behøver at være tilgængelig for
/// <c>migrations add</c>.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var raw = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? "Host=localhost;Database=rema_app;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(NpgsqlConnectionString.Normalize(raw))
            .Options;

        return new AppDbContext(options, new FixedTenantProvider(Guid.Empty));
    }
}
