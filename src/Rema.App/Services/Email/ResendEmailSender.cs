using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Rema.App.Services.Email;

/// <summary>
/// Sender mail via Resends REST-API (<c>POST /emails</c>). API-nøglen sendes i
/// Authorization-headeren – aldrig i URL'en. Bruges når
/// <see cref="EmailOptions.IsConfigured"/> er sand.
/// </summary>
public sealed class ResendEmailSender(
    HttpClient http,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public const string BaseAddress = "https://api.resend.com/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly EmailOptions _o = options.Value;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var payload = new SendRequest
        {
            From = string.IsNullOrWhiteSpace(_o.FromName) ? _o.FromEmail : $"{_o.FromName} <{_o.FromEmail}>",
            To = [message.ToEmail],
            Subject = message.Subject,
            Text = message.BodyText,
            Html = message.BodyHtml,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(payload, options: Json),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ApiKey);

        try
        {
            using var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return true;

            var body = await resp.Content.ReadAsStringAsync(ct);
            string? apiMessage = null;
            try { apiMessage = JsonSerializer.Deserialize<ErrorBody>(body, Json)?.Message; }
            catch (JsonException) { /* behold rå tekst */ }

            logger.LogError(
                "Resend afviste mail til {To}: {Status} {Message}",
                message.ToEmail, (int)resp.StatusCode, apiMessage ?? body);

            // 401/403/422 = varig fejl (nøgle/afsender/domæne), 429/5xx = midlertidig.
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Kunne ikke nå Resend for mail til {To}", message.ToEmail);
            return false;
        }
    }

    private sealed class SendRequest
    {
        [JsonPropertyName("from")] public string From { get; set; } = "";
        [JsonPropertyName("to")] public List<string> To { get; set; } = [];
        [JsonPropertyName("subject")] public string Subject { get; set; } = "";
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("html")] public string? Html { get; set; }
    }

    private sealed class ErrorBody
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
