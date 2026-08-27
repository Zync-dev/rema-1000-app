using Microsoft.AspNetCore.Identity;

namespace Rema.App.Data.Entities;

/// <summary>Rolle med Guid-nøgle, så den matcher <see cref="ApplicationUser"/>.</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }
}

/// <summary>Kanoniske rollenavne. Brug som <c>[Authorize(Roles = ...)]</c>-værdier.</summary>
public static class RoleNames
{
    /// <summary>Købmand – fuld adgang til butikkens data og brugere.</summary>
    public const string Koebmand = "Koebmand";

    /// <summary>Souschef – daglig drift, kan bruge alle værktøjer.</summary>
    public const string Souschef = "Souschef";

    /// <summary>Medarbejder – begrænset adgang.</summary>
    public const string Medarbejder = "Medarbejder";

    public static readonly IReadOnlyList<string> All = [Koebmand, Souschef, Medarbejder];

    /// <summary>Roller der må administrere butikkens brugere og indstillinger.</summary>
    public const string Managers = $"{Koebmand},{Souschef}";
}
