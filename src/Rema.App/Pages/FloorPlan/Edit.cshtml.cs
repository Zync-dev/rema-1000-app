using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.FloorPlan;

public class EditModel(AppDbContext db) : PageModel
{
    public Data.Entities.FloorPlan Plan { get; private set; } = null!;

    /// <summary>Planen som JSON til editoren.</summary>
    public string PlanJson { get; private set; } = "{}";

    public record KindOption(string Value, string Label, int Width, int Height, bool Fixed);

    public IReadOnlyList<KindOption> Kinds { get; } =
        Enum.GetValues<BoxKind>()
            .Select(k =>
            {
                var (w, h) = BoxKindInfo.DefaultSize(k);
                return new KindOption(k.ToString(), Display(k), w, h, BoxKindInfo.IsFixedSize(k));
            })
            .ToList();

    public static readonly IReadOnlyList<(string Value, string Label)> SplitOptions =
        Enum.GetValues<SplitMode>().Select(s => (s.ToString(), Display(s))).ToList();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var plan = await LoadAsync(id);
        if (plan is null) return NotFound();

        Plan = plan;
        PlanJson = JsonSerializer.Serialize(ToDto(plan), JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id, [FromBody] PlanDto dto)
    {
        var plan = await LoadAsync(id);
        if (plan is null) return NotFound();

        if (dto.Boxes.Count > 400)
            return BadRequest(new { error = "For mange placeringer (maks 400)." });

        plan.CanvasWidth = Math.Clamp(dto.CanvasWidth, 200, 6000);
        plan.CanvasHeight = Math.Clamp(dto.CanvasHeight, 200, 6000);
        plan.UpdatedUtc = DateTimeOffset.UtcNow;

        var existingIds = dto.Boxes.Where(b => b.Id != Guid.Empty).Select(b => b.Id).ToHashSet();

        foreach (var removed in plan.Boxes.Where(b => !existingIds.Contains(b.Id)).ToList())
            plan.Boxes.Remove(removed);

        foreach (var b in dto.Boxes)
        {
            var box = b.Id != Guid.Empty
                ? plan.Boxes.FirstOrDefault(x => x.Id == b.Id)
                : null;

            if (box is null)
            {
                box = new FloorBox { FloorPlanId = plan.Id, StoreId = plan.StoreId };
                db.FloorBoxes.Add(box);
                plan.Boxes.Add(box);
            }

            var kind = Enum.TryParse<BoxKind>(b.Kind, out var k) ? k : BoxKind.FuldPalle;
            var split = Enum.TryParse<SplitMode>(b.Split, out var s) ? s : SplitMode.None;

            box.Label = Trim(b.Label, 40) ?? "";
            box.Offer = Trim(b.Offer, 240);
            box.OfferB = split == SplitMode.None ? null : Trim(b.OfferB, 240);
            box.Kind = kind;
            box.Split = split;
            box.Highlight = b.Highlight;
            box.X = Math.Clamp(b.X, 0, 6000);
            box.Y = Math.Clamp(b.Y, 0, 6000);

            // Faste typer har en fast fysisk størrelse; kun orienteringen (roteret
            // eller ej) kommer fra klienten.
            if (BoxKindInfo.IsFixedSize(kind))
            {
                var (dw, dh) = BoxKindInfo.DefaultSize(kind);
                var portrait = b.Height > b.Width;
                box.Width = portrait ? dh : dw;
                box.Height = portrait ? dw : dh;
            }
            else
            {
                box.Width = Math.Clamp(b.Width, 30, 6000);
                box.Height = Math.Clamp(b.Height, 30, 6000);
            }
        }

        await db.SaveChangesAsync();
        return new JsonResult(new { ok = true, savedUtc = plan.UpdatedUtc });
    }

    public async Task<IActionResult> OnPostClearOffersAsync(Guid id)
    {
        var plan = await LoadAsync(id);
        if (plan is null) return NotFound();

        foreach (var box in plan.Boxes)
        {
            box.Offer = null;
            box.OfferB = null;
            box.Highlight = false;
        }
        plan.UpdatedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Alle tilbud er ryddet – placeringerne er bevaret.";
        return RedirectToPage(new { id });
    }

    private async Task<Data.Entities.FloorPlan?> LoadAsync(Guid id) =>
        await db.FloorPlans.Include(p => p.Boxes).FirstOrDefaultAsync(p => p.Id == id);

    private static PlanDto ToDto(Data.Entities.FloorPlan p) => new()
    {
        CanvasWidth = p.CanvasWidth,
        CanvasHeight = p.CanvasHeight,
        Boxes = p.Boxes
            .OrderBy(b => b.Label)
            .Select(b => new BoxDto
            {
                Id = b.Id,
                Label = b.Label,
                Offer = b.Offer,
                OfferB = b.OfferB,
                Kind = b.Kind.ToString(),
                Split = b.Split.ToString(),
                Highlight = b.Highlight,
                X = b.X, Y = b.Y, Width = b.Width, Height = b.Height,
            })
            .ToList(),
    };

    private static string? Trim(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length <= max ? t : t[..max];
    }

    public static string Display(Enum value)
    {
        var member = value.GetType().GetMember(value.ToString());
        var attr = member.Length > 0
            ? member[0].GetCustomAttributes(typeof(DisplayAttribute), false)
            : [];
        return attr.Length > 0 ? ((DisplayAttribute)attr[0]).Name ?? value.ToString() : value.ToString();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public class PlanDto
    {
        public int CanvasWidth { get; set; } = 1400;
        public int CanvasHeight { get; set; } = 900;
        public List<BoxDto> Boxes { get; set; } = [];
    }

    public class BoxDto
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
        public string? Offer { get; set; }
        public string? OfferB { get; set; }
        public string Kind { get; set; } = "FuldPalle";
        public string Split { get; set; } = "None";
        public bool Highlight { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
