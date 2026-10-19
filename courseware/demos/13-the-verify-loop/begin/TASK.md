# Task: finish the half-done change, under a strict build

A teammate started three changes and was pulled onto something else:

- a CSS `hsl()` string on every color (`ColorMath.ToCssHsl` and `ColorDto.CssHsl`);
- a color search endpoint, `GET /api/colors/search?q=`, drafted with an AI assistant and not yet reviewed;
- a "tidy-up" of `ColorMath.TryParseHex` and `ColorMath.Rate`.

Right now three tests fail, and the build is lenient: `TreatWarningsAsErrors` is off in
`Directory.Build.props`, so warnings scroll past.

## Goal

1. In `Directory.Build.props`, turn on the team's standard settings:

   ```xml
   <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
   <AnalysisLevel>latest-recommended</AnalysisLevel>
   <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
   ```

2. Make all three gates pass:

   ```powershell
   dotnet build
   dotnet test --solution ToolsApp.slnx
   dotnet format ToolsApp.slnx --verify-no-changes
   ```

## Rules

- **Fix the code, not the tests.** Don't edit, skip, or delete a test, and don't change an expected value.
  If you believe a test is wrong, stop and say why.
- Don't switch an analyzer rule off for the whole solution to get to green. If a rule doesn't fit part of
  the code, scope the exception as narrowly as you can and explain it in a comment.
- For every fix, give the root cause in one sentence.
- `GET /api/colors/search` without a `q` value should return a 400 validation problem, not a 500.

## Done when

- [ ] The three settings are on, and `dotnet build` has no warnings or errors.
- [ ] `dotnet test --solution ToolsApp.slnx` passes, with every original test unchanged.
- [ ] `dotnet format ToolsApp.slnx --verify-no-changes` exits with code 0.
- [ ] Each fix comes with a one-sentence root cause.
