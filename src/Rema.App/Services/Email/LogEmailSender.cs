namespace Rema.App.Services.Email;

/// <summary>
/// Bruges når der ikke er sat en SMTP-server op. Sender ingenting, men logger
/// hele mailen, så man kan se hvad der ville være sendt (og opdage manglende opsætning).
/// </summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogWarning(
            "MAIL IKKE SENDT (ingen SMTP opsat). Til: {To} · Emne: {Subject}\n{Body}",
            message.ToEmail, message.Subject, message.BodyText);
        return Task.FromResult(true);
    }
}
