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

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var plan = await db.FloorPlans
            .Include(p => p.Boxes)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();

        Plan = plan;
        Boxes = plan.Boxes.OrderBy(b => b.Label).ToList();
        return Page();
    }

    public static string KindLabel(BoxKind k) => k switch
    {
        BoxKind.Palle => "Palle",
        BoxKind.Halvpalle => "Halvpalle",
        BoxKind.Gondolender => "Gondolender",
        BoxKind.Bordplads => "Bordplads",
        BoxKind.Stakke => "Stakke",
        BoxKind.Koel => "Køl/frost",
        _ => "Andet",
    };
}
