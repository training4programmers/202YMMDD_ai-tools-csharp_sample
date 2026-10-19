# Task: Verify, Then Gate

An AI assistant added a supplier price import to the Bookstore API, and suggested two follow-ups.
Nobody has reviewed any of it yet. The build is green, which tells you very little.

## The feature

`POST /api/books/prices/import` takes the supplier's weekly price list as `text/csv`:

```text
isbn,price
9781234560010,19.49
978-1-23456-005-8,42.00
123456002x,25.99
```

- The first line is a header.
- ISBNs arrive as ISBN-13 (with or without hyphens) or as ISBN-10 (whose last character may be `X`
  or `x`). The catalog stores ISBN-13s with hyphens, for example `978-1-23456-001-0`.
- Prices always use `.` as the decimal separator, whatever the server's culture.
- Every row must match a book in the catalog, and every price must be between 0.01 and 1000.00
  (the same range `BookRequest` allows).
- **All or nothing.** If any row is invalid, nothing is saved and the response is a `400` validation
  problem that lists every bad row by line number.
- On success, the response is `200` with the number of prices updated.

`samples/supplier-prices.csv` holds this week's list. Applied to the seeded catalog it should update
four books.

## Team package policy

- Packages need a reason. Prefer the BCL and code already in the solution when the job is small.
- Every new package gets a `VERIFICATION.md` entry: it exists, who publishes it, downloads, source
  repository, recent releases, license, and that the code we'd write against it compiles.
- Approved without further review: `Microsoft.*` packages published by Microsoft, and the test
  packages already in `Directory.Packages.props`. Anything else needs the tech lead's sign-off, so
  record your recommendation and leave the package out of this change.
- Versions live in `Directory.Packages.props`. Never add a version to a `.csproj`.

## Quality gates (the definition of done)

The solution must build with these on, with zero warnings:

- `TreatWarningsAsErrors`
- `AnalysisLevel` set to `latest-recommended`
- `EnforceCodeStyleInBuild`
- CA1849 (sync calls in async methods) and CA2000 (undisposed objects) turned on as warnings, because
  `latest-recommended` doesn't include them

and `dotnet format --verify-no-changes` must pass. Suppressions are allowed only with a written
justification, scoped as narrowly as possible.
