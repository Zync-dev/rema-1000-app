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

        var subject = "Påmindelse: " + Shorten(r.Text, 60);
        var text =
            $"{r.Text}\n\n" +
            $"Tidspunkt: {when} kl. {time}\n\n" +
            "— sendt automatisk fra Rema Butiksværktøjer";

        var html =
            $"<p style=\"font-size:16px\"><strong>{System.Net.WebUtility.HtmlEncode(r.Text)}</strong></p>" +
            $"<p style=\"color:#555\">Tidspunkt: {when} kl. {time}</p>" +
            "<p style=\"color:#999;font-size:12px\">Sendt automatisk fra Rema Butiksværktøjer.</p>";

        return new EmailMessage(toEmail, toName, subject, text, html);
    }

    private static string Shorten(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
}
