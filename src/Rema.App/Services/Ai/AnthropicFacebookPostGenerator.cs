using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Rema.App.Data.Entities;

namespace Rema.App.Services.Ai;

/// <summary>
/// Genererer Facebook-opslag via Anthropic Messages API med butikkens egen
/// API-nøgle. Korte, deterministiske opslag: lav effort, begrænset output.
/// </summary>
public sealed class AnthropicFacebookPostGenerator(
    ApiKeyProtector protector,
    ILogger<AnthropicFacebookPostGenerator> logger) : IFacebookPostGenerator
{
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

        var client = new AnthropicClient { ApiKey = apiKey };
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "claude-opus-5" : settings.Model.Trim();

        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = MaxOutputTokens,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            System = FacebookPromptBuilder.BuildSystem(store, settings, examples),
            Messages =
            [
                new() { Role = Role.User, Content = FacebookPromptBuilder.BuildUser(settings, type, brief) },
            ],
        };

        Message response;
        try
        {
            response = await client.Messages.Create(request, cancellationToken: ct);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new AiGenerationException("API-nøglen blev afvist. Tjek at den er korrekt under Indstillinger.", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new AiGenerationException("Anthropic er lige nu overbelastet for jeres konto. Prøv igen om lidt.", ex);
        }
        catch (AnthropicNotFoundException ex)
        {
            throw new AiGenerationException($"Modellen \"{model}\" findes ikke eller er ikke tilgængelig for jeres konto.", ex);
        }
        catch (AnthropicException ex)
        {
            logger.LogError(ex, "Anthropic-kald fejlede for butik {StoreId}", store.Id);
            throw new AiGenerationException("Der opstod en fejl i kaldet til AI-tjenesten. Prøv igen.", ex);
        }

        if (response.StopReason == "refusal")
            throw new AiGenerationException("AI'en afviste at skrive dette opslag. Prøv at omformulere oplysningerne.");

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new AiGenerationException("AI'en returnerede et tomt svar. Prøv igen.");

        return new GeneratedPost(
            text,
            model,
            (int)(response.Usage?.InputTokens ?? 0),
            (int)(response.Usage?.OutputTokens ?? 0));
    }

    public async Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct = default)
    {
        try
        {
            var client = new AnthropicClient { ApiKey = apiKey };
            await client.Messages.Create(new MessageCreateParams
            {
                Model = string.IsNullOrWhiteSpace(model) ? "claude-opus-5" : model.Trim(),
                MaxTokens = 4,
                Messages = [new() { Role = Role.User, Content = "ping" }],
            }, cancellationToken: ct);
            return true;
        }
        catch (AnthropicException ex)
        {
            logger.LogInformation(ex, "API-nøgletest fejlede");
            return false;
        }
    }
}
