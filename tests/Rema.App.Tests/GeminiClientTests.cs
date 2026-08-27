using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Rema.App.Services.Ai;

namespace Rema.App.Tests;

public class GeminiClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (GeminiClient client, StubHandler handler) Make(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri(GeminiClient.BaseAddress) };
        return (new GeminiClient(http, NullLogger<GeminiClient>.Instance), handler);
    }

    [Fact]
    public async Task Parses_text_and_usage_and_sends_key_in_header_not_url()
    {
        const string body = """
        {
          "candidates": [
            { "content": { "parts": [ { "text": "Kæmpe tilbud i dag! 🎉" } ] }, "finishReason": "STOP" }
          ],
          "usageMetadata": { "promptTokenCount": 320, "candidatesTokenCount": 88 }
        }
        """;
        var (client, handler) = Make(HttpStatusCode.OK, body);

        var result = await client.GenerateAsync("AIzaSECRET", "gemini-2.5-flash", "sys", "user", 2000, default);

        Assert.Equal("Kæmpe tilbud i dag! 🎉", result.Text);
        Assert.Equal(320, result.PromptTokens);
        Assert.Equal(88, result.OutputTokens);

        Assert.Equal("AIzaSECRET", handler.LastRequest!.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain("AIzaSECRET", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("gemini-2.5-flash:generateContent", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Invalid_key_maps_to_friendly_message()
    {
        const string body = """
        { "error": { "code": 400, "message": "API key not valid. Please pass a valid API key.", "status": "INVALID_ARGUMENT" } }
        """;
        var (client, _) = Make(HttpStatusCode.BadRequest, body);

        var ex = await Assert.ThrowsAsync<AiGenerationException>(
            () => client.GenerateAsync("bad", "gemini-2.5-flash", "s", "u", 100, default));
        Assert.Contains("API-nøglen blev afvist", ex.Message);
    }

    [Fact]
    public async Task Rate_limit_maps_to_friendly_message()
    {
        var (client, _) = Make(HttpStatusCode.TooManyRequests, """{ "error": { "message": "Quota exceeded" } }""");

        var ex = await Assert.ThrowsAsync<AiGenerationException>(
            () => client.GenerateAsync("k", "gemini-2.5-flash", "s", "u", 100, default));
        Assert.Contains("gratis-grænse", ex.Message);
    }

    [Fact]
    public async Task Safety_block_maps_to_friendly_message()
    {
        const string body = """
        { "candidates": [ { "finishReason": "SAFETY", "content": { "parts": [] } } ] }
        """;
        var (client, _) = Make(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<AiGenerationException>(
            () => client.GenerateAsync("k", "gemini-2.5-flash", "s", "u", 100, default));
        Assert.Contains("afviste at skrive", ex.Message);
    }

    [Fact]
    public async Task Prompt_block_maps_to_friendly_message()
    {
        var (client, _) = Make(HttpStatusCode.OK, """{ "promptFeedback": { "blockReason": "SAFETY" } }""");

        var ex = await Assert.ThrowsAsync<AiGenerationException>(
            () => client.GenerateAsync("k", "gemini-2.5-flash", "s", "u", 100, default));
        Assert.Contains("blokerede", ex.Message);
    }
}
