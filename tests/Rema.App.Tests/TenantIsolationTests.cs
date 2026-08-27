using Microsoft.EntityFrameworkCore;
using Rema.App.Data.Entities;

namespace Rema.App.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Store_only_sees_its_own_calculations()
    {
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var db = TestDb.NewName();

        await using (var ctx = TestDb.For(storeA, db))
        {
            ctx.ProductCalculations.Add(new ProductCalculation { ProductName = "A-vare" });
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = TestDb.For(storeB, db))
        {
            ctx.ProductCalculations.Add(new ProductCalculation { ProductName = "B-vare" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = TestDb.For(storeA, db))
        {
            var names = await ctx.ProductCalculations.Select(c => c.ProductName).ToListAsync();
            Assert.Equal(["A-vare"], names);
        }
    }

    [Fact]
    public async Task SaveChanges_stamps_current_store_on_new_rows()
    {
        var storeA = Guid.NewGuid();
        var db = TestDb.NewName();

        await using var ctx = TestDb.For(storeA, db);
        var calc = new ProductCalculation { ProductName = "Uden StoreId" };
        ctx.ProductCalculations.Add(calc);
        await ctx.SaveChangesAsync();

        Assert.Equal(storeA, calc.StoreId);
    }

    [Fact]
    public async Task Cannot_read_another_stores_row_even_by_id()
    {
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var db = TestDb.NewName();
        Guid rowId;

        await using (var ctx = TestDb.For(storeA, db))
        {
            var calc = new ProductCalculation { ProductName = "Hemmelig" };
            ctx.ProductCalculations.Add(calc);
            await ctx.SaveChangesAsync();
            rowId = calc.Id;
        }

        await using (var ctx = TestDb.For(storeB, db))
        {
            var found = await ctx.ProductCalculations.FirstOrDefaultAsync(c => c.Id == rowId);
            Assert.Null(found);
        }
    }
}
