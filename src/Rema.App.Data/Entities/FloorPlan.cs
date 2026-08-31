using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>
/// En gulvplan for én etage/afdeling i butikken. Indeholder de fysiske
/// placeringer (paller, skråborde, endebokse) som tilbudsvarer kan lægges i.
/// En butik kan have flere gulvplaner.
/// </summary>
public class FloorPlan : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Fri tekst, fx "Stueetage" eller "Kælder – kolonial".</summary>
    [MaxLength(400)]
    public string? Description { get; set; }

    /// <summary>Lærredets bredde i planenheder (≈ cm). Bokse positioneres inden for dette.</summary>
    public int CanvasWidth { get; set; } = 1400;

    /// <summary>Lærredets højde i planenheder (≈ cm).</summary>
    public int CanvasHeight { get; set; } = 900;

    /// <summary>Overskrift på udskriften. Tom = brug planens navn.</summary>
    [MaxLength(160)]
    public string? PrintHeadline { get; set; }

    /// <summary>Fri noter der printes under tegningen.</summary>
    [MaxLength(2000)]
    public string? PrintNotes { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FloorBox> Boxes { get; set; } = new List<FloorBox>();
}
