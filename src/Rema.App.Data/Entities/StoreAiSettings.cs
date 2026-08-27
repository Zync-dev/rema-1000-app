using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>Hvor mange emoji AI'en må bruge i opslag.</summary>
public enum EmojiUsage
{
    [Display(Name = "Ingen")] None = 0,
    [Display(Name = "Få")] Light = 1,
    [Display(Name = "Mange")] Rich = 2,
}

/// <summary>
/// Butikkens indstillinger for AI-Facebook-opslag: API-nøgle (krypteret) og
/// stilprofil, så alle opslag holder samme tone. Én række pr. butik.
/// </summary>
public class StoreAiSettings : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    /// <summary>Anthropic API-nøgle, krypteret med Data Protection. Aldrig vist i klartekst.</summary>
    public string? ApiKeyProtected { get; set; }

    /// <summary>Sidste 4 tegn af nøglen, til visning ("sk-…abcd").</summary>
    [MaxLength(8)]
    public string? ApiKeyHint { get; set; }

    /// <summary>Gemini-model. Standard: gemini-2.5-flash (gratis niveau).</summary>
    [MaxLength(60)]
    public string Model { get; set; } = "gemini-2.5-flash";

    // --- Stilprofil ---
    [MaxLength(400)]
    public string? Tone { get; set; }

    public EmojiUsage EmojiUsage { get; set; } = EmojiUsage.Light;

    [MaxLength(300)]
    public string? SignOff { get; set; }

    [MaxLength(300)]
    public string? Hashtags { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(600)]
    public string? OpeningHours { get; set; }

    [MaxLength(1500)]
    public string? ExtraGuidance { get; set; }

    /// <summary>Konkurrencebetingelser der indsættes i konkurrence-opslag.</summary>
    [MaxLength(2000)]
    public string? CompetitionRulesText { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FacebookStyleExample> Examples { get; set; } = new List<FacebookStyleExample>();

    public bool HasApiKey => !string.IsNullOrEmpty(ApiKeyProtected);
}

/// <summary>Et eksempel-opslag der viser AI'en butikkens stil (few-shot).</summary>
public class FacebookStyleExample : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    public Guid StoreAiSettingsId { get; set; }
    public StoreAiSettings? Settings { get; set; }

    [MaxLength(3000)]
    public string Text { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
