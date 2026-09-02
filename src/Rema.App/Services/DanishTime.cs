namespace Rema.App.Services;

/// <summary>
/// Butiks-tid = dansk tid (Europe/Copenhagen). Alle butikker er danske, så vi
/// behøver ikke tidszone pr. butik. Bruges til "i dag", forfaldstider mv.
/// </summary>
public static class DanishTime
{
    public static TimeZoneInfo Zone { get; } = Resolve();

    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>Konverterer en lokal dansk dato+tid til UTC.</summary>
    public static DateTimeOffset ToUtc(DateTime localDanish)
    {
        var unspecified = DateTime.SpecifyKind(localDanish, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Europe/Copenhagen", "Romance Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
