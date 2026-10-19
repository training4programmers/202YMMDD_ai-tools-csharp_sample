# Module 12 Exit Ticket: C#-Specific Pitfalls and Verification

*Scope: hallucinated packages and API versions · stale framework knowledge · nullable and analyzer warnings as gates · EditorConfig, analyzers, and dotnet format · performance review.  ~5 minutes · Not graded; this is for you and your instructor.*

You've just watched a green build turn into eighteen specific errors, and turned a few gates on
yourself. Take five minutes to note what's clear and what isn't; there are no wrong answers here.

## Quick Recap

1. An assistant suggests `dotnet add package ToolsApp.ColorSpaces.Extensions`. You run
   `dotnet package search ToolsApp.ColorSpaces.Extensions --exact-match` and it prints
   `No results found.` What's the best next step?
   - a) Wait a few days and try again; new packages take time to index
   - b) Ask the assistant for a similarly named package and add that one instead
   - c) Reject the suggestion, record why, and decide whether the BCL or a properly verified package covers the need
   - d) Publish a package with that ID to your private feed so restore succeeds

2. Your team turns on `TreatWarningsAsErrors` and `<AnalysisLevel>latest-recommended</AnalysisLevel>`,
   and the build is clean. A colleague concludes that no async method calls `SaveChanges()` or
   `.Result` anymore. In a sentence or two: why might that be wrong, and what would you change?

3. In your own words, explain why `var palette = await db.Palettes.FirstOrDefaultAsync(...);` followed
   by `palette!.Colors` is a problem in an AI-generated diff even though the build is clean, and what
   you'd do instead.

## Muddiest Point

What's the one thing from this module that's still fuzzy?

## Connect It

Think about your own team's main solution. Which two or three gates from this module would you turn on
first (nullable as errors, an `AnalysisLevel`, `EnforceCodeStyleInBuild`, specific CA rules,
`dotnet format --verify-no-changes`, a package-approval rule), and what do you expect to break when you
do? If nothing comes to mind, use Activity 21: which suppression did you keep, and how did you justify
its scope?

<details>
<summary>Instructor Answer Key</summary>

1. **c)** "Not found" is final: models repeat invented names, and anyone can register an unclaimed ID
   on nuget.org (slopsquatting), so waiting **a)** is how you'd eventually install someone else's code.
   **b)** repeats the mistake; any alternative needs the same four checks (exists, trustworthy, API
   compiles against that version, allowed by policy). **d)** makes the build pass and hides the
   problem. Strong answers also ask the zeroth question: does this need a package at all? In Demo 21,
   the "package" was thirty lines of HSL math.
2. Listen for: **`latest-recommended` doesn't include `CA1849`** (call async methods when in an async
   method), so sync-over-async calls such as `SaveChanges()`, `ReadToEnd()`, and `.Result` in
   `Task`-returning methods aren't reported. The fix is to **opt in** with
   `dotnet_diagnostic.CA1849.severity = warning` in `.editorconfig` (Demo 21's solution found
   `app.Run()` after an `await` this way). Bonus if they mention that `CA2000` and `CA1851` are also
   opt-in, that `CA2007` is a policy decision, or that the threading analyzers package covers
   `async void` and blocking calls in synchronous code.
3. Listen for the idea, not the wording: `FirstOrDefaultAsync` **can return null**, the compiler warned
   (`CS8602`), and **`!` silences the warning without changing runtime behavior**, so an unknown id
   becomes a `NullReferenceException` and a **500 instead of a 404**. No gate catches it, because `!` is
   the instruction to stop checking. What to do instead: handle the null (`if (palette is null) return
   TypedResults.NotFound();`, or `is not { } palette`), add a test for the unknown id, make nullable
   warnings errors, tell the assistant never to use `!` to silence a warning, and search every AI diff
   for new `!`.

**Reading the signals:** Question 1 checks whether "verify before you add" has replaced "try it and
see"; anyone choosing **a)** or **d)** needs the slopsquatting point again, ideally with the
`dotnet package search` output on screen. Question 2 is the best indicator of whether learners
understand that analyzer levels are curated sets, not "everything"; vague answers ("turn on more
rules") suggest revisiting chapter 03's table. Question 3 separates people who treat a clean build as
done from people who review for what tools can't see; that habit matters for every remaining module.
Expect Muddiest Points about `AnalysisLevel` versus `AnalysisMode`, where to put a suppression, and
`ConfigureAwait`. For Connect It, listen for realistic expectations (a wave of nullable and culture
warnings in existing code, CA1707 in tests, a phased rollout per project) rather than "turn everything
on"; teams that plan to start with `WarningsAsErrors` for a short list are on a good path.
</details>
