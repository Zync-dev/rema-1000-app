using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;

namespace Rema.App.Pages.Tasks;

[Authorize(Policy = "ErLeder")]
public class ListsModel(
    AppDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public List<Row> Checklists { get; private set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed record Row(Guid Id, string Title, ChecklistRecurrence Recurrence, DateOnly? Date, int ItemCount, bool IsArchived);

    public class InputModel
    {
        [Required(ErrorMessage = "Giv listen en titel.")]
        [StringLength(120, MinimumLength = 2)]
        [Display(Name = "Titel")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Gentages")]
        public ChecklistRecurrence Recurrence { get; set; } = ChecklistRecurrence.Daily;

        [Display(Name = "Dato")]
        public DateOnly? Date { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Recurrence == ChecklistRecurrence.Once && Input.Date is null)
            ModelState.AddModelError("Input.Date", "Vælg en dato for engangslisten.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.Checklists.Add(new Checklist
        {
            Title = Input.Title.Trim(),
            Recurrence = Input.Recurrence,
            Date = Input.Recurrence == ChecklistRecurrence.Once ? Input.Date : null,
            CreatedByUserId = Guid.TryParse(userManager.GetUserId(User), out var uid) ? uid : Guid.Empty,
        });
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Tjeklisten er oprettet. Tilføj opgaverne herunder.";
        var created = await db.Checklists.OrderByDescending(c => c.CreatedUtc).FirstAsync();
        return RedirectToPage("List", new { id = created.Id });
    }

    private async Task LoadAsync()
    {
        Checklists = await db.Checklists
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.Title)
            .Select(c => new Row(
                c.Id, c.Title, c.Recurrence, c.Date, c.Items.Count, c.IsArchived))
            .ToListAsync();
    }
}
