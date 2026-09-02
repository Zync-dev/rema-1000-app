using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Rema.App.Services.Email;

/// <summary>Sender mail via SMTP (MailKit). Bruges når <see cref="EmailOptions.IsConfigured"/> er sand.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _o = options.Value;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_o.FromName, _o.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToEmail, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.BodyText,
            HtmlBody = message.BodyHtml,
        }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var socketOptions = _o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_o.Host!, _o.Port, socketOptions, ct);
            if (!string.IsNullOrWhiteSpace(_o.Username))
                await client.AuthenticateAsync(_o.Username, _o.Password ?? "", ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kunne ikke sende mail til {To}", message.ToEmail);
            return false;
        }
    }
}
