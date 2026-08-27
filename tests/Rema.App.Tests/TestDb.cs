using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Rema.App.Data;
using Rema.App.Data.Tenancy;

namespace Rema.App.Tests;

/// <summary>
/// Hjælper til InMemory-databaser i tests. En delt <see cref="InMemoryDatabaseRoot"/>
/// sikrer at flere <see cref="AppDbContext"/>-instanser med samme navn deler data,
/// også når de bygger hver sin interne service-provider.
/// </summary>
public static class TestDb
{
    private static readonly InMemoryDatabaseRoot Root = new();

    public static AppDbContext For(Guid storeId, string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(name, Root)
                .EnableSensitiveDataLogging()
                .Options,
            new FixedTenantProvider(storeId));

    /// <summary>Unikt db-navn pr. kald, så tests ikke deler tilstand.</summary>
    public static string NewName(string prefix = "db") => $"{prefix}-{Guid.NewGuid():N}";
}
