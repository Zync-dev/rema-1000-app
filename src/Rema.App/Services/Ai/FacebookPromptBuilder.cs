using System.Text;
using Rema.App.Data.Entities;

namespace Rema.App.Services.Ai;

/// <summary>
/// Bygger system- og brugerprompt til Facebook-opslagsgeneratoren ud fra
/// butikkens stilprofil. Systemprompten er den samme uanset opslagstype, så
/// prompt-caching kan udnyttes; kun brugerbeskeden varierer.
/// </summary>
public static class FacebookPromptBuilder
{
    public static string BuildSystem(Store store, StoreAiSettings s, IReadOnlyList<string> examples)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Du skriver opslag til Facebook-siden for {store.Name}, en Rema 1000-butik i {store.City}.");
        sb.AppendLine("Skriv altid på dansk, i butikkens faste stil, klar til at kopiere direkte ind på Facebook.");
        sb.AppendLine();

        sb.AppendLine("STIL:");
        sb.AppendLine($"- Tone: {Or(s.Tone, "imødekommende, lokal og konkret – som en købmand der taler til sine kunder")}");
        sb.AppendLine("- Emoji: " + s.EmojiUsage switch
        {
            EmojiUsage.None => "brug ingen emoji.",
            EmojiUsage.Rich => "brug gerne flere emoji, hvor det passer.",
            _ => "brug få, velvalgte emoji.",
        });
        if (!string.IsNullOrWhiteSpace(s.SignOff))
            sb.AppendLine($"- Afslut opslaget med: \"{s.SignOff.Trim()}\"");
        if (!string.IsNullOrWhiteSpace(s.Hashtags))
            sb.AppendLine($"- Afslut med hashtags: {s.Hashtags.Trim()}");
        if (!string.IsNullOrWhiteSpace(s.ExtraGuidance))
            sb.AppendLine($"- Yderligere retningslinjer: {s.ExtraGuidance.Trim()}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(s.Address) || !string.IsNullOrWhiteSpace(s.OpeningHours))
        {
            sb.AppendLine("BUTIKSFAKTA (brug kun hvis det er relevant for opslaget):");
            if (!string.IsNullOrWhiteSpace(s.Address)) sb.AppendLine($"- Adresse: {s.Address.Trim()}");
            if (!string.IsNullOrWhiteSpace(s.OpeningHours)) sb.AppendLine($"- Åbningstider: {s.OpeningHours.Trim()}");
            sb.AppendLine();
        }

        sb.AppendLine("REGLER:");
        sb.AppendLine("- Find aldrig på priser, datoer, procenter, produkter eller vilkår. Brug kun de oplysninger brugeren giver.");
        sb.AppendLine("- Mangler en nødvendig oplysning, så skriv en tydelig plads i kantede klammer, fx [indsæt slutdato].");
        sb.AppendLine("- Undgå påstande der kan være vildledende (\"billigst\", \"Danmarks bedste\") medmindre brugeren udtrykkeligt beder om det.");
        sb.AppendLine("- Hold opslaget kort og læsevenligt. Returnér KUN selve opslagsteksten – ingen forklaring, ingen overskrift som \"Facebook-opslag:\".");
        sb.AppendLine();

        var trimmed = examples.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).ToList();
        if (trimmed.Count > 0)
        {
            sb.AppendLine("EKSEMPLER PÅ BUTIKKENS STIL (match tone og længde, kopiér ikke indholdet):");
            for (var i = 0; i < trimmed.Count; i++)
            {
                sb.AppendLine($"--- Eksempel {i + 1} ---");
                sb.AppendLine(trimmed[i]);
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildUser(StoreAiSettings s, FacebookPostType type, string brief)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Lav et Facebook-opslag af typen: {TypeLabel(type)}.");
        sb.AppendLine();
        sb.AppendLine("Oplysninger til opslaget:");
        sb.AppendLine(brief.Trim());

        if (type == FacebookPostType.Konkurrence)
        {
            sb.AppendLine();
            sb.AppendLine("Da dette er en konkurrence, skal opslaget også:");
            sb.AppendLine("- forklare tydeligt hvordan man deltager, og hvornår vinderen findes,");
            sb.AppendLine("- oplyse at konkurrencen ikke er sponsoreret af eller tilknyttet Facebook,");
            sb.AppendLine("- kort nævne hvordan deltagernes oplysninger bruges (kun til at finde og kontakte vinderen).");
            if (!string.IsNullOrWhiteSpace(s.CompetitionRulesText))
            {
                sb.AppendLine("- afslutte med disse betingelser ordret:");
                sb.AppendLine(s.CompetitionRulesText.Trim());
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string TypeLabel(FacebookPostType t) => t switch
    {
        FacebookPostType.Tilbud => "tilbud/gode køb",
        FacebookPostType.Konkurrence => "konkurrence",
        FacebookPostType.NyMedarbejder => "præsentation af ny medarbejder",
        FacebookPostType.Aabningstider => "ændrede åbningstider (helligdag e.l.)",
        FacebookPostType.Event => "lokalt event i eller ved butikken",
        _ => "andet",
    };
}
