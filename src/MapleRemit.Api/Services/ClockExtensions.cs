namespace MapleRemit.Api.Services;

public static class ClockExtensions
{
    // Postgres keeps microseconds; truncate so replays match.
    public static DateTimeOffset GetUtcNowMicroseconds(this TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
