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
public class MissedModel(
    AppDbContext db,
    ChecklistService checklists,
    ITenantProvider tenant,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public List<DayGroup> Days { get; private set; } = [];

    public sealed record Row(Guid Id, string Checklist, string Title, string? Assignee);
    public sealed record DayGroup(DateOnly Date, List<Row> Tasks);

    public async Task OnGetAsync()
    {
        await checklists.EnsureRecentDaysAsync();

        var names = await db.Users
            .Where(u => u.StoreId == tenant.StoreId)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var today = DanishTime.Today;
        var open = await db.ChecklistTasks
            .Include(t => t.Day)!.ThenInclude(d => d!.Checklist)
            .Where(t => !t.Done && t.Day!.Date < today)
            .ToListAsync();

        Days = open
            .GroupBy(t => t.Day!.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new DayGroup(g.Key, g
                .OrderBy(t => t.Day!.Checklist!.Title)
                .ThenBy(t => t.Position)
                .Select(t => new Row(
                    t.Id, t.Day!.Checklist!.Title, t.Title,
                    t.AssigneeUserId is Guid a && names.TryGetValue(a, out var n) ? n : null))
                .ToList()))
            .ToList();
    }

    public async Task<IActionResult> OnPostDoneAsync(Guid id)
    {
        var task = await db.ChecklistTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();
        task.Done = true;
        task.DoneByUserId = Guid.TryParse(userManager.GetUserId(User), out var uid) ? uid : null;
        task.DoneUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id)
    {
        var task = await db.ChecklistTasks
            .Include(t => t.Day)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null || task.Done) return RedirectToPage();

        var today = DanishTime.Today;
        var checklistId = task.Day!.ChecklistId;

        var todayDay = await db.ChecklistDays
            .Include(d => d.Tasks)
            .FirstOrDefaultAsync(d => d.ChecklistId == checklistId && d.Date == today);
        if (todayDay is null)
        {
            todayDay = new ChecklistDay { StoreId = tenant.StoreId, ChecklistId = checklistId, Date = today };
            db.ChecklistDays.Add(todayDay);
        }

        task.Day = todayDay;
        task.Position = todayDay.Tasks.Count == 0 ? 0 : todayDay.Tasks.Max(t => t.Position) + 1;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Opgaven er flyttet til i dag.";
        return RedirectToPage();
    }
}
