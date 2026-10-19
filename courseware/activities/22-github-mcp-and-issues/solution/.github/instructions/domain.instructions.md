---
applyTo: "src/Bookstore.Domain/**/*.cs"
---

# Bookstore domain rules

- The domain project has no framework dependencies: no EF Core, ASP.NET Core, or `TimeProvider`
  lookups. Methods that depend on the date take it as a `DateOnly` parameter.
- Dates without a time of day are `DateOnly`. Comparisons use the date the caller passes in, never
  the machine clock.
- Money is `decimal`. Round to cents with `Math.Round(amount, 2, MidpointRounding.AwayFromZero)`.
- Guard clauses use `ArgumentNullException.ThrowIfNull` and `ArgumentOutOfRangeException.ThrowIf...`.
- Public members have XML doc comments that state the business rule, including edge cases such as
  "published today is not a pre-order".
