using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data.Entities;
using Rema.App.Pages.FloorPlan;

namespace Rema.App.Tests;

public class FloorPlanTests
{
    private static Rema.App.Data.AppDbContext Ctx(Guid storeId, string dbName) => TestDb.For(storeId, dbName);

    private static async Task<Guid> SeedPlanAsync(Guid storeId, string dbName, string name = "Stueetage")
    {
        await using var db = Ctx(storeId, dbName);
        var plan = new FloorPlan { Name = name };
        db.FloorPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    [Fact]
    public async Task Save_adds_updates_and_removes_boxes()
    {
        var store = Guid.NewGuid();
        var dbName = nameof(Save_adds_updates_and_removes_boxes);
        var planId = await SeedPlanAsync(store, dbName);

        // Første gem: to bokse.
        await using (var db = Ctx(store, dbName))
        {
            var model = new EditModel(db);
            var result = await model.OnPostSaveAsync(planId, new EditModel.PlanDto
            {
                CanvasWidth = 1200,
                CanvasHeight = 800,
                Boxes =
                [
                    new() { Label = "A1", Kind = "Palle", X = 10, Y = 10, Width = 100, Height = 80, Offer = "Cola 5 kr" },
                    new() { Label = "A2", Kind = "Gondolender", X = 200, Y = 10, Width = 200, Height = 60 },
                ],
            });
            Assert.IsType<JsonResult>(result);
        }

        Guid keepId;
        await using (var db = Ctx(store, dbName))
        {
            var boxes = await db.FloorBoxes.OrderBy(b => b.Label).ToListAsync();
            Assert.Equal(2, boxes.Count);
            Assert.Equal(BoxKind.Gondolender, boxes[1].Kind);
            Assert.All(boxes, b => Assert.Equal(store, b.StoreId));
            keepId = boxes[0].Id;

            var plan = await db.FloorPlans.FindAsync(planId);
            Assert.Equal(1200, plan!.CanvasWidth);
        }

        // Andet gem: behold A1 (omdøbt), fjern A2, tilføj A3.
        await using (var db = Ctx(store, dbName))
        {
            var model = new EditModel(db);
            await model.OnPostSaveAsync(planId, new EditModel.PlanDto
            {
                CanvasWidth = 1200,
                CanvasHeight = 800,
                Boxes =
                [
                    new() { Id = keepId, Label = "A1-ny", Kind = "Palle", X = 10, Y = 10, Width = 100, Height = 80 },
                    new() { Label = "A3", Kind = "Stakke", X = 400, Y = 400, Width = 90, Height = 90 },
                ],
            });
        }

        await using (var db = Ctx(store, dbName))
        {
            var labels = await db.FloorBoxes.OrderBy(b => b.Label).Select(b => b.Label).ToListAsync();
            Assert.Equal(["A1-ny", "A3"], labels);
        }
    }

    [Fact]
    public async Task Save_clamps_out_of_range_geometry()
    {
        var store = Guid.NewGuid();
        var dbName = nameof(Save_clamps_out_of_range_geometry);
        var planId = await SeedPlanAsync(store, dbName);

        await using (var db = Ctx(store, dbName))
        {
            await new EditModel(db).OnPostSaveAsync(planId, new EditModel.PlanDto
            {
                CanvasWidth = 999999,
                CanvasHeight = -5,
                Boxes = [new() { Label = "X", Kind = "Palle", X = -100, Y = -100, Width = 5, Height = 999999 }],
            });
        }

        await using (var db = Ctx(store, dbName))
        {
            var plan = await db.FloorPlans.Include(p => p.Boxes).FirstAsync(p => p.Id == planId);
            Assert.Equal(4000, plan.CanvasWidth);
            Assert.Equal(200, plan.CanvasHeight);
            var box = plan.Boxes.Single();
            Assert.Equal(0, box.X);
            Assert.Equal(24, box.Width);
            Assert.Equal(4000, box.Height);
        }
    }

    [Fact]
    public async Task Save_rejects_plan_from_another_store()
    {
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var dbName = nameof(Save_rejects_plan_from_another_store);
        var planId = await SeedPlanAsync(storeA, dbName);

        await using var db = Ctx(storeB, dbName);
        var result = await new EditModel(db).OnPostSaveAsync(planId, new EditModel.PlanDto());

        Assert.IsType<NotFoundResult>(result);
    }
}
