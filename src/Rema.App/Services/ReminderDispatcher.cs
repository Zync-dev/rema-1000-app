using Microsoft.EntityFrameworkCore;
using Rema.App.Data;
using Rema.App.Data.Entities;
using Rema.App.Services.Email;

namespace Rema.App.Services;

/// <summary>
/// Sender de påmindelser hvis tid er kommet. Kaldes fra <see cref="ReminderDispatcher"/>
/// hvert minut. Forespørger med <c>IgnoreQueryFilters</c> fordi der ikke er nogen
/// butikskontekst i et baggrundsjob – hver påmindelse bærer selv sit <c>StoreId</c>.
/// </summary>
public sealed class ReminderSender(
    AppDbContext db,
    IEmailSender email,
    ILogger<ReminderSender> logger)
{
    public const int MaxAttempts = 4;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    /// <returns>Antal påmindelser der blev afsendt.</returns>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var due = await db.Reminders.IgnoreQueryFilters()
            .Where(r => r.Status == ReminderStatus.Scheduled && r.SendAtUtc <= now)
            .OrderBy(r => r.SendAtUtc)
            .Take(25)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        var sentCount = 0;
        foreach (var r in due)
        {
            r.Attempts++;
            var (toEmail, toName) = await ResolveRecipientAsync(r, ct);

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                r.Status = ReminderStatus.Failed;
                r.SentUtc = now;
                r.Error = "Modtageren har ingen e-mailadresse.";
                continue;
            }

            bool ok;
            try
            {
                ok = await email.SendAsync(ReminderMail.Build(r, toEmail, toName), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Uventet fejl ved afsendelse af påmindelse {Id}", r.Id);
                ok = false;
            }

            if (ok)
            {
                r.Status = ReminderStatus.Sent;
                r.SentUtc = now;
                r.Error = null;
                sentCount++;
                logger.LogInformation("Påmindelse {Id} sendt til {To}", r.Id, toEmail);
            }
            else if (r.Attempts >= MaxAttempts)
            {
                r.Status = ReminderStatus.Failed;
                r.SentUtc = now;
                r.Error = $"Mailen kunne ikke sendes efter {r.Attempts} forsøg.";
            }
            else
            {
                r.Error = $"Mailen kunne ikke sendes ({r.Attempts}. forsøg). Prøver igen.";
                r.SendAtUtc = now.Add(RetryDelay);
            }
        }

        await db.SaveChangesAsync(ct);
        return sentCount;
    }

    private async Task<(string? email, string? name)> ResolveRecipientAsync(Reminder r, CancellationToken ct)
    {
        if (r.RecipientUserId is Guid uid)
        {
            var user = await db.Users.IgnoreQueryFilters()
                .Where(u => u.Id == uid)
                .Select(u => new { u.Email, u.DisplayName })
                .FirstOrDefaultAsync(ct);
            return (user?.Email, user?.DisplayName);
        }
        return (r.RecipientEmail, r.RecipientName);
    }
}

/// <summary>Kører <see cref="ReminderSender"/> i en løkke, én gang i minuttet.</summary>
public sealed class ReminderDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<ReminderDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Påmindelses-dispatcher startet – tjekker hvert {Seconds}. sekund.", Interval.TotalSeconds);
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ReminderSender>();
                await sender.RunAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Påmindelses-dispatcheren fejlede i denne omgang");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Bygger selve mailen for en påmindelse.</summary>
public static class ReminderMail
{
    public static EmailMessage Build(Reminder r, string toEmail, string? toName)
    {
        var whenLocal = TimeZoneInfo.ConvertTime(r.DueAtUtc, DanishTime.Zone);
        var when = DateOnly.FromDateTime(whenLocal.Date).ToWeekdayDate();
        var time = whenLocal.ToString("HH:mm");
        var firstName = FirstName(toName);

        var subject = "Påmindelse: " + Shorten(r.Text, 60);

        var text =
            "PÅMINDELSE\n\n" +
            (firstName is null ? "" : $"Hej {firstName},\n\n") +
            $"{r.Text}\n\n" +
            $"Hvornår:  {when} kl. {time}\n\n" +
            "—\nSendt automatisk fra Rema Butiksværktøjer";

        return new EmailMessage(toEmail, toName, subject, text, BuildHtml(r.Text, when, time, firstName));
    }

    private static string BuildHtml(string bodyRaw, string when, string time, string? firstName)
    {
        var body = System.Net.WebUtility.HtmlEncode(bodyRaw);
        var preheader = System.Net.WebUtility.HtmlEncode(Shorten(bodyRaw, 100));
        var greeting = firstName is null
            ? ""
            : $"""<p style="margin:0 0 16px;font-size:15px;color:#1e2430;">Hej {System.Net.WebUtility.HtmlEncode(firstName)},</p>""";

        const string font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

        return $$"""
        <!DOCTYPE html>
        <html lang="da">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <meta name="color-scheme" content="light only">
        <title>Påmindelse</title>
        </head>
        <body style="margin:0;padding:0;background:#eceae3;-webkit-text-size-adjust:100%;">
        <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;">{{preheader}}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#eceae3;">
        <tr><td align="center" style="padding:28px 12px;">
        <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;background:#ffffff;border:1px solid #e2ddce;border-radius:16px;overflow:hidden;">
        <tr><td style="background:#0a4d9c;padding:20px 32px;">
        <span style="font-family:{{font}};font-size:19px;font-weight:800;letter-spacing:.5px;color:#ffffff;">REMA&nbsp;1000</span>
        <span style="font-family:{{font}};font-size:13px;font-weight:600;color:#a9c8ea;padding-left:9px;">Butiksværktøjer</span>
        </td></tr>
        <tr><td style="padding:32px 32px 28px;font-family:{{font}};">
        <p style="margin:0 0 18px;font-size:12px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:#e2231a;">Påmindelse</p>
        {{greeting}}
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border-left:4px solid #0a4d9c;background:#f6f5f0;border-radius:0 10px 10px 0;">
        <tr><td style="padding:20px 22px;">
        <p style="margin:0;font-size:20px;line-height:1.45;font-weight:700;color:#16213a;">{{body}}</p>
        <p style="margin:16px 0 0;font-size:15px;color:#44506a;"><strong style="color:#16213a;">{{when}}</strong> &nbsp;kl. {{time}}</p>
        </td></tr>
        </table>
        <p style="margin:24px 0 0;font-size:13px;line-height:1.6;color:#8b93a0;">Du får denne mail, så det ikke bliver glemt.</p>
        </td></tr>
        <tr><td style="padding:18px 32px;border-top:1px solid #eceae3;font-family:{{font}};">
        <p style="margin:0;font-size:12px;color:#9aa2ad;">Sendt automatisk fra Rema&nbsp;Butiksværktøjer.</p>
        </td></tr>
        </table>
        </td></tr>
        </table>
        </body>
        </html>
        """;
    }

    /// <summary>Kun fornavnet – "Karen Nielsen" → "Karen". Tomt/ukendt → null.</summary>
    private static string? FirstName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    private static string Shorten(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
}
