using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Services.Ai;

namespace Rema.App.Pages.FacebookPost;

[Authorize(Policy = "ErLeder")]
public class SettingsModel(
    AppDbContext db,
    ApiKeyProtector protector,
    IFacebookPostGenerator generator) : PageModel
{
    public const int MaxExamples = 3;

    public string? ApiKeyHint { get; private set; }
    public bool HasApiKey { get; private set; }

    /// <summary>
    /// Forslag til modeller på gratis-niveauet. Bare forslag – feltet er fritekst,
    /// så en nyere model kan skrives ind uden kodeændring. Se ai.google.dev/gemini-api/docs/models.
    /// </summary>
    public static readonly string[] ModelSuggestions =
        ["gemini-3.7-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite", "gemini-3.1-flash-lite"];

    private static readonly System.Text.RegularExpressions.Regex ModelPattern =
        new(@"^gemini-[a-z0-9.\-]{1,50}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public const string DefaultCompetitionRules =
        "Konkurrencen er ikke sponsoreret af, administreret af eller tilknyttet Facebook. "
        + "Vinderen kontaktes via Facebook. Deltageroplysninger bruges kun til at finde og kontakte vinderen "
        + "og slettes derefter. Man skal være fyldt 18 år for at deltage.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Display(Name = "Model")]
        public string Model { get; set; } = GeminiFacebookPostGenerator.DefaultModel;

        [Display(Name = "Ny API-nøgle")]
        [StringLength(200)]
        public string? NewApiKey { get; set; }

        public bool RemoveApiKey { get; set; }

        [Display(Name = "Tone")]
        [StringLength(400)]
        public string? Tone { get; set; }

        [Display(Name = "Emoji")]
        public EmojiUsage EmojiUsage { get; set; } = EmojiUsage.Light;

        [Display(Name = "Fast afslutning")]
        [StringLength(300)]
        public string? SignOff { get; set; }

        [Display(Name = "Hashtags")]
        [StringLength(300)]
        public string? Hashtags { get; set; }

        [Display(Name = "Adresse")]
        [StringLength(300)]
        public string? Address { get; set; }

        [Display(Name = "Åbningstider")]
        [StringLength(600)]
        public string? OpeningHours { get; set; }

        [Display(Name = "Ekstra retningslinjer")]
        [StringLength(1500)]
        public string? ExtraGuidance { get; set; }

        [Display(Name = "Konkurrencebetingelser")]
        [StringLength(2000)]
        public string? CompetitionRulesText { get; set; }

        public List<string> Examples { get; set; } = ["", "", ""];
    }

    public async Task OnGetAsync()
    {
        var s = await LoadAsync();
        if (s is null)
        {
            Input.CompetitionRulesText = DefaultCompetitionRules;
            return;
        }

        HasApiKey = s.HasApiKey;
        ApiKeyHint = s.ApiKeyHint;
        Input = new InputModel
        {
            Model = s.Model,
            Tone = s.Tone,
            EmojiUsage = s.EmojiUsage,
            SignOff = s.SignOff,
            Hashtags = s.Hashtags,
            Address = s.Address,
            OpeningHours = s.OpeningHours,
            ExtraGuidance = s.ExtraGuidance,
            CompetitionRulesText = s.CompetitionRulesText ?? DefaultCompetitionRules,
            Examples = PadExamples(s.Examples.OrderBy(e => e.SortOrder).Select(e => e.Text)),
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Model = (Input.Model ?? string.Empty).Trim();
        if (!ModelPattern.IsMatch(Input.Model))
            ModelState.AddModelError("Input.Model", "Modelnavnet skal starte med \"gemini-\", fx gemini-3.7-flash.");
        if (!ModelState.IsValid)
        {
            var cur = await LoadAsync();
            HasApiKey = cur?.HasApiKey ?? false;
            ApiKeyHint = cur?.ApiKeyHint;
            return Page();
        }

        var s = await LoadAsync();
        if (s is null)
        {
            s = new StoreAiSettings();
            db.StoreAiSettings.Add(s);
        }

        s.Model = Input.Model;
        s.Tone = Trim(Input.Tone);
        s.EmojiUsage = Input.EmojiUsage;
        s.SignOff = Trim(Input.SignOff);
        s.Hashtags = Trim(Input.Hashtags);
        s.Address = Trim(Input.Address);
        s.OpeningHours = Trim(Input.OpeningHours);
        s.ExtraGuidance = Trim(Input.ExtraGuidance);
        s.CompetitionRulesText = Trim(Input.CompetitionRulesText);
        s.UpdatedUtc = DateTimeOffset.UtcNow;

        if (Input.RemoveApiKey)
        {
            s.ApiKeyProtected = null;
            s.ApiKeyHint = null;
        }
        else if (!string.IsNullOrWhiteSpace(Input.NewApiKey))
        {
            var key = Input.NewApiKey.Trim();
            s.ApiKeyProtected = protector.Protect(key);
            s.ApiKeyHint = ApiKeyProtector.Hint(key);
        }

        // Eksempler: erstat alle.
        s.Examples.Clear();
        var order = 0;
        foreach (var text in Input.Examples.Where(x => !string.IsNullOrWhiteSpace(x)))
            s.Examples.Add(new FacebookStyleExample { StoreId = s.StoreId, Text = text.Trim(), SortOrder = order++ });

        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Indstillingerne er gemt.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestAsync(string? key)
    {
        var s = await LoadAsync();
        var apiKey = string.IsNullOrWhiteSpace(key)
            ? protector.TryUnprotect(s?.ApiKeyProtected)
            : key.Trim();
        var model = s?.Model ?? GeminiFacebookPostGenerator.DefaultModel;

        if (string.IsNullOrWhiteSpace(apiKey))
            return new JsonResult(new { ok = false, message = "Ingen nøgle at teste." });

        var ok = await generator.TestConnectionAsync(apiKey, model, HttpContext.RequestAborted);
        return new JsonResult(new
        {
            ok,
            message = ok ? "Nøglen virker." : "Nøglen blev afvist, eller modellen er ikke tilgængelig.",
        });
    }

    private async Task<StoreAiSettings?> LoadAsync() =>
        await db.StoreAiSettings.Include(x => x.Examples).FirstOrDefaultAsync();

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> PadExamples(IEnumerable<string> src)
    {
        var list = src.Take(MaxExamples).ToList();
        while (list.Count < MaxExamples) list.Add(string.Empty);
        return list;
    }
}
