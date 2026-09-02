using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;
using Rema.App.Services;

namespace Rema.App.Pages.Tasks;

[Authorize]
public class IndexModel(
    AppDbContext db,
    ChecklistService checklists,
    ITenantProvider tenant,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public DateOnly Today { get; private set; }
    public bool MineOnly { get; private set; }
    public Guid MeId { get; private set; }
    public int MissedCount { get; private set; }
    public List<Group> Groups { get; private set; } = [];

    public sealed record Row(Guid Id, string Title, bool Done, string? Assignee, bool Mine, string? DoneBy);

    public sealed record Group(string Title, string? Notes, List<Row> Tasks)
    {
        public int DoneCount => Tasks.Count(t => t.Done);
        public bool AllDone => Tasks.Count > 0 && Tasks.All(t => t.Done);
    }

    public async Task OnGetAsync(bool mine = false)
    {
        MineOnly = mine;
        MeId = CurrentUserId();
        Today = DanishTime.Today;

        await checklists.EnsureRecentDaysAsync();

        var names = await db.Users
            .Where(u => u.StoreId == tenant.StoreId)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var days = await db.ChecklistDays
            .Include(d => d.Checklist)
            .Include(d => d.Tasks)
            .Where(d => d.Date == Today)
            .ToListAsync();

        MissedCount = await db.ChecklistTasks.CountAsync(t => !t.Done && t.Day!.Date < Today);

        Groups = days
            .OrderBy(d => d.Checklist!.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => new Group(
                d.Checklist!.Title,
                d.Checklist.Notes,
                d.Tasks
                    .Where(t => !mine || t.AssigneeUserId == MeId)
                    .OrderBy(t => t.Done)
                    .ThenBy(t => t.Position)
                    .Select(t => new Row(
                        t.Id, t.Title, t.Done,
                        Name(names, t.AssigneeUserId),
                        t.AssigneeUserId == MeId,
                        t.Done ? Name(names, t.DoneByUserId) : null))
                    .ToList()))
            .Where(g => g.Tasks.Count > 0)
            .ToList();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id, bool mine)
    {
        var task = await db.ChecklistTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();

        task.Done = !task.Done;
        task.DoneByUserId = task.Done ? CurrentUserId() : null;
        task.DoneUtc = task.Done ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync();

        if (WantsJson())
            return new JsonResult(new { done = task.Done });
        return RedirectToPage(new { mine });
    }

    private static string? Name(Dictionary<Guid, string> names, Guid? id) =>
        id is Guid g && names.TryGetValue(g, out var n) ? n : null;

    private Guid CurrentUserId() =>
        Guid.TryParse(userManager.GetUserId(User), out var id) ? id : Guid.Empty;

    private bool WantsJson() =>
        Request.Headers.XRequestedWith == "fetch" ||
        Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
}
