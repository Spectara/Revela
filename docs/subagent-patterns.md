# Parallel Subagent Patterns for Revela

How to use AI subagents to keep the main conversation lean, investigate bounded questions, implement isolated slices, and audit large parts of the codebase when separate context is useful.

> **Audience:** AI agents working on Revela (Copilot, Claude, etc.) and humans configuring agent workflows.

## Why Subagents?

Substantial independent questions can benefit from separate context. Small
lookups, known-file edits, bounded reviews and one continuous behavior trace
usually belong in the current conversation; do not delegate them merely because
a specialist exists. Use the current role directly when it already owns the
requested work. Explicit named-agent skills must not recursively dispatch the
same active agent.

**Subagents help** by:
- Running in their own context window
- Returning only the **structured result** (not all the searches it took to get there)
- Allowing independent research to run in parallel when the host supports it
- Isolating conversational context, not files, build outputs, browser state, or other shared resources

## Reasoned Delegation

The parent owns product decisions, architecture, integration, and final verification. The Worker owns one bounded implementation, including reversible local details. Explain the goal and trade-offs instead of prescribing every line. A Worker should challenge an assumption with evidence, not obey a decision that contradicts the code.

Keep the model configured in each Scout/Worker agent definition fixed during an
experiment. Those definitions own the exact model names; shared instructions
refer here rather than copying versions. Do not silently override the model,
retry with a different model, or count a parent repair as a Worker success.
Before an authorized model change, confirm its exact identifier and availability
in the target runtime, then run a bounded assignment and report the observed
model (or that it is unknown). Results from different models are not the same
experiment. Configuration alone proves neither availability nor general
reliability. Revisit the approach if preparation and repair repeatedly cost more
than direct implementation.

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
| `Allowed Scope` | Absolute repository, exact editable files, required instruction paths, nearby references, execution cwd, reversible freedoms and ownership of mutable resources. |
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
Restricted agents list their tools. Keep required host-specific capabilities
and add the portable aliases/CLI tool names they actually need. Unknown names
may be ignored; file discovery alone does not establish effective tool grants.
Earlier CLI checks found that `search`/`web` alone did not expose all local/web
search tools. Treat that as historical evidence, not a guarantee about a new runtime.

| Capability | Add to `tools:` |
|------------|-----------------|
| Local text/file search | `grep`, `glob` |
| File reads | `read` |
| Web access | `web_fetch`, `web_search` |
| File edits | `edit` |
| Shell commands | `execute` |
| Delegation | `agent` |

General shell execution is not a read-only security boundary. A reviewer that
needs it must still restrict itself to authorized nonmutating checks; build/test
commands can write shared outputs even when no source file is edited.

After changing tool permissions, use a fresh session in each supported host to
inspect the grants and perform one harmless task requiring the changed
capability. For example, from the repository directory:

```pwsh
copilot --no-auto-update --no-ask-user --agent <file-name-without-.agent.md> -p "Do not edit files or run commands. Read AGENTS.md with a read tool. Return its first heading and a JSON array of the tools you can call." -s
```

Record the CLI/host version, observed tool use, unavailable capabilities and
configured/observed model. A model's self-reported tool list alone is not proof
that every listed tool works. Do not open the user's pages or invoke private
services to probe capabilities.

For pinned-model agents, compare the actual model in runtime events with the
configured identifier. Some runtimes warn about an unavailable model, substitute
another and still return exit code zero. Treat that as a failed binding check,
not a successful experiment; a model's self-report is not sufficient evidence.
Use identifiers from the target runtime's available-model catalog.

For an external fixture, grant access only to its task-owned directory (CLI
`--add-dir`) and the required file-write/acceptance-command permissions before
dispatch. A denied operation is a blocked check: the Worker stops and the parent
repairs the grant, rather than trying alternate tools or broader path discovery.

### Repository Instructions for Subagents

