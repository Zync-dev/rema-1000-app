using System.ComponentModel.DataAnnotations;

namespace Rema.App.Data.Entities;

/// <summary>
/// En Rema 1000-butik. Rod-entiteten for al butiks-ejet data (multi-tenant).
/// </summary>
public class Store
{
    public Guid Id { get; set; }

    /// <summary>Butiksnummer, fx "0123". Unikt på tværs af systemet.</summary>
    [MaxLength(16)]
    public string StoreNumber { get; set; } = string.Empty;

    /// <summary>Butikkens visningsnavn, fx "Rema 1000 Nørrebrogade".</summary>
    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string City { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
