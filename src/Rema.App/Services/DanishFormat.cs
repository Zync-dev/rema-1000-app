namespace Rema.App.Services;

/// <summary>Danske dato- og tidsformater til visning.</summary>
public static class DanishFormat
{
    private static readonly string[] Months =
    [
        "januar", "februar", "marts", "april", "maj", "juni",
        "juli", "august", "september", "oktober", "november", "december",
    ];

    private static readonly string[] Weekdays =
        ["søndag", "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag"];

    /// <summary>Fx "3. september 2026".</summary>
    public static string ToDateString(this DateOnly d) => $"{d.Day}. {Months[d.Month - 1]} {d.Year}";

    /// <summary>Fx "onsdag 3. september".</summary>
    public static string ToWeekdayDate(this DateOnly d) =>
        $"{Weekdays[(int)d.DayOfWeek]} {d.Day}. {Months[d.Month - 1]}";

    /// <summary>Relativt: "I dag", "I går", "For 3 dage siden", ellers datoen.</summary>
    public static string ToRelativeDay(this DateOnly d)
    {
        var diff = DanishTime.Today.DayNumber - d.DayNumber;
        return diff switch
        {
            0 => "I dag",
            1 => "I går",
            > 1 and <= 7 => $"For {diff} dage siden",
            _ => d.ToWeekdayDate(),
        };
    }

    /// <summary>Fx "3. sep. kl. 07:30" i dansk tid.</summary>
    public static string ToDanishDateTime(this DateTimeOffset utc)
    {
        var local = TimeZoneInfo.ConvertTime(utc, DanishTime.Zone);
        return $"{local.Day}. {Months[local.Month - 1][..3]}. kl. {local:HH:mm}";
    }
}
