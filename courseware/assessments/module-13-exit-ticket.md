# Module 13 Exit Ticket: AI Across the Delivery Lifecycle with GitHub

*Scope: AI across the lifecycle · the GitHub MCP server · issues and acceptance criteria · Copilot code review on GitHub · issue to pull request · agents in GitHub Actions · GitHub Advanced Security gates · cost visibility.  ~5 minutes · Not graded; this is for you and your instructor.*

You've just taken AI out of the IDE and into your team's whole delivery process: issues, reviews,
workflows, and security gates. Take five minutes to note what's clear and what still feels slippery;
there are no wrong answers here.

## Quick Recap

1. Your team wants every pull request into `main` to get a Copilot code review automatically, including
   a fresh review after each new push. Which setup does that?
   - a) Commit `.github/copilot-instructions.md` to `main`; Copilot reviews every pull request in a
     repository that has instructions
   - b) Add a branch ruleset that targets `main` with the **Automatically request Copilot code review**
     rule, and select its **Review new pushes** option
   - c) Turn on **Copilot can approve pull requests**, so Copilot's review counts as the required
     approval on `main`
   - d) Assign each issue to the Copilot cloud agent, so its pull requests come with a review

2. Your workflow posts an AI summary to every pull request. In one or two sentences: why must the step
   that runs the agent and the step that posts the comment be separate, and which credential does each
   one get?

3. In your own words, explain why "make the assistant ask its questions before it writes the issue"
   matters more when an agent will implement the issue than when a person will.

## Muddiest Point

What's the one thing from this module that's still fuzzy?

## Connect It

Think about how your own team delivers changes today. Pick **one** lifecycle stage (issue refinement,
review, build, release, or operations) where you'd add AI first, and say what output it would produce,
who would check it, and what you'd need from your GitHub organization admins to try it (a Copilot
policy, the GitHub MCP server, a ruleset, an Actions secret, a Code Security license). If you'd rather,
use the Bookstore workflow from Activity 23.

<details>
<summary>Instructor Answer Key</summary>

1. **b)** A branch **ruleset** with **Automatically request Copilot code review** makes the review
   automatic, and **Review new pushes** makes Copilot review again after each push (without it, Copilot
   reviews once). **a)** confuses steering with enabling: instruction files shape what Copilot looks for
   (read from the pull request's **head branch**), but they don't request a review. **c)** is the
   approval public preview, which is off by default and should stay off: Copilot comments, and a person
   approves the merge. **d)** is a different feature (issue to pull request), and a cloud agent's pull
   request still needs its own review.
2. Listen for the security idea: the agent reads **untrusted pull request content** (title, body, diff)
   that can try to steer it (prompt injection), so it must not hold anything that can act on the
   repository. The agent step gets only the **AI credential** (`ANTHROPIC_API_KEY`, or for Copilot CLI
   the run's token with `copilot-requests: write`, which can only make Copilot requests); the posting
   step gets **`GH_TOKEN` with `pull-requests: write`** and runs a script, not a model, checked out from
   the **base** commit so a pull request can't change the code that holds the write token. Bonus: each
   secret is mapped with `env:` into one step, PR text reaches scripts through `env:` rather than
   `${{ }}` inside `run:` (script injection), checkout uses `persist-credentials: false`, and fork pull
   requests get no secrets at all. The strongest answers name the limit: on a pull request from a branch
   in the same repository, the PR's own workflow runs with the secrets, so anyone who can push a branch
   is trusted with them (`CODEOWNERS` gates the merge, not the run), and environment-protected secrets
   with required reviewers are the stronger boundary (on private repositories, an Enterprise Cloud
   feature).
3. Listen for: an agent **implements the words on the page** and fills every gap with a plausible guess,
   then **writes tests that enshrine its guesses**, so an undecided question ("does case matter?", "400
   or 409?", "is today a pre-order?") becomes shipped behavior with green tests. A person would ask; the
   agent usually doesn't. Strong answers mention that the **product owner**, not the model, answers the
   questions, and that concrete **Given/When/Then** criteria map one-to-one to tests.

**Reading the signals:** Most of this module is generally available, so expect Muddiest Points about the
few things that aren't, or are plan-gated ("can Copilot approve?", "do we get agentic autofix?", "what's
Agentic Workflows?"), and about the GitHub MCP server's toolsets and tokens ("why does `get_me` fail?").
A two-minute recap of the "mature or emerging" table from chapter 01 usually settles the first; chapter
02's note that naming toolsets replaces the defaults settles the second. Question 1 is the best check
that *enabling* (a ruleset) versus *steering* (instruction files) landed; learners who pick **a)** will
wait for reviews that never come. Question 2 shows whether the workflow security model stuck: answers
like "so it's tidier" or "for performance" mean the prompt-injection point didn't land, so revisit
chapter 06's security section. For Connect It, listen for a sensible first stage (read-only, easy to
verify, reversible: PR summaries, issue refinement through MCP) with a named human checker and a
realistic admin ask. Learners who start with "an agent that merges pull requests" are worth a
conversation about accountability before Module 14.
</details>
