using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>
/// En gulvplan for én etage/afdeling i butikken. Indeholder de fysiske
/// placeringer (kasser, paller, gondolender) som tilbudsvarer kan lægges i.
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

    /// <summary>Lærredets bredde i planenheder (bokse positioneres inden for dette).</summary>
    public int CanvasWidth { get; set; } = 1000;

    /// <summary>Lærredets højde i planenheder.</summary>
    public int CanvasHeight { get; set; } = 700;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FloorBox> Boxes { get; set; } = new List<FloorBox>();
}
