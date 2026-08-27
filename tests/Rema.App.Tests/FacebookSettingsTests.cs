using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data.Entities;
using Rema.App.Services.Ai;
using Rema.App.Pages.FacebookPost;

namespace Rema.App.Tests;

public class FacebookSettingsTests
{
    private sealed class StubGenerator : IFacebookPostGenerator
    {
        public Task<GeneratedPost> GenerateAsync(Store s, StoreAiSettings st, IReadOnlyList<string> ex, FacebookPostType t, string b, CancellationToken ct = default)
            => Task.FromResult(new GeneratedPost("tekst", "gemini-2.5-flash", 1, 1));
        public Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private static SettingsModel NewModel(Rema.App.Data.AppDbContext db) =>
        new SettingsModel(db, new ApiKeyProtector(new EphemeralDataProtectionProvider()), new StubGenerator())
            .Wire();

    [Fact]
    public async Task Save_creates_settings_encrypts_key_and_stores_examples()
    {
        var store = Guid.NewGuid();
        var dbName = TestDb.NewName();

        await using (var db = TestDb.For(store, dbName))
        {
            var model = NewModel(db);
            model.Input = new SettingsModel.InputModel
            {
                Model = "gemini-2.5-flash-lite",
                NewApiKey = "sk-ant-secret-9999",
                Tone = "Glad",
                SignOff = "Vi ses!",
                Examples = ["Eksempel et", "", "Eksempel tre"],
            };
            var result = await model.OnPostAsync();
            Assert.IsType<RedirectToPageResult>(result);
        }

        await using (var db = TestDb.For(store, dbName))
        {
            var s = await db.StoreAiSettings.Include(x => x.Examples).SingleAsync();
            Assert.Equal("gemini-2.5-flash-lite", s.Model);
            Assert.Equal(store, s.StoreId);
            Assert.NotNull(s.ApiKeyProtected);
            Assert.NotEqual("sk-ant-secret-9999", s.ApiKeyProtected);
            Assert.Equal("····9999", s.ApiKeyHint);
            Assert.Equal(2, s.Examples.Count);
            Assert.Contains(s.Examples, e => e.Text == "Eksempel tre");
            Assert.All(s.Examples, e => Assert.Equal(store, e.StoreId));
        }
    }

    [Fact]
    public async Task Save_can_remove_key_without_touching_style()
    {
        var store = Guid.NewGuid();
        var dbName = TestDb.NewName();

        await using (var db = TestDb.For(store, dbName))
        {
            db.StoreAiSettings.Add(new StoreAiSettings
            {
                StoreId = store, Model = "gemini-2.5-flash",
                ApiKeyProtected = "blob", ApiKeyHint = "····1234", Tone = "Fast tone",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.For(store, dbName))
        {
            var model = NewModel(db);
            model.Input = new SettingsModel.InputModel { Model = "gemini-2.5-flash", RemoveApiKey = true, Tone = "Fast tone" };
            await model.OnPostAsync();
        }

        await using (var db = TestDb.For(store, dbName))
        {
            var s = await db.StoreAiSettings.SingleAsync();
            Assert.Null(s.ApiKeyProtected);
            Assert.Null(s.ApiKeyHint);
            Assert.Equal("Fast tone", s.Tone);
        }
    }

    [Fact]
    public async Task Settings_are_isolated_per_store()
    {
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var dbName = TestDb.NewName();

        await using (var db = TestDb.For(storeA, dbName))
        {
            db.StoreAiSettings.Add(new StoreAiSettings { StoreId = storeA, Model = "gemini-2.5-flash", ApiKeyProtected = "a" });
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.For(storeB, dbName))
        {
            Assert.Null(await db.StoreAiSettings.FirstOrDefaultAsync());
        }
    }
}
