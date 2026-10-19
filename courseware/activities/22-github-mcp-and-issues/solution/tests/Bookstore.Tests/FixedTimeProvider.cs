namespace Bookstore.Tests;

/// <summary>A clock that always reads the same instant, so date rules don't depend on when tests run.</summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
