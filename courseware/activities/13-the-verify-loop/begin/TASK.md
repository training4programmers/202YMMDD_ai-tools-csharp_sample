# Task: land the half-finished migration

A teammate was migrating three things and didn't finish:

- **Coupons** moved from a `switch` to a lookup table in `PriceCalculator`.
- **ISBN handling** was "simplified" in `Isbn.Normalize`, and ISBN-13s must now start with 978 or 979.
- **A lookup endpoint**, `GET /api/books/by-isbn/{isbn}`, was drafted with an AI assistant and not reviewed.

Several tests fail, and the build is lenient (`TreatWarningsAsErrors` is off).

## Goal

1. Turn on the team's standard settings in `Directory.Build.props`:

   ```xml
   <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
   <AnalysisLevel>latest-recommended</AnalysisLevel>
   <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
   ```

2. Make all three gates pass:

   ```powershell
   dotnet build
   dotnet test --solution Bookstore.slnx
   dotnet format Bookstore.slnx --verify-no-changes
   ```

## Rules

- **Don't modify the tests.** No edits to existing test files, no skipped or deleted tests, no changed
  expected values. New tests in new files are welcome.
- Every fix comes with a one-sentence root cause.
- No solution-wide analyzer switch-offs. If a rule doesn't fit a specific piece of code, scope the
  exception as narrowly as possible and give the reason in the code.
- The public behavior of `PriceCalculator` doesn't change: it stays an injectable instance class.

## Done when

- [ ] The three settings are on and `dotnet build` reports no warnings or errors.
- [ ] `dotnet test --solution Bookstore.slnx` passes with the original tests untouched.
- [ ] `dotnet format Bookstore.slnx --verify-no-changes` exits with code 0.
- [ ] You can name the root cause behind each group of failures.
