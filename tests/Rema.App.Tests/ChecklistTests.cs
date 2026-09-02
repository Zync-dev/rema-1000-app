using Microsoft.EntityFrameworkCore;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;
using Rema.App.Services;

namespace Rema.App.Tests;

public class ChecklistTests
{
    private static readonly DateTimeOffset LongAgo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Checklist NewChecklist(Guid store, ChecklistRecurrence rec, params string[] items)
    {
        var c = new Checklist { StoreId = store, Title = "Morgenrutine", Recurrence = rec, CreatedUtc = LongAgo };
        var pos = 0;
        foreach (var t in items)
            c.Items.Add(new ChecklistItem { StoreId = store, Title = t, Position = pos++ });
        return c;
    }

    [Theory]
    [InlineData(ChecklistRecurrence.Daily, "2026-09-05" /* lørdag */, true)]
    [InlineData(ChecklistRecurrence.Weekdays, "2026-09-05", false)]
    [InlineData(ChecklistRecurrence.Weekdays, "2026-09-04" /* fredag */, true)]
    public void AppliesOn_follows_recurrence(ChecklistRecurrence rec, string date, bool expected)
    {
        var c = new Checklist { Recurrence = rec };
        Assert.Equal(expected, c.AppliesOn(DateOnly.Parse(date)));
    }

    [Fact]
    public void AppliesOn_once_matches_only_its_date()
    {
        var c = new Checklist { Recurrence = ChecklistRecurrence.Once, Date = new DateOnly(2026, 9, 10) };
        Assert.True(c.AppliesOn(new DateOnly(2026, 9, 10)));
        Assert.False(c.AppliesOn(new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public async Task EnsureDays_materialises_tasks_from_template()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Checklists.Add(NewChecklist(store, ChecklistRecurrence.Daily, "Fej", "Tøm skrald"));
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.For(store, name))
        {
            var svc = new ChecklistService(db, new FixedTenantProvider(store));
            await svc.EnsureDaysAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));
        }

        await using (var db = TestDb.For(store, name))
        {
            var days = await db.ChecklistDays.Include(d => d.Tasks).ToListAsync();
            Assert.Equal(3, days.Count);
            Assert.All(days, d => Assert.Equal(2, d.Tasks.Count));
            var first = days.OrderBy(d => d.Date).First();
            Assert.Equal(new[] { "Fej", "Tøm skrald" }, first.Tasks.OrderBy(t => t.Position).Select(t => t.Title));
            Assert.All(first.Tasks, t => Assert.Equal(store, t.StoreId));
            Assert.All(first.Tasks, t => Assert.NotNull(t.SourceItemId));
        }
    }

    [Fact]
    public async Task EnsureDays_is_idempotent()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Checklists.Add(NewChecklist(store, ChecklistRecurrence.Daily, "Fej"));
            await db.SaveChangesAsync();
        }

        for (var i = 0; i < 3; i++)
        {
            await using var db = TestDb.For(store, name);
            var svc = new ChecklistService(db, new FixedTenantProvider(store));
            await svc.EnsureDaysAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));
        }

        await using (var db = TestDb.For(store, name))
        {
            Assert.Equal(2, await db.ChecklistDays.CountAsync());
            Assert.Equal(2, await db.ChecklistTasks.CountAsync());
        }
    }

    [Fact]
    public async Task EnsureDays_skips_weekends_for_weekdays_and_archived()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Checklists.Add(NewChecklist(store, ChecklistRecurrence.Weekdays, "Bank"));
            db.Checklists.Add(new Checklist { StoreId = store, Title = "Gammel", Recurrence = ChecklistRecurrence.Daily, IsArchived = true });
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.For(store, name))
        {
            var svc = new ChecklistService(db, new FixedTenantProvider(store));
            // 4. sep = fredag, 5.-6. weekend, 7. mandag
            await svc.EnsureDaysAsync(new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 7));
        }

        await using (var db = TestDb.For(store, name))
        {
            var dates = await db.ChecklistDays.Select(d => d.Date).OrderBy(d => d).ToListAsync();
            Assert.Equal(new[] { new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 7) }, dates);
        }
    }

    [Fact]
    public async Task EnsureDays_does_not_backfill_before_the_checklist_existed()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Checklists.Add(new Checklist
            {
                StoreId = store, Title = "Ny", Recurrence = ChecklistRecurrence.Daily,
                CreatedUtc = new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero),
                Items = { new ChecklistItem { StoreId = store, Title = "Gør noget" } },
            });
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.For(store, name))
        {
            var svc = new ChecklistService(db, new FixedTenantProvider(store));
            await svc.EnsureDaysAsync(new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 12));
        }

        await using (var db = TestDb.For(store, name))
        {
            var dates = await db.ChecklistDays.Select(d => d.Date).OrderBy(d => d).ToListAsync();
            Assert.Equal(new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 12) }, dates);
        }
    }

    [Fact]
    public async Task EnsureDays_once_list_only_on_its_date()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Checklists.Add(new Checklist
            {
                StoreId = store, Title = "Inventur", Recurrence = ChecklistRecurrence.Once,
                Date = new DateOnly(2026, 9, 15), CreatedUtc = LongAgo,
                Items = { new ChecklistItem { StoreId = store, Title = "Tæl kølere" } },
            });
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.For(store, name))
        {
            var svc = new ChecklistService(db, new FixedTenantProvider(store));
            await svc.EnsureDaysAsync(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 20));
        }

        await using (var db = TestDb.For(store, name))
        {
            var day = Assert.Single(await db.ChecklistDays.ToListAsync());
            Assert.Equal(new DateOnly(2026, 9, 15), day.Date);
        }
    }
}
