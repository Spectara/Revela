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

Keep **Revela Scout MAI** and **Revela Worker MAI** pinned to **MAI Code 1.1** during this experiment. Do not silently override the model, retry with a different model, or count a parent repair as a Worker success. The fixed model lets us evaluate reasoned assignments without changing that condition between trials; configuration alone proves neither runtime availability nor general reliability. Revisit the approach if preparation and repair repeatedly cost more than direct implementation.

### Reusable Decisions

The parent updates the owning repository's existing documentation when behavior or a meaningful constraint changes. Keep the rationale short and next to the relevant contract or procedure. Link that documentation in the assignment and include enough context for the Worker to act. Task-specific site choices belong in the site repository, not Revela's global instructions.

Do not create separate decision, idea or review documents or archive folders unless explicitly requested. Routine implementation details and trial results belong in the task or release result, with links to retained evidence where useful. Do not copy transcripts, private reasoning or secrets into documentation.

Distinguish proposed behavior from authorized changes and verified implementation.
Report checks and gaps after integration; documentation describes the current
contract, but is not proof that it passed verification or authority over current instructions.

### Assignment Contract

Every implementation assignment requires all five fields:

| Field           | Required content                                                                                                                     |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `Goal`          | Behavior, audience, rationale, trade-offs and links to existing relevant documentation, if applicable.                              |
| `Allowed Scope` | Absolute repository, exact editable files, nearby references, execution cwd, reversible freedoms and ownership of mutable resources. |
| `Do Not Change` | Protected behavior and contracts with reasons, excluded files and forbidden external effects.                                        |
| `Acceptance`    | Exact focused command or reproducible procedure, cwd and pass/fail criteria; parent integration checks listed separately.            |
| `Return`        | Changes, local decisions, first-check result, retries, contradictory evidence and remaining risks.                                   |

The Worker confirms scope and rationale before editing. Missing fields or an
unresolved product decision must be returned to the parent, not invented.

### Shared Resources and Verification

- Reserve build outputs, generated sites, server ports, terminals and browser
   pages. Never run concurrent mutations of a shared resource; isolated context
   does not mean isolated filesystem or processes.
- Run the smallest discriminating check immediately after the first substantive
   edit. Repair the same slice before expanding scope.
- The parent owns final gates: .NET changes require build, relevant tests and
   format verification; theme/site changes require fresh generation, output/link/
   asset checks and affected browser journeys; documentation/agent changes need
   links, applicable frontmatter and routing checks. Mixed changes require their union.
- Use owned local browser pages, observe actual settings, and restore interception
   and injected state or dispose the test context. Never alter the user's pages,
   fetch private feeds or submit remote forms without authorization.
- A reviewer can challenge both the implementation and the parent's assumptions
   or tests. Separate verified defects, recommendations and unavailable checks.
   Never present parent repairs or model substitutions as Worker success.

### Lightweight Trial Notes

Record configured/observed model (unknown if unavailable), first-check result,
retries, clarifications, parent repairs and independent findings briefly with the
task result. Distinguish assignment gaps from execution or acceptance defects.
Count preparation/review/repair effort, but invent no timing or cost measurements.
One successful assignment does not prove general reliability or efficiency.

### Tool Frontmatter

Agent `tools:` lists must work in both VS Code and Copilot CLI. An agent that
should use every available tool, such as Revela Dev, omits `tools:` entirely.
Restricted agents list their tools; unknown names are ignored, so keep the VS Code
names and add the CLI runtime names explicitly:
the aliases `search` and `web` did not grant `rg`/`glob` or `web_fetch`/`web_search`
in Copilot CLI 1.0.88, while `read`, `edit`, `execute` and `agent` did.

| Capability | Add to `tools:` |
|------------|-----------------|
| Local text/file search | `grep`, `glob` |
| Web access | `web_fetch`, `web_search` |
| File edits | `edit` |

After changing an agent, verify what the runtime actually grants instead of
trusting the configuration:

```pwsh
copilot -p "Output only a JSON array of the tools you can call." --agent <file-name-without-.agent.md> -s
```

## Bounded Workflow

1. Start with a concrete behavior and its nearest owning code. Use a Scout only
   if the owner or discriminating check remains unclear.
2. Settle behavior and architecture before assigning implementation. A Worker
   must return an incomplete assignment instead of inventing missing requirements.
3. Give each stateless agent the required context and expected result format.
   Parallelize only independent work supported by the host; serialize shared mutations.
4. Inspect the returned diff and evidence, resolve contradictions and run the
   parent-owned integration gate. A small successful sample is not proof that an
   unreviewed subsystem is correct.

## See Also

- [`AGENTS.md`](../AGENTS.md) — Repo orientation for agents
- [`.github/agents/revela-reviewer.agent.md`](../.github/agents/revela-reviewer.agent.md) — Reviewer agent definition
- [`.github/prompts/full-review.prompt.md`](../.github/prompts/full-review.prompt.md) — Full review workflow
