using Rema.App.Data.Entities;

namespace Rema.App.Services.Ai;

/// <summary>
/// Genererer Facebook-opslag via Google Gemini (gratis niveau) med butikkens egen
/// API-nøgle. Korte opslag: begrænset output.
/// </summary>
public sealed class GeminiFacebookPostGenerator(
    GeminiClient gemini,
    ApiKeyProtector protector) : IFacebookPostGenerator
{
    public const string DefaultModel = "gemini-3.7-flash";
    private const int MaxOutputTokens = 2000;

    public async Task<GeneratedPost> GenerateAsync(
        Store store,
        StoreAiSettings settings,
        IReadOnlyList<string> examples,
        FacebookPostType type,
        string brief,
        CancellationToken ct = default)
    {
        var apiKey = protector.TryUnprotect(settings.ApiKeyProtected)
            ?? throw new AiGenerationException(
                "Butikkens API-nøgle mangler eller kunne ikke læses. Bed købmanden om at indtaste den igen under Indstillinger.");

        var model = string.IsNullOrWhiteSpace(settings.Model) ? DefaultModel : settings.Model.Trim();
        var system = FacebookPromptBuilder.BuildSystem(store, settings, examples);
        var user = FacebookPromptBuilder.BuildUser(settings, type, brief);

        var result = await gemini.GenerateAsync(apiKey, model, system, user, MaxOutputTokens, ct);

        return new GeneratedPost(result.Text, model, result.PromptTokens, result.OutputTokens);
    }

    public async Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct = default)
    {
        try
        {
            await gemini.GenerateAsync(
                apiKey,
                string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim(),
                system: "Svar kun med ordet OK.",
                user: "ping",
                maxOutputTokens: 8,
                ct);
            return true;
        }
        catch (AiGenerationException)
        {
            return false;
        }
    }
}
