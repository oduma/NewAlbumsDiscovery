using NewAlbumsDiscovery.Domain.AIDiscovery;

namespace NewAlbumsDiscovery.Domain.Tests.AIDiscovery;

public class TimeframeFormatterTests
{
    private readonly TimeframeFormatter _formatter = new();

    [Fact]
    public void Format_WithUtcInstant_ReturnsThirtyDayWindowEndingToday()
    {
        var asOf = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

        var result = _formatter.Format(asOf);

        Assert.Equal("between 14-JUL-2026 and 13-AUG-2026", result);
    }

    [Fact]
    public void Format_WithNonZeroOffset_UsesUtcDateNotLocalDate()
    {
        // 2026-08-13T23:30:00-05:00 is 2026-08-14T04:30:00 UTC, so the UTC date is the 14th.
        var asOf = new DateTimeOffset(2026, 8, 13, 23, 30, 0, TimeSpan.FromHours(-5));

        var result = _formatter.Format(asOf);

        Assert.Equal("between 15-JUL-2026 and 14-AUG-2026", result);
    }
}
