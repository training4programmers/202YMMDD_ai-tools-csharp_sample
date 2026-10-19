namespace Bookstore.Api;

internal static class TimeProviderExtensions
{
    /// <summary>Today's date in UTC: the date the catalog uses for publication rules.</summary>
    public static DateOnly UtcToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
