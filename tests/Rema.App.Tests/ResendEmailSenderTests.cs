using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rema.App.Services.Email;

namespace Rema.App.Tests;

public class ResendEmailSenderTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (ResendEmailSender sender, StubHandler handler) Make(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri(ResendEmailSender.BaseAddress) };
        var opts = Options.Create(new EmailOptions
        {
            ApiKey = "re_test_123",
            FromEmail = "noreply@butik.dk",
            FromName = "Rema Butiksværktøjer",
        });
        return (new ResendEmailSender(http, opts, NullLogger<ResendEmailSender>.Instance), handler);
    }

    private static readonly EmailMessage Msg =
        new("karen@example.com", "Karen", "Påmindelse: rundstykker", "hej", "<p>hej</p>");

    [Fact]
    public async Task Sends_bearer_key_in_header_and_expected_payload()
    {
        var (sender, handler) = Make(HttpStatusCode.OK, """{ "id": "abc-123" }""");

        var ok = await sender.SendAsync(Msg);

        Assert.True(ok);
        Assert.Equal("emails", handler.LastRequest!.RequestUri!.AbsolutePath.TrimStart('/'));
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("re_test_123", handler.LastRequest.Headers.Authorization.Parameter);
        Assert.DoesNotContain("re_test_123", handler.LastRequest.RequestUri!.ToString());

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;
        Assert.Equal("Rema Butiksværktøjer <noreply@butik.dk>", root.GetProperty("from").GetString());
        Assert.Equal("karen@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal("Påmindelse: rundstykker", root.GetProperty("subject").GetString());
        Assert.Equal("hej", root.GetProperty("text").GetString());
        Assert.Equal("<p>hej</p>", root.GetProperty("html").GetString());
    }

    [Fact]
    public async Task Returns_false_on_rejected_send()
    {
        var (sender, _) = Make(HttpStatusCode.UnprocessableEntity,
            """{ "name": "validation_error", "message": "The from address is not verified." }""");

        Assert.False(await sender.SendAsync(Msg));
    }

    [Fact]
    public async Task Returns_false_when_resend_is_unreachable()
    {
        var handler = new ThrowingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(ResendEmailSender.BaseAddress) };
        var sender = new ResendEmailSender(http,
            Options.Create(new EmailOptions { ApiKey = "re_x", FromEmail = "a@b.dk" }),
            NullLogger<ResendEmailSender>.Instance);

        Assert.False(await sender.SendAsync(Msg));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("no network");
    }
}
