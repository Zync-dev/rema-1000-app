using System.ComponentModel.DataAnnotations;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data.Entities;

/// <summary>Hvor tit en tjekliste dukker op.</summary>
public enum ChecklistRecurrence
{
    /// <summary>Kun én bestemt dag (<see cref="Checklist.Date"/>).</summary>
    Once = 0,

    /// <summary>Hver dag.</summary>
    Daily = 1,

    /// <summary>Mandag til fredag.</summary>
    Weekdays = 2,
}

/// <summary>
/// En tjekliste – en skabelon af opgaver der skal løses. Ud fra recurrence
/// materialiseres den til en <see cref="ChecklistDay"/> med afkrydsbare
/// <see cref="ChecklistTask"/>-linjer for hver relevant dato.
/// </summary>
public class Checklist : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ChecklistRecurrence Recurrence { get; set; } = ChecklistRecurrence.Daily;

    /// <summary>Kun brugt når <see cref="Recurrence"/> er <see cref="ChecklistRecurrence.Once"/>.</summary>
    public DateOnly? Date { get; set; }

    public bool IsArchived { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
    public ICollection<ChecklistDay> Days { get; set; } = new List<ChecklistDay>();

    /// <summary>Gælder tjeklisten den givne dato?</summary>
    public bool AppliesOn(DateOnly date) => Recurrence switch
    {
        ChecklistRecurrence.Once => Date == date,
        ChecklistRecurrence.Daily => true,
        ChecklistRecurrence.Weekdays => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        _ => false,
    };
}

/// <summary>En linje i tjekliste-skabelonen.</summary>
public class ChecklistItem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    public Guid ChecklistId { get; set; }
    public Checklist? Checklist { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int Position { get; set; }

    /// <summary>Fast ansvarlig for denne opgave. Null = "alle".</summary>
    public Guid? AssigneeUserId { get; set; }
}

/// <summary>Én konkret dags udgave af en tjekliste.</summary>
public class ChecklistDay : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    public Guid ChecklistId { get; set; }
    public Checklist? Checklist { get; set; }

    public DateOnly Date { get; set; }

    public ICollection<ChecklistTask> Tasks { get; set; } = new List<ChecklistTask>();
}

/// <summary>En afkrydsbar opgave på en bestemt dag.</summary>
public class ChecklistTask : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }

    public Guid ChecklistDayId { get; set; }
    public ChecklistDay? Day { get; set; }

    /// <summary>Titlen kopieres fra skabelonen, så senere redigering ikke ændrer historik.</summary>
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public Guid? AssigneeUserId { get; set; }

    public int Position { get; set; }

    public bool Done { get; set; }
    public Guid? DoneByUserId { get; set; }
    public DateTimeOffset? DoneUtc { get; set; }

    /// <summary>Skabelon-linjen opgaven kom fra. Null for opgaver tilføjet direkte til dagen.</summary>
    public Guid? SourceItemId { get; set; }
}
