using Rema.App.Data.Entities;
using Rema.App.Pages.Team;
using Rema.App.Services;

namespace Rema.App.Tests;

public class TeamTests
{
    [Fact]
    public void Generated_passwords_meet_identity_rules()
    {
        for (var i = 0; i < 500; i++)
        {
            var pw = NewModel.GeneratePassword();
            Assert.Equal(12, pw.Length);
            Assert.Contains(pw, char.IsUpper);
            Assert.Contains(pw, char.IsLower);
            Assert.Contains(pw, char.IsDigit);
            Assert.DoesNotContain(pw, c => "O01lI".Contains(c)); // ingen let forvekslelige tegn
        }
    }

    [Fact]
    public void Generated_passwords_are_not_predictable()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 200; i++) set.Add(NewModel.GeneratePassword());
        Assert.True(set.Count > 190, "adgangskoderne skal være tilfældige");
    }

    [Theory]
    [InlineData(RoleNames.Koebmand, 0)]
    [InlineData(RoleNames.Souschef, 1)]
    [InlineData(RoleNames.Medarbejder, 2)]
    public void Role_rank_orders_by_access(string role, int rank) =>
        Assert.Equal(rank, RoleInfo.Rank(role));

    [Fact]
    public void Only_koebmand_can_assign_leder_roles()
    {
        Assert.Equal(
            new[] { RoleNames.Medarbejder, RoleNames.Souschef },
            RoleInfo.AssignableBy(actorIsKoebmand: false));

        Assert.Contains(RoleNames.Koebmand, RoleInfo.AssignableBy(actorIsKoebmand: true));
    }

    [Fact]
    public void Role_labels_are_danish()
    {
        Assert.Equal("Købmand", RoleInfo.Label(RoleNames.Koebmand));
        Assert.Equal("Souschef", RoleInfo.Label(RoleNames.Souschef));
        Assert.Equal("Medarbejder", RoleInfo.Label(RoleNames.Medarbejder));
    }
}
