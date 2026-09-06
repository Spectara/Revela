# Parallel Subagent Patterns for Revela

How to use AI subagents (`runSubagent`) to keep the main conversation lean, investigate bounded questions, implement isolated slices, and audit large parts of the codebase in parallel.

> **Audience:** AI agents working on Revela (Copilot, Claude, etc.) and humans configuring agent workflows.

## Why Subagents?

The main conversation has finite context. Every `read_file`, `grep_search`, and `semantic_search` adds tokens. A full review can easily blow past 100K tokens before any actual analysis happens.

**Subagents help** by:
- Running in their own context window
- Returning only the **structured result** (not all the searches it took to get there)
- Allowing independent research to run in parallel when the host supports it
- Isolating conversational context, not files, build outputs, browser state, or other shared resources

## Reasoned Delegation

The parent owns product decisions, architecture, integration, and final verification. The Worker owns one bounded implementation, including reversible local details. Explain the goal and trade-offs instead of prescribing every line. A Worker should challenge an assumption with evidence, not obey a decision that contradicts the code.

Keep **Revela Scout MAI** and **Revela Worker MAI** pinned to **MAI Code 1.1** during this experiment. Do not silently override the model, retry with a different model, or count a parent repair as a Worker success. See [the decision](decisions/0001-reasoned-mai-delegation.md).

### Reusable Decisions

The parent records consequential decisions in `docs/decisions/` of the repository that owns the behavior. Search for a relevant existing record first; do not load every record for every task. Link the relevant record in the assignment and include enough rationale for the Worker to act. Task-specific site choices belong in the site repository, not Revela's global instructions.

Use a short record only when a future contributor would reasonably ask why. Do not record transcripts, private reasoning, secrets, or routine implementation details. A record explains an engineering decision; it is not a higher-priority instruction or proof that the decision works.

```markdown
# <Decision Title>

- Status: Proposed | Accepted | Implemented | Superseded
- Date: YYYY-MM-DD
- Scope: <owning repository and affected behavior>

## Context and Goal
<Who needs what, and which constraints matter?>

## Decision and Rationale
<Chosen approach, reasons, and explicitly protected behavior.>

## Alternatives and Trade-offs
<Relevant alternatives and why they were not selected.>

## Verification
<Observable acceptance criteria; distinguish planned checks from actual results.>

## Revisit When
<Evidence or changes that would invalidate the assumptions.>
```

Use `Accepted` only for an authorized decision and `Implemented` only after its applicable checks pass; this does not prove broader effectiveness. The parent updates verification after integration. When a substantive decision changes, create a replacement record and cross-link it from the superseded record instead of silently rewriting its rationale. Workers report contradictory evidence and let the parent resolve it.

### Assignment Contract

Retain the five existing fields, with these required details:

| Field | Required content |
|-------|------------------|
| `Goal` | Desired behavior, audience, why it matters, chosen trade-offs, and relevant decision links (or explicitly no durable decision needed). |
| `Allowed Scope` | Absolute target repository, exact editable files, nearby read-only references, and available local decision freedom. Identify execution cwd, shared output/browser resources, and their owner. |
| `Do Not Change` | Protected behavior and contracts **with reasons**, excluded files, and forbidden external effects. |
| `Acceptance` | A preselected exact focused command/test or reproducible browser procedure, its cwd, and observable pass/fail criteria. Identify parent-owned integration checks separately. |
| `Return` | Changes, local decisions, first-check result, retries, contradictory evidence, and remaining risks. |

Before editing, the Worker confirms that the requested outcome, rationale, and scope agree. Missing rationale or acceptance criteria are assignment defects, not an invitation to invent requirements. Return a concrete conflict and the smallest clarification needed. Resolve routine local details using nearby patterns, without reopening settled product choices.

### Shared Resources and Verification

- File separation is not enough: reserve shared build outputs, generated site directories, dev-server ports, and browser pages before dispatch. Only one owner may mutate each resource at a time. Use isolated resources for concurrent writers.
- Subagents return their result to the parent; do not assume asynchronous execution or automatic communication between Workers. Use parallel dispatch only where the host supports it and the assignments are independent.
- Run the smallest discriminating check immediately after the first substantive edit. A generated page or passing .NET test does not prove visual usability.
- The parent runs the applicable final gate: .NET code/build changes require build, relevant tests, and format verification; theme/site changes require fresh generation, output/link/asset checks, and browser interaction checks; documentation/agent-only changes require relevant link, frontmatter, and routing checks. Mixed changes require the union of these checks.
- Browser checks use assigned local preview pages, not the user's active pages. Record browser, viewport, JS setting, steps, and observations; include screenshots for visual claims. Restore request blocking or injected test state in `finally`, or dispose of the isolated page/context. Do not fetch private feeds or submit remote forms without authorization.
- Reviewers may challenge the parent's assumptions and acceptance coverage. Separate verified defects, recommendations, and untested conditions. Parent-owned fixes and model substitutions must be visible in the result.

