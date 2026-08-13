using System.Globalization;

namespace NewAlbumsDiscovery.Domain.AIDiscovery;

/// <summary>
/// Formats the {{timeframe}} prompt placeholder (docs/requirements/FUNCTIONAL_REQUIREMENTS.md ->
/// Phase 7) as a rolling 30-day window ending "today", computed from the UTC instant supplied by
/// the caller - this type has no TimeProvider dependency of its own, keeping it a pure,
/// deterministically-testable Domain type.
/// </summary>
public sealed class TimeframeFormatter
{
    private const int LookbackDays = 30;

    public string Format(DateTimeOffset asOf)
    {
        var end = asOf.UtcDateTime.Date;
        var start = end.AddDays(-LookbackDays);
        return $"between {FormatDate(start)} and {FormatDate(end)}";
    }

    private static string FormatDate(DateTime date)
        => date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
}
