using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Services;

/// <summary>
/// Kører ved opstart: anvender ventende migrationer og sikrer at rollerne findes.
/// </summary>
public static class DbInitializer
{
    public static async Task RunAsync(IServiceProvider services, bool migrate)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        if (migrate)
            await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }
}