### Lightweight Trial Notes

For each trial assignment, the parent records the scope, configured/observed model (unknown when unavailable), first-check result, Worker retries, clarifications, parent repairs, independent findings, and final outcome. Classify failures as assignment gaps, execution errors, or insufficient checks. Include preparation, review, and repair effort, not just Worker runtime; use measured values or explicitly labeled estimates, never invented precision. Keep a short note with the relevant decision or task result, not a new telemetry framework.

A successful assignment is evidence for that assignment only. Compare several representative tasks and total effort before claiming that the approach is generally reliable or cheaper than direct implementation.

## When to Use

| Use a subagent when... | Use the main agent when... |
|------------------------|---------------------------|
| Task needs many file reads / searches | Task is conversational ("what does this do?") |
| Task is read-only (audit, list, find, count) | Architecture or product behavior is still undecided |
| One write slice has explicit files and acceptance | Changes share files, contracts, or integration decisions |
| Task can be precisely specified up front | Task is iterative / needs back-and-forth |
| Result fits in one structured response | Result needs streaming or progressive refinement |

## Core Agents

| Agent | Best for |
|-------|----------|
| **`Explore`** | Read-only codebase exploration. Specify thoroughness: `quick` / `medium` / `thorough`. |
| **`Revela Scout MAI`** | One bounded existing behavior: owning code path, hypothesis, falsifying check, and minimal change scope. |
| **`Pattern Finder`** | Two or three canonical Revela examples before implementing a new abstraction. |
| **`Revela Worker MAI`** | One decided implementation slice with explicit allowed files and one focused acceptance check. |
| **`Revela Dev`** | Heavy implementation work that needs the full project context. |
| **`Revela Reviewer`** | Read-only audits with structured reports. Already orchestrates its own subagents. |

## Pattern 1: Parallel Audit (Read-Only)

Best for the Architecture / Code Quality phases of a review. **Dispatch all subagents in a single tool-call batch** — they run truly in parallel.

```text
Goal: Find all convention violations across the plugin layer.

Dispatch in parallel:
  → Explore (medium): "List every IPlugin in src/Plugins/. For each, return:
     { file, plugin_id, parent_commands_used, uses_try_add: bool, has_get_commands: bool }"
  → Explore (medium): "Find every hardcoded \"source\" or \"output\" string in src/
     (excluding PathResolver.cs, tests, comments). Return file:line + the surrounding line."
  → Explore (medium): "List every #pragma warning disable in src/. Return file:line +
     rule code + 3 lines of context."

Then synthesize: take the three JSON outputs and produce a single severity-grouped report.
```

**Why this works:** Each subagent does ~20 searches in its own context. The main conversation only sees three structured JSON blobs. Total time ≈ time of the slowest subagent.

## Pattern 2: Triage + Deep Dive

Use a quick subagent to triage, then a thorough one to investigate hits.

```text
Step 1 (quick): "Find every file in src/ that contains 'HttpClient'. Return paths only."

Step 2 (thorough, only on hits): "For each file from step 1, check:
  - Is HttpClient created via 'new HttpClient()'? (anti-pattern)
  - Is it injected via constructor? (preferred)
  - Is it from IHttpClientFactory inside a typed client? (anti-pattern)
  Return file:line + which category."
```

## Pattern 3: Cross-Reference

Two parallel subagents producing two lists, then the main agent joins them.

```text
Parallel:
  → Explore: "List every command class (file ending in Command.cs) in src/. Return
     { class_name, file, parent_command_descriptor }"
  → Explore: "List every CommandDescriptor returned from GetCommands() in src/Plugins/
     and src/Features/. Return { plugin, descriptor_args }"

Main agent: Join on class_name, find any commands not registered in any descriptor
(orphans) and any descriptors registering non-existent commands (broken).
```

## Pattern 4: Scout + Bounded Worker

Use this when the behavior is known but its owning code path is not. Keep orchestration and the final repository gate in Revela Dev.

