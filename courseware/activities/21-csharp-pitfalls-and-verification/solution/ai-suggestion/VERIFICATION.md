# Verification: supplier import suggestions

Record what you checked before acting on [SUGGESTION.md](SUGGESTION.md), and what you decided. Keep
the evidence: the command you ran and what it printed, or the page you looked at and what it said.

Checked 2026-10-16 against nuget.org. The compile check ran in a throwaway `net10.0` project outside
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
| Does the package exist? | `dotnet package search CsvHelper --exact-match --source https://api.nuget.org/v3/index.json` | **Yes.** Exact match lists the full version history up to 33.1.0. |
| Who publishes it? Downloads, repository, latest release date? | `dotnet package search CsvHelper --take 1` and the nuget.org page | Owner `joshclose`; about 700 million total downloads; source at `github.com/JoshClose/CsvHelper`; license `MS-PL OR Apache-2.0`; latest stable 33.1.0, published 2025-06-02. A reputable, widely used package. |
| Do the APIs the suggestion uses exist in that version? | Throwaway project with CsvHelper 33.1.0, the suggestion's code pasted in, `dotnet build` | **No.** `CS1503: Argument 1: cannot convert from 'System.IO.StreamReader' to 'CsvHelper.IParser'` (the constructor now needs a `CultureInfo` or a configuration), and two `CS0200` errors: `IReaderConfiguration.HasHeaderRecord` and `IParserConfiguration.Delimiter` are read only. The suggestion is written for an old major version. |
| Is it allowed by the team's package policy (`TASK.md`)? | Read the package policy | Not without the tech lead's sign-off: it isn't a `Microsoft.*` package. Policy says to record the recommendation and leave it out of this change. |
| Do we need it at all? | Read the format in `TASK.md` and `samples/supplier-prices.csv` | No. Two columns, no quoted fields, no embedded commas or line breaks. Splitting each line and parsing the price with `CultureInfo.InvariantCulture` covers it. |

**Verdict:** **Rejected for this change.** The package is real and reputable, but the suggested code
doesn't compile against the current version, the policy needs a sign-off we don't have, and the format
doesn't need it.

**What we did instead (if rejected):** `SupplierPriceList.Parse` in `Bookstore.Domain` parses the file
with the BCL (`decimal.TryParse` with `NumberStyles.AllowDecimalPoint` and the invariant culture),
validates every row, and reports bad rows by line number. If a future supplier format adds quoting,
revisit CsvHelper with the tech lead, using its current API:

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    PrepareHeaderForMatch = args => args.Header.ToUpperInvariant(), // "isbn" matches Isbn
};
using var csv = new CsvReader(reader, config);
```
