using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

public enum FacebookPostType
{
    [Display(Name = "Tilbud")] Tilbud = 0,
    [Display(Name = "Konkurrence")] Konkurrence = 1,
    [Display(Name = "Ny medarbejder")] NyMedarbejder = 2,
    [Display(Name = "Helligdags-/åbningstider")] Aabningstider = 3,
    [Display(Name = "Lokalt event")] Event = 4,
    [Display(Name = "Andet")] Andet = 5,
}

public enum FacebookPostStatus
{
    [Display(Name = "Kladde")] Kladde = 0,
    [Display(Name = "Klar")] Klar = 1,
    [Display(Name = "Brugt")] Brugt = 2,
}

/// <summary>Et AI-genereret (og evt. redigeret) Facebook-opslag.</summary>
public class FacebookPost : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    public FacebookPostType PostType { get; set; }
    public FacebookPostStatus Status { get; set; } = FacebookPostStatus.Kladde;

    /// <summary>Brugerens input – de fakta opslaget skal bygge på.</summary>
    [MaxLength(4000)]
    public string Brief { get; set; } = string.Empty;

    [MaxLength(8000)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Sand hvis teksten er ændret manuelt efter generering.</summary>
    public bool EditedByUser { get; set; }

    [MaxLength(60)]
    public string? Model { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedByUserId { get; set; }

    [MaxLength(120)]
    public string CreatedByName { get; set; } = string.Empty;
}
