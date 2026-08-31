using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.FloorPlan;

public class IndexModel(AppDbContext db) : PageModel
{
    public record Row(Guid Id, string Name, string? Description, int BoxCount, int OfferCount, DateTimeOffset UpdatedUtc);

    public IReadOnlyList<Row> Plans { get; private set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Giv gulvplanen et navn.")]
        [StringLength(120)]
        [Display(Name = "Navn")]
        public string Name { get; set; } = string.Empty;

        [StringLength(400)]
        [Display(Name = "Beskrivelse")]
        public string? Description { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var plan = new Data.Entities.FloorPlan
        {
            Name = Input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
        };
        db.FloorPlans.Add(plan);
        await db.SaveChangesAsync();

        return RedirectToPage("Edit", new { id = plan.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var plan = await db.FloorPlans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is not null)
        {
            db.FloorPlans.Remove(plan);
            await db.SaveChangesAsync();
            TempData["StatusMessage"] = "Gulvplanen er slettet.";
        }
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Plans = await db.FloorPlans
            .OrderBy(p => p.Name)
            .Select(p => new Row(
                p.Id,
                p.Name,
                p.Description,
                p.Boxes.Count,
                p.Boxes.Count(b => (b.Offer != null && b.Offer != "") || (b.OfferB != null && b.OfferB != "")),
                p.UpdatedUtc))
            .ToListAsync();
    }
}
