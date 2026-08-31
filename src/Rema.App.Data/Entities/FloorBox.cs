using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>
/// Type af placering på gulvet. De faste typer har en fast fysisk størrelse
/// (ca. 1 planenhed = 1 cm); kun <see cref="Andet"/> kan skaleres frit.
/// </summary>
public enum BoxKind
{
    [Display(Name = "1/1 palle")] FuldPalle = 0,
    [Display(Name = "1/2 palle")] HalvPalle = 1,
    [Display(Name = "1/4 palle")] KvartPalle = 2,
    [Display(Name = "Skråbord")] Skraabord = 3,
    [Display(Name = "Endeboks")] Endeboks = 4,
    [Display(Name = "Andet")] Andet = 5,
}

/// <summary>Om en placering er delt op i to celler (fx pærer / æbler).</summary>
public enum SplitMode
{
    [Display(Name = "Ingen")] None = 0,
    [Display(Name = "Venstre / højre")] LeftRight = 1,
    [Display(Name = "Top / bund")] TopBottom = 2,
}

/// <summary>Fysiske standardstørrelser (planenheder ≈ cm) og regler pr. type.</summary>
public static class BoxKindInfo
{
    public static (int Width, int Height) DefaultSize(BoxKind kind) => kind switch
    {
        BoxKind.FuldPalle => (120, 80),
        BoxKind.HalvPalle => (80, 60),
        BoxKind.KvartPalle => (60, 40),
        BoxKind.Skraabord => (240, 80),   // ~2 × 1/1 palle
        BoxKind.Endeboks => (133, 90),
        _ => (130, 100),
    };

    /// <summary>Faste typer kan ikke skaleres frit – kun roteres.</summary>
    public static bool IsFixedSize(BoxKind kind) => kind != BoxKind.Andet;
}

/// <summary>
/// Én placering i en <see cref="FloorPlan"/>. Har en fast etiket (fx "A1") og
/// felt(er) til ugens tilbudsvare, så planen kan genbruges uge efter uge.
/// </summary>
public class FloorBox : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid FloorPlanId { get; set; }
    public FloorPlan? FloorPlan { get; set; }

    /// <summary>Fast nummer/navn på placeringen, fx "A1".</summary>
    [MaxLength(40)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Ugens vare (celle A, eller hele boksen når den ikke er delt).</summary>
    [MaxLength(240)]
    public string? Offer { get; set; }

    /// <summary>Ugens vare i celle B, når boksen er delt.</summary>
    [MaxLength(240)]
    public string? OfferB { get; set; }

    public BoxKind Kind { get; set; } = BoxKind.FuldPalle;

    public SplitMode Split { get; set; } = SplitMode.None;

    /// <summary>Fremhæv boksen (fx annonce-/kampagnevare).</summary>
    public bool Highlight { get; set; }

    // Geometri i planenheder (samme koordinatsystem som FloorPlan.Canvas*).
    // Rotation håndteres ved at bytte om på Width/Height, så footprinten altid
    // matcher virkeligheden.
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 120;
    public int Height { get; set; } = 80;
}
