using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;
using Rema.App.Services;
using Rema.App.Services.Email;

namespace Rema.App.Tests;

public class ReminderTests
{
    private sealed class FakeEmail : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public bool Succeed { get; set; } = true;
        public Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (Succeed) Sent.Add(message);
            return Task.FromResult(Succeed);
        }
    }

    private static Reminder DueReminder(Guid store, string email = "karen@butik.dk") => new()
    {
        StoreId = store,
        Text = "På mandag henter Karen 50 rundstykker",
        DueAtUtc = new DateTimeOffset(2026, 9, 7, 6, 0, 0, TimeSpan.Zero),
        SendAtUtc = new DateTimeOffset(2026, 9, 7, 5, 0, 0, TimeSpan.Zero),
        LeadMinutes = 60,
        RecipientName = "Karen",
        RecipientEmail = email,
    };

    [Fact]
    public void Mail_has_danish_time_and_short_subject()
    {
        var r = DueReminder(Guid.NewGuid());
        var msg = ReminderMail.Build(r, "karen@butik.dk", "Karen Nielsen");

        Assert.StartsWith("Påmindelse:", msg.Subject);
        Assert.Contains("rundstykker", msg.Subject);
        // 06:00 UTC den 7. sep = sommertid i DK → 08:00 lokalt
        Assert.Contains("kl. 08:00", msg.BodyText);
        Assert.Contains("mandag", msg.BodyText);
        Assert.Contains("Hej Karen,", msg.BodyText); // kun fornavn
    }

    [Fact]
    public void Html_is_a_full_branded_document_with_encoded_body()
    {
        var r = DueReminder(Guid.NewGuid());
        r.Text = "Ring til <chefen> & sig god weekend";
        var html = ReminderMail.Build(r, "karen@butik.dk", "Karen Nielsen").BodyHtml!;

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("REMA", html);
        Assert.Contains("kl. 08:00", html);
        Assert.Contains("Hej Karen,", html);
        // brugerteksten skal være HTML-escaped, ikke rå
        Assert.Contains("Ring til &lt;chefen&gt; &amp; sig god weekend", html);
        Assert.DoesNotContain("<chefen>", html);
    }

    [Fact]
    public void Html_without_a_name_has_no_greeting()
    {
        var r = DueReminder(Guid.NewGuid());
        r.RecipientName = null;
        var msg = ReminderMail.Build(r, "x@y.dk", null);
        Assert.DoesNotContain("Hej ", msg.BodyHtml!);
        Assert.DoesNotContain("Hej ", msg.BodyText);
    }

    [Fact]
    public void Long_subject_is_truncated()
    {
        var r = DueReminder(Guid.NewGuid());
        r.Text = new string('x', 200);
        var msg = ReminderMail.Build(r, "a@b.dk", null);
        Assert.True(msg.Subject.Length <= "Påmindelse: ".Length + 60);
    }

    [Fact]
    public void DanishTime_roundtrips_local_to_utc()
    {
        // 7. september kl. 08:00 dansk sommertid = 06:00 UTC
        var utc = DanishTime.ToUtc(new DateTime(2026, 9, 7, 8, 0, 0));
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 6, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public async Task Sender_sends_due_and_marks_sent()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Reminders.Add(DueReminder(store));
            db.Reminders.Add(new Reminder
            {
                StoreId = store, Text = "Senere", RecipientEmail = "x@y.dk",
                DueAtUtc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                SendAtUtc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            });
            await db.SaveChangesAsync();
        }

        var mail = new FakeEmail();
        await using (var db = TestDb.For(store, name))
        {
            var sender = new ReminderSender(db, mail, NullLogger<ReminderSender>.Instance);
            var n = await sender.RunAsync(new DateTimeOffset(2026, 9, 7, 5, 30, 0, TimeSpan.Zero));
            Assert.Equal(1, n);
        }

        Assert.Single(mail.Sent);
        await using (var db = TestDb.For(store, name))
        {
            var statuses = await db.Reminders.OrderBy(r => r.Text).Select(r => r.Status).ToListAsync();
            Assert.Equal(new[] { ReminderStatus.Sent, ReminderStatus.Scheduled }, statuses);
        }
    }

    [Fact]
    public async Task Sender_retries_then_fails_after_max_attempts()
    {
        var store = Guid.NewGuid();
        var name = TestDb.NewName();
        await using (var db = TestDb.For(store, name))
        {
            db.Reminders.Add(DueReminder(store));
            await db.SaveChangesAsync();
        }

        var mail = new FakeEmail { Succeed = false };
        var now = new DateTimeOffset(2026, 9, 7, 5, 30, 0, TimeSpan.Zero);

        for (var i = 0; i < ReminderSender.MaxAttempts; i++)
        {
            await using var db = TestDb.For(store, name);
            var sender = new ReminderSender(db, mail, NullLogger<ReminderSender>.Instance);
            // flyt "nu" frem forbi retry-forsinkelsen hver gang
            await sender.RunAsync(now.AddHours(i));
        }

        Assert.Empty(mail.Sent);
        await using (var db = TestDb.For(store, name))
        {
            var r = await db.Reminders.SingleAsync();
            Assert.Equal(ReminderStatus.Failed, r.Status);
            Assert.Equal(ReminderSender.MaxAttempts, r.Attempts);
        }
    }

    [Fact]
    public async Task Sender_only_touches_its_own_store_rows_but_across_all_stores()
    {
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var name = TestDb.NewName();

        await using (var db = TestDb.For(storeA, name))
        {
            db.Reminders.Add(DueReminder(storeA, "a@x.dk"));
            await db.SaveChangesAsync();
        }
        await using (var db = TestDb.For(storeB, name))
        {
            db.Reminders.Add(DueReminder(storeB, "b@x.dk"));
            await db.SaveChangesAsync();
        }

        var mail = new FakeEmail();
        await using (var db = TestDb.For(Guid.NewGuid(), name)) // baggrundsjob: ingen butik
        {
            var sender = new ReminderSender(db, mail, NullLogger<ReminderSender>.Instance);
            var n = await sender.RunAsync(new DateTimeOffset(2026, 9, 7, 6, 0, 0, TimeSpan.Zero));
            Assert.Equal(2, n); // begge butikker
        }
        Assert.Equal(2, mail.Sent.Count);
    }
}
