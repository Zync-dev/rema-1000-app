using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

public enum ReminderStatus
{
    /// <summary>Venter på at blive sendt.</summary>
    Scheduled = 0,
    Sent = 1,
    Failed = 2,
    Cancelled = 3,
}

/// <summary>
/// En påmindelse om noget der skal ske – fx "På mandag henter Karen 50 rundstykker".
/// Sendes som mail til den ansvarlige på det aftalte tidspunkt (minus et evt. varsel).
/// </summary>
public class Reminder : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Hvornår tingen skal ske (UTC).</summary>
    public DateTimeOffset DueAtUtc { get; set; }

    /// <summary>Send så mange minutter før <see cref="DueAtUtc"/>. 0 = præcis på tidspunktet.</summary>
    public int LeadMinutes { get; set; }

    /// <summary>Beregnet: <see cref="DueAtUtc"/> minus varsel. Dispatcheren sammenligner med denne.</summary>
    public DateTimeOffset SendAtUtc { get; set; }

    /// <summary>Modtager = en butiksbruger, hvis sat.</summary>
    public Guid? RecipientUserId { get; set; }

    /// <summary>Modtager = fri kontakt (bruges når <see cref="RecipientUserId"/> er null).</summary>
    [MaxLength(120)]
    public string? RecipientName { get; set; }

    [MaxLength(200)]
    public string? RecipientEmail { get; set; }

    public ReminderStatus Status { get; set; } = ReminderStatus.Scheduled;
    public DateTimeOffset? SentUtc { get; set; }

    /// <summary>Antal afsendelsesforsøg. Efter for mange fejl markeres påmindelsen som <see cref="ReminderStatus.Failed"/>.</summary>
    public int Attempts { get; set; }

    [MaxLength(400)]
    public string? Error { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