A custom agent selected with `--agent` is the session agent, not a delegated
subagent. In Copilot CLI, session agents receive repository instructions by
default; delegated custom agents require `include-custom-instructions: true`.
Revela profiles opt in because their work depends on repository conventions.
See the [CLI instruction contract](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-custom-agents-for-cli#applying-your-repositorys-custom-instructions-to-a-custom-agent).

The editor's Copilot SDK conversion does not forward this field. Every profile
therefore also requires explicit reads of the target's `AGENTS.md`,
`.github/copilot-instructions.md` and applicable scoped instructions when present.
The parent supplies the target repository, execution cwd and required instruction
paths in the assignment; it does not assume its own context was inherited.
For another site repository, read that site's instructions instead of applying
Revela's .NET gates. If required instructions are inaccessible, return a blocker.
Opt-in configuration and successful discovery alone do not prove delivery or use.

## Harness Compatibility

Select the harness before interpreting configuration. The VS Code **Local**
harness is not the same runtime as the **Copilot** SDK harness, even when both
run on the same machine. A CLI update does not establish which SDK runtime the
editor uses. See the [harness comparison](https://code.visualstudio.com/docs/agents/run/agent-harnesses)
and [agent field reference](https://code.visualstudio.com/docs/agents/reference/custom-agent-file-reference).

| Surface | Local harness | Copilot SDK / CLI | Revela rule |
|---------|---------------|-------------------|-------------|
| Instructions | Editor discovery/settings apply. | Session agents load applicable sources; delegated custom agents opt in. SDK conversion can omit the opt-in field. | Read the target's baseline and scoped instructions explicitly; do not assume inherited context or an implicit precedence rule. |
| Tools | VS Code tool names and supported aliases. | Runtime tool names and supported aliases. | Keep required host capabilities plus portable aliases; verify actual calls, not just discovery. |
| Handoffs | `handoffs` provides suggested UI transitions. | Local handoff behavior is not a portable runtime contract. | Keep approval-oriented handoffs, but allow explicit role selection/delegation without those buttons. |
| Subagent follow-ups | Invocations are stateless. | Some hosts expose retained agents and messaging. | Supply complete context to stateless calls; use follow-ups only when the actual API supports them. |
| Models | Subagent selection has its own preference order. | `model` supports ordered fallback; `reasoning-effort` depends on the selected model. | Preserve explicit experiment pins; do not introduce silent fallback or effort overrides. |
| Forked skills | VS Code documents `context: fork` with `github.copilot.chat.skillTool.enabled`. | Skill discovery does not prove fork execution. | Verify a separate context and its result in each target host before claiming isolation. |
| Hooks | Separate Local schema and settings. | Shared Copilot hook schema; plugin-agent conversion can omit scoped hooks. | Do not copy hook payloads between harnesses or broaden hook scope as a workaround. |

`target: github-copilot` selects the Copilot schema for editor interpretation;
`target: vscode` selects the Local schema. Neither field switches the executing
harness, and model choice does not switch it either. The [instruction guide](https://code.visualstudio.com/docs/agent-customization/custom-instructions),
[forked-skill guide](https://code.visualstudio.com/docs/agent-customization/agent-skills#_run-a-skill-in-a-forked-context)
and [hook guide](https://code.visualstudio.com/docs/agent-customization/hooks)
describe host-specific controls and limitations.

### Harmless Runtime Checks

- Record the editor/CLI version and selected harness. Confirm that a known
  read-only task actually uses the expected file/search tools.
- Test actual delegation separately from `--agent` selection. Have the parent
  invoke exactly one named custom agent for a bounded read-only task, with the
  target repository, required instruction paths and explicit forbidden effects.
  Observe the delegation call, child start/completion, child tool reads and
  returned result. For a pinned agent, verify the child's model from runtime
  events, not the parent's model or a self-report. The child must read the target's
  baseline and applicable scoped instructions before inspecting its assigned code.
  Do not manually copy their contents into the test prompt or count a parent's
  own reads/result as child success. Repeat after relevant runtime updates.
- Check Local handoff buttons in a fresh Local session; their absence in another
  harness is not evidence that the underlying agent is unavailable.
- Test skill isolation with a task-owned fixture skill containing `context: fork`
  and only a harmless read/result task. Observe the dedicated skill/subagent
  execution and returned result. Reading its frontmatter or manually delegating
  the task is not evidence that the skill forked.
- If the skill content is delivered to the main conversation and its read task
  runs there, report inline execution, not a successful fork. The current agent
  retains resource ownership and verification; `context: fork` is neither a
  permission boundary nor an excuse to simulate isolation with manual delegation.
- Never invoke the release workflows merely to test isolation. Do not toggle
  user settings automatically, and remove task-owned fixture files afterwards.
- Report CLI-only results as CLI-only. If the editor's harness/settings or
  execution trace cannot be inspected, leave those checks explicitly unverified.

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
- [`.github/skills/full-review/SKILL.md`](../.github/skills/full-review/SKILL.md) — Explicit-invocation full review workflow
