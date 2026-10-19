# Verification: `ToolsApp.ColorSpaces.Extensions`

What we checked before acting on [SUGGESTION.md](SUGGESTION.md), and what we did instead. Recorded
2026-10-16; package search results change over time, so re-run the checks rather than trusting this
file later.

## Verdict

**Rejected.** The package doesn't exist. The feature it was meant to provide (HSL to RGB) is about 30
lines of math, now `ColorMath.FromHsl` and `ColorMath.TryParseHsl`, covered by tests.

## Checks

| Check | How | Result |
|-------|-----|--------|
| Does the package exist? | `dotnet package search ToolsApp.ColorSpaces.Extensions --exact-match --source https://api.nuget.org/v3/index.json` | **No results found.** No `ToolsApp.ColorSpaces` either, and `https://www.nuget.org/packages/ToolsApp.ColorSpaces.Extensions` returns 404. |
| Is it an internal package? | Looked for a `nuget.config` with a private feed | There isn't one; nuget.org is the only source. The name was built from our own namespace, a common hallucination pattern. |
| Do the APIs exist? | Searched for `HslColor.Parse` and `ToHexString()` | Not in the BCL or in this solution. `Convert.ToHexString(byte[])` exists, but it formats bytes, not colors. |
| Is the suggestion's example right? | Converted `hsl(38, 76%, 81%)` with the new `ColorMath.FromHsl` and checked the reverse with `ToHsl` | **No.** It's `#F3D8AA`, not the claimed `#F4D8A8` (which is `hsl(37.9, 77.6%, 80.8%)`). The example output was invented too. |
| Would the code have worked? | Read it against the importer | No. `line.Split(',')` splits `hsl(38, 76%, 81%)` into four fields, so `parts[1]` would be `hsl(38`. |

## Risk if we had run the command

`dotnet add package` would have failed with `NU1101: Unable to find package`. That's the safe outcome
*today*. Anyone can register an unclaimed package ID on nuget.org, and IDs that models invent are
exactly the ones an attacker would register ("slopsquatting"). The day someone publishes
`ToolsApp.ColorSpaces.Extensions`, the same command succeeds and runs their code in our build.

## What we did instead

- `ColorMath.FromHsl(Hsl)`: the standard HSL to RGB conversion. A scratch run confirmed that
  `FromHsl(ToHsl(rgb)) == rgb` for all 16,777,216 colors; the test suite checks a 140,608-color sample.
- `ColorMath.TryParseHsl(string?, out Hsl)`: CSS `hsl()` with commas or spaces, parsed with
  `CultureInfo.InvariantCulture` (tested under `de-DE`, where `7.5` would otherwise read as `75`).
- `PaletteCsv.Parse` splits each line at the **first** comma only, so `hsl()` values survive.

## If we ever do need a color library

A real one exists: `Colourful` (owner `tompazourek`, about 301,000 downloads, MIT license, source on
GitHub, last release December 2023, at the time of writing). Before adding it we'd still check the
owner, the repository, the release history, and its license against our policy, then pin the version
centrally in `Directory.Packages.props`, commit a lock file, and record the decision here.
