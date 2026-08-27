using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rema.App.Services.Ai;

/// <summary>
/// Tynd klient til Google Gemini' <c>generateContent</c>-endpoint (gratis niveau).
/// API-nøglen sendes i <c>x-goog-api-key</c>-headeren – aldrig i URL'en.
/// </summary>
public sealed class GeminiClient(HttpClient http, ILogger<GeminiClient> logger)
{
    public const string BaseAddress = "https://generativelanguage.googleapis.com/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record Result(string Text, int PromptTokens, int OutputTokens);

    public async Task<Result> GenerateAsync(
        string apiKey, string model, string system, string user,
        int maxOutputTokens, CancellationToken ct)
    {
        var body = new GenerateRequest
        {
            SystemInstruction = new Content { Parts = [new Part { Text = system }] },
            Contents = [new Content { Role = "user", Parts = [new Part { Text = user }] }],
            GenerationConfig = new GenConfig { MaxOutputTokens = maxOutputTokens, Temperature = 0.85 },
            SafetySettings = SafetyCategories.Select(c => new SafetySetting { Category = c, Threshold = "BLOCK_ONLY_HIGH" }).ToList(),
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        req.Headers.Add("x-goog-api-key", apiKey);

        using var resp = await http.SendAsync(req, ct);
        var payload = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw MapError(resp.StatusCode, payload);

        GenerateResponse? parsed;
        try { parsed = JsonSerializer.Deserialize<GenerateResponse>(payload, Json); }
        catch (JsonException ex) { throw new AiGenerationException("Uventet svar fra Gemini.", ex); }

        if (parsed?.PromptFeedback?.BlockReason is { Length: > 0 } blocked)
            throw new AiGenerationException($"Gemini blokerede forespørgslen ({blocked}). Prøv at omformulere oplysningerne.");

        var candidate = parsed?.Candidates?.FirstOrDefault();
        if (candidate is null)
            throw new AiGenerationException("Gemini returnerede ingen tekst. Prøv igen.");

        if (candidate.FinishReason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST")
            throw new AiGenerationException("Gemini afviste at skrive dette opslag. Prøv at omformulere oplysningerne.");

        var text = string.Concat(candidate.Content?.Parts?.Select(p => p.Text) ?? []).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new AiGenerationException("Gemini returnerede et tomt svar. Prøv igen.");

        return new Result(
            text,
            parsed!.UsageMetadata?.PromptTokenCount ?? 0,
            parsed.UsageMetadata?.CandidatesTokenCount ?? 0);
    }

    private AiGenerationException MapError(HttpStatusCode status, string payload)
    {
        string? apiMessage = null;
        try { apiMessage = JsonSerializer.Deserialize<ErrorEnvelope>(payload, Json)?.Error?.Message; }
        catch (JsonException) { /* ignore */ }

        logger.LogWarning("Gemini-fejl {Status}: {Message}", (int)status, apiMessage);

        return status switch
        {
            HttpStatusCode.BadRequest when apiMessage?.Contains("API key not valid", StringComparison.OrdinalIgnoreCase) == true
                => new AiGenerationException("API-nøglen blev afvist. Tjek den under Indstillinger."),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => new AiGenerationException("API-nøglen blev afvist eller mangler adgang. Tjek den under Indstillinger."),
            HttpStatusCode.NotFound
                => new AiGenerationException(
                    "Den valgte model findes ikke (længere). Skift model under Indstillinger."
                    + (string.IsNullOrWhiteSpace(apiMessage) ? "" : $" Google skriver: {apiMessage}")),
            HttpStatusCode.TooManyRequests
                => new AiGenerationException("I har nået Geminis gratis-grænse for i dag/minuttet. Prøv igen senere."),
            _ => new AiGenerationException("Der opstod en fejl i kaldet til Gemini. Prøv igen."),
        };
    }

    private static readonly string[] SafetyCategories =
    [
        "HARM_CATEGORY_HARASSMENT",
        "HARM_CATEGORY_HATE_SPEECH",
        "HARM_CATEGORY_SEXUALLY_EXPLICIT",
        "HARM_CATEGORY_DANGEROUS_CONTENT",
    ];

    // --- Wire DTOs ------------------------------------------------------
    private sealed class GenerateRequest
    {
        [JsonPropertyName("systemInstruction")] public Content? SystemInstruction { get; set; }
        [JsonPropertyName("contents")] public List<Content> Contents { get; set; } = [];
        [JsonPropertyName("generationConfig")] public GenConfig? GenerationConfig { get; set; }
        [JsonPropertyName("safetySettings")] public List<SafetySetting>? SafetySettings { get; set; }
    }

    private sealed class Content
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("parts")] public List<Part> Parts { get; set; } = [];
    }

    private sealed class Part
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class GenConfig
    {
        [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
    }

    private sealed class SafetySetting
    {
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("threshold")] public string Threshold { get; set; } = "";
    }

    private sealed class GenerateResponse
    {
        [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; }
        [JsonPropertyName("promptFeedback")] public PromptFeedback? PromptFeedback { get; set; }
        [JsonPropertyName("usageMetadata")] public UsageMetadata? UsageMetadata { get; set; }
    }

    private sealed class Candidate
    {
        [JsonPropertyName("content")] public Content? Content { get; set; }
        [JsonPropertyName("finishReason")] public string? FinishReason { get; set; }
    }

    private sealed class PromptFeedback
    {
        [JsonPropertyName("blockReason")] public string? BlockReason { get; set; }
    }

    private sealed class UsageMetadata
    {
        [JsonPropertyName("promptTokenCount")] public int PromptTokenCount { get; set; }
        [JsonPropertyName("candidatesTokenCount")] public int CandidatesTokenCount { get; set; }
    }

    private sealed class ErrorEnvelope
    {
        [JsonPropertyName("error")] public ErrorBody? Error { get; set; }
        public sealed class ErrorBody
        {
            [JsonPropertyName("message")] public string? Message { get; set; }
            [JsonPropertyName("status")] public string? Status { get; set; }
        }
    }
}
