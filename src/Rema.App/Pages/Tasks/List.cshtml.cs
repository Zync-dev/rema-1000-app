using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Services;

namespace Rema.App.Pages.Tasks;

[Authorize(Policy = "ErLeder")]
public class ListModel(AppDbContext db, TeamDirectory team) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Checklist Checklist { get; private set; } = null!;
    public IReadOnlyList<TeamMember> Assignees { get; private set; } = [];

    [BindProperty] public DetailsInput Details { get; set; } = new();
    [BindProperty] public ItemInput NewItem { get; set; } = new();

    public class DetailsInput
    {
        [Required(ErrorMessage = "Titlen må ikke være tom.")]
        [StringLength(120, MinimumLength = 2)]
        [Display(Name = "Titel")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Note (vises over opgaverne)")]
        public string? Notes { get; set; }

        [Display(Name = "Gentages")]
        public ChecklistRecurrence Recurrence { get; set; }

        [Display(Name = "Dato")]
        public DateOnly? Date { get; set; }
    }

    public class ItemInput
    {
        [Required(ErrorMessage = "Skriv hvad der skal gøres.")]
        [StringLength(200, MinimumLength = 2)]
        [Display(Name = "Opgave")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Ansvarlig")]
        public Guid? AssigneeUserId { get; set; }
    }

    private async Task<Checklist?> LoadAsync()
    {
        Assignees = await team.AssignableAsync();
        return await db.Checklists
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == Id);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var c = await LoadAsync();
        if (c is null) return NotFound();
        Checklist = c;
        Details = new DetailsInput { Title = c.Title, Notes = c.Notes, Recurrence = c.Recurrence, Date = c.Date };
        return Page();
    }

    public async Task<IActionResult> OnPostDetailsAsync()
    {
        var c = await LoadAsync();
        if (c is null) return NotFound();
        Checklist = c;

        // Kun Details-formularen er sendt – valider udelukkende den.
        ModelState.Clear();
        TryValidateModel(Details, nameof(Details));
        if (Details.Recurrence == ChecklistRecurrence.Once && Details.Date is null)
            ModelState.AddModelError("Details.Date", "Vælg en dato.");
        if (!ModelState.IsValid) return Page();

        c.Title = Details.Title.Trim();
        c.Notes = string.IsNullOrWhiteSpace(Details.Notes) ? null : Details.Notes.Trim();
        c.Recurrence = Details.Recurrence;
        c.Date = Details.Recurrence == ChecklistRecurrence.Once ? Details.Date : null;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Gemt.";
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostAddItemAsync()
    {
        var c = await LoadAsync();
        if (c is null) return NotFound();
        Checklist = c;
        Details = new DetailsInput { Title = c.Title, Notes = c.Notes, Recurrence = c.Recurrence, Date = c.Date };

        ModelState.Clear();
        TryValidateModel(NewItem, nameof(NewItem));
        if (!ModelState.IsValid) return Page();

        var nextPos = c.Items.Count == 0 ? 0 : c.Items.Max(i => i.Position) + 1;
        c.Items.Add(new ChecklistItem
        {
            ChecklistId = c.Id,
            Title = NewItem.Title.Trim(),
            AssigneeUserId = NewItem.AssigneeUserId,
            Position = nextPos,
        });
        await db.SaveChangesAsync();
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostUpdateItemAsync(Guid itemId, string title, Guid? assignee)
    {
        var item = await db.ChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ChecklistId == Id);
        if (item is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(title)) item.Title = title.Trim();
        item.AssigneeUserId = assignee;
        await db.SaveChangesAsync();
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDeleteItemAsync(Guid itemId)
    {
        var item = await db.ChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ChecklistId == Id);
        if (item is not null)
        {
            db.ChecklistItems.Remove(item);
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostMoveItemAsync(Guid itemId, int dir)
    {
        var items = await db.ChecklistItems.Where(i => i.ChecklistId == Id)
            .OrderBy(i => i.Position).ThenBy(i => i.Title).ToListAsync();
        var idx = items.FindIndex(i => i.Id == itemId);
        var swap = idx + Math.Sign(dir);
        if (idx >= 0 && swap >= 0 && swap < items.Count)
        {
            (items[idx].Position, items[swap].Position) = (items[swap].Position, items[idx].Position);
            await db.SaveChangesAsync();
        }
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(bool archive)
    {
        var c = await db.Checklists.FirstOrDefaultAsync(c => c.Id == Id);
        if (c is null) return NotFound();
        c.IsArchived = archive;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = archive
            ? "Tjeklisten er arkiveret og laver ikke flere opgaver."
            : "Tjeklisten er aktiv igen.";
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var c = await db.Checklists.FirstOrDefaultAsync(c => c.Id == Id);
        if (c is not null)
        {
            db.Checklists.Remove(c);
            await db.SaveChangesAsync();
        }
        TempData["StatusMessage"] = "Tjeklisten og dens historik er slettet.";
        return RedirectToPage("Lists");
    }
}
