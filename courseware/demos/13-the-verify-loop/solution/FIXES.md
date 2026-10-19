# Fixes: root causes (recorded outcome)

The agent's final summary from the recorded run, after the instructor's review. Your run will word it
differently and may find the problems in a different order.

## Settings

- `Directory.Build.props`: `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, and
  `EnforceCodeStyleInBuild` turned on.
- `.editorconfig`: CA1707 off for `tests/**.cs` only. Test names use underscores on purpose, and CA1707 is
  meant for public API names. Renaming the tests would have broken the "don't edit tests" rule.

## Analyzer findings

| Rule | Where | Root cause and fix |
|------|-------|--------------------|
| CA1305 | `ColorMath.ToCssHsl` | `string.Format` used the current culture for machine-readable CSS. Now `string.Create(CultureInfo.InvariantCulture, ...)`. |
| CS8602 | `ColorSearchEndpoints` | `q` is an optional query parameter and was dereferenced unchecked, so a request without `q` returned 500. Now a 400 validation problem (new test file `ColorSearchValidationTests.cs`). |
| CA2016 | `ColorSearchEndpoints` | The request's `CancellationToken` wasn't passed to `ToListAsync`. Now forwarded. |
| CA1304, CA1311, CA1862 | `ColorSearchEndpoints` | `ToLower()`/`ToUpper()` with `Contains` for a case-insensitive match: culture-dependent and allocating. Now `Contains(text, StringComparison.OrdinalIgnoreCase)`. |
| IDE0161 | `ColorSearchEndpoints` | Block-scoped namespace against the `.editorconfig` rule. Now file-scoped (also fixed the whitespace that failed `dotnet format`). |
| CA1707 | 13 test methods | See Settings: scoped off for test code. |

## Test failures

| Test | Root cause | Fix |
|------|------------|-----|
| `TryParseHex_reads_long_and_short_forms("#F0A")` | The stack-allocated expansion wrote each digit to positions `i` and `i + 3`, producing `F0AF0A` instead of `FF00AA`. | Write positions `2i` and `2i + 1`. |
| `Rate_maps_ratios_to_wcag_levels(4.5, AA)` | The new threshold table compared with `>`, but WCAG thresholds are minimums: 4.5:1 meets AA. | Compare with `>=`. |
| `ToCssHsl_rounds_to_whole_numbers("#00A6A6")` | `(int)` truncated 32.5% lightness to 32. | Round with `MidpointRounding.AwayFromZero`; a hue that rounds to 360 becomes 0. |

## Gates

```text
dotnet build                                      Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test --solution ToolsApp.slnx              total: 28, failed: 0, succeeded: 28
dotnet format ToolsApp.slnx --verify-no-changes   exit code 0
```

Existing tests are unchanged; the only test change is the new `ColorSearchValidationTests.cs`.
