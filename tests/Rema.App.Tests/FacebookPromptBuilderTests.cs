using Rema.App.Data.Entities;
using Rema.App.Services.Ai;

namespace Rema.App.Tests;

public class FacebookPromptBuilderTests
{
    private static readonly Store Store = new() { Name = "Rema 1000 Nørrebrogade", City = "København" };

    [Fact]
    public void System_prompt_embeds_store_style_and_examples()
    {
        var s = new StoreAiSettings
        {
            Tone = "Uformel og glad",
            EmojiUsage = EmojiUsage.Rich,
            SignOff = "Vi ses i butikken!",
            Hashtags = "#rema1000 #nørrebro",
            Address = "Nørrebrogade 1",
            OpeningHours = "Man-søn 7-22",
            ExtraGuidance = "Skriv 'du', ikke 'De'.",
        };

        var sys = FacebookPromptBuilder.BuildSystem(Store, s, ["Kæmpe tilbud i dag! 🎉"]);

        Assert.Contains("Rema 1000 Nørrebrogade", sys);
        Assert.Contains("København", sys);
        Assert.Contains("Uformel og glad", sys);
        Assert.Contains("flere emoji", sys);
        Assert.Contains("Vi ses i butikken!", sys);
        Assert.Contains("#rema1000 #nørrebro", sys);
        Assert.Contains("Nørrebrogade 1", sys);
        Assert.Contains("Skriv 'du', ikke 'De'.", sys);
        Assert.Contains("Kæmpe tilbud i dag!", sys);
        Assert.Contains("Find aldrig på priser", sys);
    }

    [Fact]
    public void System_prompt_has_sensible_defaults_when_profile_is_empty()
    {
        var sys = FacebookPromptBuilder.BuildSystem(Store, new StoreAiSettings(), []);

        Assert.Contains("dansk", sys);
        Assert.Contains("få, velvalgte emoji", sys);
        Assert.DoesNotContain("EKSEMPLER", sys);
    }

    [Fact]
    public void Competition_user_prompt_adds_disclaimer_and_store_rules()
    {
        var s = new StoreAiSettings { CompetitionRulesText = "Man skal være 18 år. Vinder kontaktes via Facebook." };

        var user = FacebookPromptBuilder.BuildUser(s, FacebookPostType.Konkurrence, "Vind et gavekort på 500 kr.");

        Assert.Contains("Vind et gavekort på 500 kr.", user);
        Assert.Contains("ikke er sponsoreret af eller tilknyttet Facebook", user);
        Assert.Contains("Man skal være 18 år", user);
    }

    [Fact]
    public void Non_competition_user_prompt_omits_competition_block()
    {
        var user = FacebookPromptBuilder.BuildUser(new StoreAiSettings(), FacebookPostType.Tilbud, "Cola 10 kr");

        Assert.Contains("Cola 10 kr", user);
        Assert.DoesNotContain("sponsoreret", user);
        Assert.DoesNotContain("deltagernes oplysninger", user);
    }
}
