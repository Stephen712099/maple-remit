using MapleRemit.Api.Services;
using Microsoft.Extensions.Time.Testing;

namespace MapleRemit.Tests.Services;

public class ClockExtensionsTests
{
    [Fact]
    public void Truncates_to_whole_microseconds()
    {
        var start = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start.AddTicks(1234567)); // 123456.7 microseconds

        Assert.Equal(start.AddTicks(1234560), clock.GetUtcNowMicroseconds());
    }
}
