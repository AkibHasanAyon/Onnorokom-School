namespace SchoolAdmission.Application.Common.Time;

public static class TimeExtensions
{
    /// <summary>Drops sub-second precision and normalizes to UTC (offset 0). API timestamps are whole seconds.</summary>
    public static DateTimeOffset TruncateToSeconds(this DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }
}
