using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.FloorPlan;

public class PrintModel(AppDbContext db) : PageModel
{
    public Data.Entities.FloorPlan Plan { get; private set; } = null!;
    public IReadOnlyList<FloorBox> Boxes { get; private set; } = [];
    public IReadOnlyList<FloorShape> Shapes { get; private set; } = [];

    public string Headline => string.IsNullOrWhiteSpace(Plan.PrintHeadline) ? Plan.Name : Plan.PrintHeadline!.Trim();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var plan = await Load(id);
        if (plan is null) return NotFound();

        Plan = plan;
        Boxes = plan.Boxes.OrderBy(b => b.Label, StringComparer.OrdinalIgnoreCase).ToList();
        Shapes = FloorShapes.Parse(plan.ShapesJson);
        return Page();
    }

    public async Task<IActionResult> OnPostSettingsAsync(Guid id, string? headline, string? notes)
    {
        var plan = await Load(id);
        if (plan is null) return NotFound();

        plan.PrintHeadline = Clean(headline, 160);
        plan.PrintNotes = Clean(notes, 2000);
        plan.UpdatedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    private Task<Data.Entities.FloorPlan?> Load(Guid id) =>
        db.FloorPlans.Include(p => p.Boxes).FirstOrDefaultAsync(p => p.Id == id);

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length <= max ? t : t[..max];
    }

    public static string KindLabel(BoxKind k) => EditModel.Display(k);

    public static string SplitLabelA(SplitMode s) => s switch
    {
        SplitMode.LeftRight => "venstre",
        SplitMode.TopBottom => "øverst",
        _ => "",
    };

    public static string SplitLabelB(SplitMode s) => s switch
    {
        SplitMode.LeftRight => "højre",
        SplitMode.TopBottom => "nederst",
        _ => "",
    };
}
