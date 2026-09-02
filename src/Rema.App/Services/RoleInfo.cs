using Rema.App.Data.Entities;

namespace Rema.App.Services;

/// <summary>Danske visningsnavne og rækkefølge for de tre roller.</summary>
public static class RoleInfo
{
    public static string Label(string? role) => role switch
    {
        RoleNames.Koebmand => "Købmand",
        RoleNames.Souschef => "Souschef",
        RoleNames.Medarbejder => "Medarbejder",
        _ => role ?? "–",
    };

    /// <summary>Lavere tal = mere adgang. Bruges til sortering og til at spærre nedgradering.</summary>
    public static int Rank(string? role) => role switch
    {
        RoleNames.Koebmand => 0,
        RoleNames.Souschef => 1,
        RoleNames.Medarbejder => 2,
        _ => 3,
    };

    /// <summary>Roller en given bruger må tildele andre.</summary>
    public static IEnumerable<string> AssignableBy(bool actorIsKoebmand) =>
        actorIsKoebmand
            ? [RoleNames.Medarbejder, RoleNames.Souschef, RoleNames.Koebmand]
            : [RoleNames.Medarbejder, RoleNames.Souschef];
}
