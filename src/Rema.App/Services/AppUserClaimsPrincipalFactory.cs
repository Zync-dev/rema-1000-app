using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Services;

/// <summary>
/// Tilføjer butiks-claims til brugerens principal ved login, så resten af
/// applikationen (og tenant-filteret) kan læse butikken uden et databasekald.
/// </summary>
public class AppUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<IdentityOptions> options,
    AppDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(AppClaims.StoreId, user.StoreId.ToString()));

        var storeName = await db.Stores
            .Where(s => s.Id == user.StoreId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(storeName))
            identity.AddClaim(new Claim(AppClaims.StoreName, storeName));

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));

        return identity;
    }
}
