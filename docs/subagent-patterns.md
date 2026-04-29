# Parallel Subagent Patterns for Revela

How to use AI subagents (`runSubagent`) to keep the main conversation lean and audit large parts of the codebase in parallel.

> **Audience:** AI agents working on Revela (Copilot, Claude, etc.) and humans configuring agent workflows.

## Why Subagents?

The main conversation has finite context. Every `read_file`, `grep_search`, and `semantic_search` adds tokens. A full review can easily blow past 100K tokens before any actual analysis happens.

**Subagents fix this** by:
- Running in their own context window
- Returning only the **structured result** (not all the searches it took to get there)
- Running **in parallel** — three subagents finish in the time of one
- Being **stateless** — no risk of cross-contamination between tasks

## When to Use

| Use a subagent when... | Use the main agent when... |
|------------------------|---------------------------|
| Task needs many file reads / searches | Task is conversational ("what does this do?") |
| Task is read-only (audit, list, find, count) | Task involves writing code |
| Task can be precisely specified up front | Task is iterative / needs back-and-forth |
| Result fits in one structured response | Result needs streaming or progressive refinement |

## The Three Built-in Agents

| Agent | Best for |
|-------|----------|
| **`Explore`** | Read-only codebase exploration. Specify thoroughness: `quick` / `medium` / `thorough`. |
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

## Pattern 4: Sample-Driven Review

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
- ❌ **Don't use a subagent for code writing if multi-step coordination is needed.** Subagents are stateless — the main agent should do the writing.
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
