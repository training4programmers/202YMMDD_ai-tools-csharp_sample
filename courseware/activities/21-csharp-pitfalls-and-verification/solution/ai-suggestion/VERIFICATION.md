# Verification: supplier import suggestions

Record what you checked before acting on [SUGGESTION.md](SUGGESTION.md), and what you decided. Keep
the evidence: the command you ran and what it printed, or the page you looked at and what it said.

Checked 2026-10-16 against nuget.org, and the CsvHelper rows re-checked on 2026-10-29 after CsvHelper
34.0.0 was published. Each compile check ran in a throwaway `net10.0` project or file-based app outside
the lab folder (package added, the suggestion's code pasted verbatim, `dotnet build`), deleted
afterward. No package was added to this solution. Search results change over time; re-run the checks
rather than trusting this file later.

## Suggestion 1: `Isbn13.Net`

| Check | How you checked | Result |
|-------|-----------------|--------|
| Does the package exist? | `dotnet package search Isbn13.Net --exact-match --source https://api.nuget.org/v3/index.json`; opened `https://www.nuget.org/packages/Isbn13.Net` | **No results found**, and the package page returns 404. A broader `dotnet package search isbn` lists only unrelated packages (barcode generators and a small ISBN parsing library with a different name). |
| Who publishes it? Downloads, repository, latest release date? | n/a | Nothing to check: the package doesn't exist. The namespace `Isbn13.Net`, the type `IsbnConverter`, and the method `ToIsbn13` in the suggestion are all invented. |
| Do the APIs the suggestion uses exist in that version? | n/a | No. There's no package for `IsbnConverter.ToIsbn13` to come from. |
| Is it allowed by the team's package policy (`TASK.md`)? | Read the package policy | No. It isn't a `Microsoft.*` package or an existing test package, so it would need the tech lead's sign-off even if it existed. |
| Do we need it at all? | Read `src/Bookstore.Domain/Isbn.cs` and the ISBN-10 to ISBN-13 rule | No. Converting is a fixed rule: drop the ISBN-10 check character, prefix `978`, and recompute the ISBN-13 check digit, which `Isbn.cs` already knows how to do. |

**Verdict:** **Rejected.** The package doesn't exist (a hallucinated name). "Not found" is final: an
unclaimed ID like this is exactly what slopsquatting targets, so we don't wait for it to appear.

**What we did instead (if rejected):** `Isbn.TryToIsbn13(string?, out string?)` in
`Bookstore.Domain`: validates an ISBN-10 (mod 11, final `X` or `x` allowed) or ISBN-13, and returns the
13 plain digits. Covered by `IsbnConversionTests`.

## Suggestion 2: `CsvHelper`

| Check | How you checked | Result |
|-------|-----------------|--------|
| Does the package exist? | `dotnet package search CsvHelper --exact-match --source https://api.nuget.org/v3/index.json` | **Yes.** Exact match lists the full version history: up to 34.0.0 on 2026-10-29 (up to 33.1.0 on 2026-10-16). |
| Who publishes it? Downloads, repository, latest release date? | `dotnet package search CsvHelper --take 1` and the nuget.org page | Owner `joshclose`; about 723 million total downloads; source at `github.com/JoshClose/CsvHelper`; license `MS-PL OR Apache-2.0`; latest stable 34.0.0, published 2026-10-28. On 2026-10-16 the latest was 33.1.0, published 2025-06-02. A reputable, widely used package. |
| Do the APIs the suggestion uses exist in that version? | File-based app with `#:package CsvHelper@34.0.0`, the suggestion's code pasted in, `dotnet build`; on 2026-10-16, a throwaway project with CsvHelper 33.1.0 | **No, in either version.** 34.0.0: `CS1729: 'CsvReader' does not contain a constructor that takes 1 arguments`, and two `CS1061` errors (`CsvReader` has no `Configuration`). Version 34 replaced the configuration API: readers come from `CsvReader.Create` with `CsvReaderOptions`, and the `CsvHelper.Configuration` namespace is gone. 33.1.0 (the first check, kept as history): `CS1503: Argument 1: cannot convert from 'System.IO.StreamReader' to 'CsvHelper.IParser'` (that constructor needed a `CultureInfo` or a configuration), and two `CS0200` errors: `IReaderConfiguration.HasHeaderRecord` and `IParserConfiguration.Delimiter` are read only. The suggestion is written for an old major version. `dotnet package list --file spike.cs --vulnerable --include-transitive` reports no vulnerable packages for 34.0.0. |
| Is it allowed by the team's package policy (`TASK.md`)? | Read the package policy | Not without the tech lead's sign-off: it isn't a `Microsoft.*` package. Policy says to record the recommendation and leave it out of this change. |
| Do we need it at all? | Read the format in `TASK.md` and `samples/supplier-prices.csv` | No. Two columns, no quoted fields, no embedded commas or line breaks. Splitting each line and parsing the price with `CultureInfo.InvariantCulture` covers it. |

**Verdict:** **Rejected for this change.** The package is real and reputable, but the suggested code
doesn't compile against the current version, the policy needs a sign-off we don't have, and the format
doesn't need it.

**What we did instead (if rejected):** `SupplierPriceList.Parse` in `Bookstore.Domain` parses the file
with the BCL (`decimal.TryParse` with `NumberStyles.AllowDecimalPoint` and the invariant culture),
validates every row, and reports bad rows by line number. If a future supplier format adds quoting,
revisit CsvHelper with the tech lead, using its current API. This compiles and runs against 34.0.0
(checked 2026-10-29, under a `de-DE` current culture); the `CsvConfiguration` and
`new CsvReader(reader, config)` form that 33.x used fails there with `CS0234`:

```csharp
var options = new CsvReaderOptions
{
    HasHeader = true,
    FormatProvider = CultureInfo.InvariantCulture, // prices parse the same in every culture
};
using var csv = CsvReader.Create(reader, options, leaveOpen: false);

foreach (var row in csv.GetRecords<SupplierPriceRow>())
{
    // ... validate row.Isbn and row.Price
}

[CsvRecord] // 34 maps records with its source generator, so the class must be partial
public sealed partial class SupplierPriceRow
{
    public string Isbn { get; set; } = "";
    public decimal Price { get; set; }
}
```
