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
                    new() { Label = "A1", Kind = "FuldPalle", X = 10, Y = 10, Width = 100, Height = 80, Offer = "Cola 5 kr" },
                    new() { Label = "A2", Kind = "Endeboks", X = 200, Y = 10, Width = 200, Height = 60 },
                ],
            });
            Assert.IsType<JsonResult>(result);
        }

        Guid keepId;
        await using (var db = Ctx(store, dbName))
        {
            var boxes = await db.FloorBoxes.OrderBy(b => b.Label).ToListAsync();
            Assert.Equal(2, boxes.Count);
            Assert.Equal(BoxKind.Endeboks, boxes[1].Kind);
            // Faste typer får deres rigtige størrelse uanset hvad klienten sendte.
            Assert.Equal((120, 80), (boxes[0].Width, boxes[0].Height));
            Assert.Equal((133, 90), (boxes[1].Width, boxes[1].Height));
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
                    new() { Id = keepId, Label = "A1-ny", Kind = "FuldPalle", X = 10, Y = 10, Width = 100, Height = 80 },
                    new() { Label = "A3", Kind = "Andet", X = 400, Y = 400, Width = 90, Height = 90 },
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
                Boxes = [new() { Label = "X", Kind = "Andet", X = -100, Y = -100, Width = 5, Height = 999999 }],
            });
        }

        await using (var db = Ctx(store, dbName))
        {
            var plan = await db.FloorPlans.Include(p => p.Boxes).FirstAsync(p => p.Id == planId);
            Assert.Equal(6000, plan.CanvasWidth);
            Assert.Equal(200, plan.CanvasHeight);
            var box = plan.Boxes.Single();
            Assert.Equal(0, box.X);
            Assert.Equal(30, box.Width);
            Assert.Equal(6000, box.Height);
        }
    }

    [Fact]
    public async Task Save_keeps_split_cells_and_rotated_fixed_size()
    {
        var store = Guid.NewGuid();
        var dbName = nameof(Save_keeps_split_cells_and_rotated_fixed_size);
        var planId = await SeedPlanAsync(store, dbName);

        await using (var db = Ctx(store, dbName))
        {
            await new EditModel(db).OnPostSaveAsync(planId, new EditModel.PlanDto
            {
                CanvasWidth = 1400, CanvasHeight = 900,
                Boxes =
                [
                    // Delt palle: pærer / æbler
                    new() { Label = "A1", Kind = "FuldPalle", Split = "LeftRight", Offer = "Pærer", OfferB = "Æbler", X = 0, Y = 0, Width = 120, Height = 80 },
                    // Roteret fuld palle (dybde > bredde)
                    new() { Label = "A2", Kind = "FuldPalle", X = 300, Y = 0, Width = 80, Height = 120 },
                ],
            });
        }

        await using (var db = Ctx(store, dbName))
        {
            var boxes = await db.FloorBoxes.OrderBy(b => b.Label).ToListAsync();
            Assert.Equal(SplitMode.LeftRight, boxes[0].Split);
            Assert.Equal("Pærer", boxes[0].Offer);
            Assert.Equal("Æbler", boxes[0].OfferB);
            Assert.Equal((120, 80), (boxes[0].Width, boxes[0].Height));
            // roteret: den faste størrelse byttes om
            Assert.Equal((80, 120), (boxes[1].Width, boxes[1].Height));
        }

        // Fjern opdelingen igen -> celle B ryddes
        await using (var db = Ctx(store, dbName))
        {
            var a1 = await db.FloorBoxes.FirstAsync(b => b.Label == "A1");
            await new EditModel(db).OnPostSaveAsync(planId, new EditModel.PlanDto
            {
                CanvasWidth = 1400, CanvasHeight = 900,
                Boxes = [new() { Id = a1.Id, Label = "A1", Kind = "FuldPalle", Split = "None", Offer = "Pærer", OfferB = "Æbler", Width = 120, Height = 80 }],
            });
        }
        await using (var db = Ctx(store, dbName))
        {
            var a1 = await db.FloorBoxes.SingleAsync();
            Assert.Equal(SplitMode.None, a1.Split);
            Assert.Null(a1.OfferB);
        }
    }

    [Fact]
    public async Task Save_stores_and_sanitizes_shapes()
    {
        var store = Guid.NewGuid();
        var dbName = nameof(Save_stores_and_sanitizes_shapes);
        var planId = await SeedPlanAsync(store, dbName);

        await using (var db = Ctx(store, dbName))
        {
            await new EditModel(db).OnPostSaveAsync(planId, new EditModel.PlanDto
            {
                CanvasWidth = 1400, CanvasHeight = 900,
                Shapes =
                [
                    new() { Kind = "rect", Color = "#0a4d9c", Width = 8, Points = [[10, 10], [400, 300]] },
                    new() { Kind = "pen", Color = "#badcol", Width = 999, Points = [[0, 0], [50, 20], [80, 90]] },
                    new() { Kind = "line", Points = [[5, 5]] },               // for få punkter -> kasseret
                ],
            });
        }

        await using (var db = Ctx(store, dbName))
        {
            var plan = await db.FloorPlans.FirstAsync(p => p.Id == planId);
            var shapes = FloorShapes.Parse(plan.ShapesJson);
            Assert.Equal(2, shapes.Count);
            Assert.Equal("rect", shapes[0].Kind);
            Assert.Equal("#0a4d9c", shapes[0].Color);
            Assert.Equal(FloorShapes.DefaultColor, shapes[1].Color);          // ugyldig farve -> standard
            Assert.Equal(60, shapes[1].Width);                                // klampet
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
