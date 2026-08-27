using Rema.App.Data.Entities;

namespace Rema.App.Services.Ai;

public sealed record GeneratedPost(string Text, string Model, int InputTokens, int OutputTokens);

/// <summary>Fejl der er sikre at vise til brugeren (fx ugyldig nøgle, rate limit).</summary>
public sealed class AiGenerationException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IFacebookPostGenerator
{
    Task<GeneratedPost> GenerateAsync(
        Store store,
        StoreAiSettings settings,
        IReadOnlyList<string> examples,
        FacebookPostType type,
        string brief,
        CancellationToken ct = default);

    /// <summary>Lille kald der bekræfter at API-nøglen virker.</summary>
    Task<bool> TestConnectionAsync(string apiKey, string model, CancellationToken ct = default);
}