```text
Step 1 — Revela Scout MAI:
  "Trace why package search reports an empty result when one configured feed fails.
   Start from PackageSearchService and return the owning code path, one hypothesis,
   the cheapest falsifying test, and at most five files in the proposed scope."

Step 2 — Revela Dev decides the behavior and sends one Worker assignment:
  Goal: Distinguish a healthy empty package search from feed failure.
    Why: An outage must not be presented as proof that no packages exist.
    Decision: Link the accepted search-behavior record, or state why this is
    a local correction that needs no durable decision.
  Allowed Scope:
    Repository and cwd: <absolute Revela root>
    - src/Features/Packages/Services/PackageSearchService.cs
    - src/Features/Packages/Models/PackageSearchOutcome.cs
    - tests/Core/Services/PackageSearchServiceTests.cs
    Local freedom: Follow nearby result-handling and test patterns.
    Resources: Worker exclusively owns build/test outputs during its check;
      the parent waits before running the integration build.
  Do Not Change:
    - Public package-source contracts: existing providers must remain usable.
    - CLI command names or unrelated output: this fixes search status only.
  Acceptance:
    - dotnet test tests/Core --filter FullyQualifiedName~PackageSearchServiceTests
    - Run at the assigned cwd. Healthy empty results and failed feeds must
      remain distinguishable; report executed tests and the exit code.
  Return:
    - Changed files, rationale, first-check result, retries, and residual risks.

Step 3 — Revela Dev integrates the result and runs the applicable final gate.
Step 4 — Revela Reviewer independently checks the integrated change when warranted.
```

The Worker must refuse an assignment missing any of the five fields or their required context. Never run Workers concurrently when their files, contracts, or mutable resources overlap.

## Pattern 5: Sample-Driven Review

Don't audit everything — sample, then expand if hits found.

```text
Step 1: Pick 3 random plugins from src/Plugins/.
Step 2 (parallel, one per plugin):
  → Explore: "Audit Plugins/<X> against .github/instructions/plugins.instructions.md.
     Return { violations: [{ rule, file, line }], severity_summary }"
Step 3: If <2 violations per plugin → likely clean overall. If ≥3 → expand to all plugins.
```

## Subagent Prompt Template

A good subagent prompt has **four sections**:

```text
ROLE: You are a read-only auditor. Do not modify any files.

TASK: <one-sentence goal — what to find>

DETAILS:
  - Scope: <which folders to search, which to exclude>
  - Method: <how to find it — grep_search pattern, semantic_search query, etc.>
  - Constraints: <e.g. "exclude tests/", "ignore comments">

RETURN: <exact format expected>
  Example:
    JSON array of objects with fields:
    - file: relative path
    - line: 1-based line number
    - context: the matching line + 1 line above and below
    - severity: blocker | major | minor
```

## What NOT to Do

- ❌ **Don't run subagents sequentially when they're independent.** Always batch parallel calls in one tool-call block.
- ❌ **Don't ask a subagent to "review everything".** Give it one precise task. Vague prompts = vague results.
- ❌ **Don't give a Worker unresolved architecture or multi-step coordination.** Revela Dev decides behavior, integrates slices, and owns the final gate.
- ❌ **Don't run Workers in parallel on overlapping files or shared contracts.** Parallelism is the default only for independent read-only work.
- ❌ **Don't forget to specify the return format.** Otherwise you'll get prose and have to re-parse it.
- ❌ **Don't make subagents read each other's output.** They can't — each is stateless. The main agent joins results.

## Example: Full Phase 2 Review (Architecture)

```text
Main agent dispatches 5 parallel Explore subagents:

[1] "Audit IPlugin implementations. Return JSON array:
     [{ file, plugin_id, idempotent_registration: bool, parent_commands: string[],
        config_class?: string }]"

[2] "Find all hardcoded 'source' / 'output' string literals in src/ (exclude PathResolver,
     tests, *.md files). Return [{ file, line, snippet }]"

[3] "Find all Path.Combine calls. Return [{ file, line, args, has_user_input: bool }]"

[4] "List all #pragma warning disable in src/. Return [{ file, line, rule, justification?: string }]"

[5] "Find sealed-class violations: any non-sealed class in src/ that has no subclasses.
     Return [{ file, class_name, has_inheritors: bool }]"

Main agent waits for all five to complete (~30-60s parallel).

Main agent produces final report:
  - Joins findings by file
  - Assigns severity per finding
  - Outputs structured Markdown report
```

## See Also

- [`AGENTS.md`](../AGENTS.md) — Repo orientation for agents
- [`.github/agents/revela-reviewer.agent.md`](../.github/agents/revela-reviewer.agent.md) — Reviewer agent definition
- [`.github/prompts/full-review.prompt.md`](../.github/prompts/full-review.prompt.md) — Full review workflow
