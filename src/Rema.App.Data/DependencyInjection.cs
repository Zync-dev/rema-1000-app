using Microsoft.EntityFrameworkCore;
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

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(normalized, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        return services;
    }
}
