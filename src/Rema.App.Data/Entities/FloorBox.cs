using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>Type af placering på gulvet.</summary>
public enum BoxKind
{
    [Display(Name = "Palle")] Palle = 0,
    [Display(Name = "Halvpalle")] Halvpalle = 1,
    [Display(Name = "Gondolender")] Gondolender = 2,
    [Display(Name = "Bordplads")] Bordplads = 3,
    [Display(Name = "Stakke")] Stakke = 4,
    [Display(Name = "Køl/frost")] Koel = 5,
    [Display(Name = "Andet")] Andet = 6,
}

/// <summary>
/// Én placering i en <see cref="FloorPlan"/>. Har en fast etiket (fx "A1") og
/// et felt til ugens tilbudsvare, så planen kan genbruges uge efter uge.
/// </summary>
public class FloorBox : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid FloorPlanId { get; set; }
    public FloorPlan? FloorPlan { get; set; }

    /// <summary>Fast nummer/navn på placeringen, fx "A1" eller "Palle 3".</summary>
    [MaxLength(40)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Ugens vare på denne placering (fri tekst: varenavn, varenr., pris).</summary>
    [MaxLength(240)]
    public string? Offer { get; set; }

    public BoxKind Kind { get; set; } = BoxKind.Palle;

    /// <summary>Fremhæv boksen (fx annonce-/kampagnevare).</summary>
    public bool Highlight { get; set; }

    // Geometri i planenheder (samme koordinatsystem som FloorPlan.Canvas*).
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 120;
    public int Height { get; set; } = 90;
}
