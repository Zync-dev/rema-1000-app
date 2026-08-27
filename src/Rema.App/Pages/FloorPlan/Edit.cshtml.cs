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

    public IReadOnlyList<(string Value, string Label)> Kinds { get; } =
        Enum.GetValues<BoxKind>()
            .Select(k => (k.ToString(), KindLabel(k)))
            .ToList();

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

        plan.CanvasWidth = Math.Clamp(dto.CanvasWidth, 200, 4000);
        plan.CanvasHeight = Math.Clamp(dto.CanvasHeight, 200, 4000);
        plan.UpdatedUtc = DateTimeOffset.UtcNow;

        var existingIds = dto.Boxes.Where(b => b.Id != Guid.Empty).Select(b => b.Id).ToHashSet();

        // Fjern bokse der ikke længere findes (cascade sletter dem).
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

            box.Label = Trim(b.Label, 40) ?? "";
            box.Offer = Trim(b.Offer, 240);
            box.Kind = Enum.TryParse<BoxKind>(b.Kind, out var k) ? k : BoxKind.Palle;
            box.Highlight = b.Highlight;
            box.X = Math.Clamp(b.X, 0, 4000);
            box.Y = Math.Clamp(b.Y, 0, 4000);
            box.Width = Math.Clamp(b.Width, 24, 4000);
            box.Height = Math.Clamp(b.Height, 24, 4000);
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
                Kind = b.Kind.ToString(),
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

    private static string KindLabel(BoxKind k) => k switch
    {
        BoxKind.Palle => "Palle",
        BoxKind.Halvpalle => "Halvpalle",
        BoxKind.Gondolender => "Gondolender",
        BoxKind.Bordplads => "Bordplads",
        BoxKind.Stakke => "Stakke",
        BoxKind.Koel => "Køl/frost",
        _ => "Andet",
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public class PlanDto
    {
        public int CanvasWidth { get; set; } = 1000;
        public int CanvasHeight { get; set; } = 700;
        public List<BoxDto> Boxes { get; set; } = [];
    }

    public class BoxDto
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
        public string? Offer { get; set; }
        public string Kind { get; set; } = "Palle";
        public bool Highlight { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
