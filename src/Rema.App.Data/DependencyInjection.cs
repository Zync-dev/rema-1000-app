using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Rema.App.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registrerer <see cref="AppDbContext"/> mod PostgreSQL. Forbindelsen må gerne
    /// være enten Npgsql-nøgleformat eller en <c>postgres://</c>-URL.
    /// Den kaldende app skal selv registrere en <c>ITenantProvider</c>.
    /// </summary>
    public static IServiceCollection AddRemaData(this IServiceCollection services, string connectionString)
    {
        var normalized = NpgsqlConnectionString.Normalize(connectionString);

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(normalized, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            // FloorPlan og FloorBox har med vilje ens tenant-filtre; advarslen om
            // krævet navigation + query-filter er derfor ikke relevant her.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        return services;
    }
}
