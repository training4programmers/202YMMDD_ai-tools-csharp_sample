# Module 07 Exit Ticket: Agentic Coding with Your Chosen Tools

*Scope: the agentic loop · your coding agent in depth · plan mode and the supervision spectrum · the verify loop · subagents, hooks, and skills · MCP servers · issue-to-PR automation.  ~5 minutes · Not graded; this is for you and your instructor.*

This module handed the keyboard to the agent and asked you to supervise instead. Take five minutes to note
what clicked and what still feels risky; there are no wrong answers here.

## Quick Recap

1. Your agent is fixing failing tests. It must never change an existing test, but it should still be able
   to add new test files. Which control enforces exactly that?
   - a) A sentence in `AGENTS.md` saying "don't change the tests"
   - b) A Claude Code deny rule, `"deny": ["Edit(/tests/**)"]`
   - c) A `PreToolUse` hook that blocks edits to files under `tests/` that already exist in git
   - d) Switching the session to auto mode so a classifier reviews each action

2. Name the four gates of the verify loop in the order you'd run them, and one MSBuild property that makes
   the build stricter for both people and agents.

3. In your own words, why is reviewing and editing an agent's **plan** a better use of your time than
   reviewing only the finished code? Give one example of a problem you could catch in a plan.

## Muddiest Point

What's the one thing from this module that's still fuzzy?

## Connect It

Think of a workflow your team repeats: a review checklist, the steps for adding an endpoint, a release
chore, or a lookup in an internal system. Would you package it as an instruction, a skill, a subagent, a
hook, or an MCP server, and why that one? If nothing comes to mind, use the Bookstore labs: which
`Bookstore.Mcp` tool would be most dangerous if it weren't read-only, and what stops it from writing today?

<details>
<summary>Instructor Answer Key</summary>

1. **c) A `PreToolUse` hook that blocks edits to existing test files.** A hook is deterministic, and it can
   apply a precise rule (in git already: block; new file: allow), exiting with code 2 so the agent sees
   the reason. a) is only advisory: it works most of the time, not always. b) is enforced but blunt: an
   `Edit(/tests/**)` deny rule also stops the agent from **adding** tests. d) changes who approves actions;
   it says nothing about test files. (Bonus point if someone notes that none of these stop a shell command,
   so `git diff --stat -- tests/` and CI remain the last check.)
2. **Build, analyzers, tests, format**: `dotnet build`, the Roslyn analyzers (which run as part of the
   build), `dotnet test --solution`, and `dotnet format --verify-no-changes`, cheapest first, stopping at the
   first red one. Accept any of `TreatWarningsAsErrors` (or `-warnaserror`), `AnalysisLevel` set to
   `latest-recommended`, `EnforceCodeStyleInBuild`, or `Nullable` enabled, ideally in
   `Directory.Build.props`.
3. Listen for **cost and leverage**: changing a plan costs a sentence, while changing an implementation
   costs a review, a revert, and another run; plan mode keeps the agent read-only until you approve. Good
   examples come from the demo and activity: `Enum.TryParse` accepting `"1"`, `Environment.NewLine` where a
   format needs LF, an unrequested package, logic in the wrong project, tests that miss the error cases, no
   protection when two orders race for the last copies. Strong answers also note that the plan is where
   **design** gets judged, and the diff is still reviewed afterwards; a plan doesn't replace the review.

**Reading the signals:** This module stacks a lot of new vocabulary: tool calls, permission modes,
checkpoints, hooks and exit codes, subagents, skills, MCP hosts and servers. Expect Muddiest Points on
**which mechanism does what** (skills versus subagents is the usual tangle; "a skill is a procedure the
agent loads, a subagent is a helper with its own context" clears most of it) and on **how much autonomy is
safe**. If many people pick b) in question 1, revisit advisory versus enforced controls and the
"new tests" catch. In Connect It, listen for a sensible match (a checklist as a skill, a must-always rule as
a hook, an internal system as an MCP server) and for least-privilege thinking about MCP tools: anyone who
mentions the read-only database connection, not just the `ReadOnly` attribute, has understood the
difference between a hint and enforcement. Learners who still describe the agent's summary as proof that
the work is done need a quick follow-up before Day 4's testing modules.
</details>
