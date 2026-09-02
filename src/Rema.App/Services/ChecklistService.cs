using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;

namespace Rema.App.Services;

/// <summary>
/// Materialiserer tjekliste-skabeloner til konkrete dage. En <see cref="ChecklistDay"/>
/// med opgaver oprettes doven: første gang nogen åbner opgavesiden en dag, og for
/// de sidste par dage bagud så "ikke nået i går" altid har data.
/// </summary>
public sealed class ChecklistService(AppDbContext db, ITenantProvider tenant)
{
    /// <summary>Hvor mange dage bagud der efterfyldes når opgavesiden åbnes.</summary>
    public const int BackfillDays = 8;

    public async Task EnsureRecentDaysAsync(CancellationToken ct = default)
    {
        var today = DanishTime.Today;
        await EnsureDaysAsync(today.AddDays(-BackfillDays), today, ct);
    }

    public async Task EnsureDaysAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var storeId = tenant.StoreId;
        if (storeId == Guid.Empty || from > to) return;

        var checklists = await db.Checklists
            .Include(c => c.Items)
            .Where(c => !c.IsArchived)
            .ToListAsync(ct);
        if (checklists.Count == 0) return;

        var ids = checklists.Select(c => c.Id).ToList();
        var existing = await db.ChecklistDays
            .Where(d => ids.Contains(d.ChecklistId) && d.Date >= from && d.Date <= to)
            .Select(d => new { d.ChecklistId, d.Date })
            .ToListAsync(ct);
        var have = existing.Select(x => (x.ChecklistId, x.Date)).ToHashSet();

        // En tjekliste laver ikke opgaver for dage før den blev oprettet.
        var born = checklists.ToDictionary(
            c => c.Id,
            c => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.CreatedUtc, DanishTime.Zone).Date));

        var added = false;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var c in checklists)
            {
                if (!c.AppliesOn(date) || have.Contains((c.Id, date)) || date < born[c.Id]) continue;

                var day = new ChecklistDay { StoreId = storeId, ChecklistId = c.Id, Date = date };
                var pos = 0;
                foreach (var item in c.Items.OrderBy(i => i.Position).ThenBy(i => i.Title))
                {
                    day.Tasks.Add(new ChecklistTask
                    {
                        StoreId = storeId,
                        Title = item.Title,
                        AssigneeUserId = item.AssigneeUserId,
                        Position = pos++,
                        SourceItemId = item.Id,
                    });
                }
                db.ChecklistDays.Add(day);
                added = true;
            }
        }

        if (!added) return;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // En samtidig forespørgsel nåede at oprette dagen først – den findes nu, og det er nok.
            foreach (var e in db.ChangeTracker.Entries<ChecklistDay>().Where(e => e.State == EntityState.Added).ToList())
                e.State = EntityState.Detached;
        }
    }
}
