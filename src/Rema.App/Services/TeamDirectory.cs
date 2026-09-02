using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;

namespace Rema.App.Services;

/// <summary>Én medarbejder set udefra – uden adgangskode.</summary>
public sealed record TeamMember(
    Guid Id, string DisplayName, string Email, string? Phone, string Role, bool IsActive);

/// <summary>
/// Læse-adgang til butikkens brugere. Alt er scopet til den aktuelle butik –
/// <see cref="ApplicationUser"/> har intet globalt tenant-filter, så filtreringen
/// sker eksplicit her.
/// </summary>
public sealed class TeamDirectory(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    ITenantProvider tenant)
{
    public Guid StoreId => tenant.StoreId;

    public async Task<IReadOnlyList<TeamMember>> ListAsync(CancellationToken ct = default)
    {
        var users = await db.Users
            .Where(u => u.StoreId == StoreId)
            .ToListAsync(ct);

        var members = new List<TeamMember>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            members.Add(new TeamMember(
                u.Id, u.DisplayName, u.Email ?? "", u.PhoneNumber,
                roles.OrderBy(RoleInfo.Rank).FirstOrDefault() ?? "", u.IsActive));
        }

        return members
            .OrderByDescending(m => m.IsActive)
            .ThenBy(m => RoleInfo.Rank(m.Role))
            .ThenBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Brugere i butikken som en opgave kan uddeles til (aktive konti).</summary>
    public async Task<IReadOnlyList<TeamMember>> AssignableAsync(CancellationToken ct = default) =>
        (await ListAsync(ct)).Where(m => m.IsActive).ToList();

    public Task<ApplicationUser?> FindInStoreAsync(Guid id) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id && u.StoreId == StoreId);

    /// <summary>Antal aktive købmænd i butikken – må aldrig ramme 0.</summary>
    public async Task<int> ActiveKoebmandCountAsync()
    {
        var all = await userManager.GetUsersInRoleAsync(RoleNames.Koebmand);
        return all.Count(u => u.StoreId == StoreId && u.IsActive);
    }
}
