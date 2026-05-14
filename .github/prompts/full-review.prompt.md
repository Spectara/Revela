---
agent: Revela Reviewer
description: "Run a multi-phase deep review of the Revela codebase (architecture → quality → security → performance)"
---

# Full Codebase Review

Execute a complete five-phase review using the **Revela Reviewer** persona (auto-routed via the `agent:` frontmatter).

## Phases (run in order — each phase blocks on the previous)

### Phase 1 — Inventory (5 min)
Establish baseline. Run these in parallel via terminal:
- `dotnet build` (warnings count, errors)
- `dotnet test --no-build` (pass/fail per project)
- `dotnet format --verify-no-changes` (style violations)
- `dotnet outdated` (outdated packages)
- `dotnet list package --vulnerable --include-transitive` (CVEs)
- LOC per module (count `*.cs` lines under `src/Sdk`, `src/Core`, `src/Plugins/*`, `src/Themes/*`)

**Gate:** if build is broken, STOP and report. Don't continue review on broken code.

### Phase 2 — Architecture
Dispatch parallel `Explore` subagents:
1. "List every `IPlugin` implementation in `src/Plugins/` — file path, plugin ID, parent commands used, whether `ConfigureServices` uses `TryAdd*`."
2. "Find every hardcoded `\"source\"` or `\"output\"` string literal in `src/` (excluding `PathResolver.cs` and tests). Return file:line + surrounding line."
3. "List every `Path.Combine(...)` call in `src/`. Return file:line + arguments. Flag any combining with method parameters or external input."
4. "Audit every `CommandDescriptor` in `src/Plugins/` and `src/Features/` — list non-default parameters (Order, Group, ParentCommand, etc.)."
5. "Find any `Singleton` service that injects a `Scoped` service (captive dependency). Return file:line."

### Phase 3 — Code Quality
Dispatch parallel `Explore` subagents:
1. "Find every log call using string interpolation (e.g. `logger.LogInformation($\"...\"`). Return file:line."
2. "Find every `Contains/Replace/IndexOf/StartsWith/EndsWith` call on a string that doesn't pass `StringComparison`. Return file:line."
3. "Find every `ToString()`, `Format()`, `Parse()` call without `CultureInfo.InvariantCulture`. Return file:line."
4. "Find every `#pragma warning disable` in `src/`. Return file:line + rule code + 3 lines of context."
5. "Find every private field starting with underscore (`_`) in `src/`. Return file:line."
6. "Find every `async` method that contains no `await` (fake-async). Return file:line."

### Phase 4 — Security (OWASP-aligned)
Manual + targeted searches:
- **Path traversal** — review all `Path.Combine` results (from Phase 2 #3)
- **Serve plugin** — verify directory traversal protection in static file serving
- **OneDrive plugin** — token storage location, expiry, refresh; SSRF check on shared link redirects
- **Logging** — search for any `Log*` calls that include URLs, tokens, or auth headers
- **Plugin loading** — assembly load context isolation (PackageLoader)
- **Scriban templates** — any raw HTML output bypassing auto-escape?
- **`HttpClient` config** — TLS pinning, cert validation defaults
- **Vulnerable packages** — from Phase 1 `--vulnerable` results

### Phase 5 — Performance
- **BenchmarkDotNet** — run `benchmarks/ImageProcessing.Benchmarks` if user requests, compare to baseline
- Search for sync-over-async (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) in `src/`
- Search for `File.ReadAllText`/`File.ReadAllBytes` in hot paths (consider streaming)
- LINQ multiple-enumeration (`.ToList()` then re-enumerate)
- NetVips disposal — every `Image.NewFromFile` / `Image.NewFromBuffer` should be in a `using`
- `FrozenDictionary`/`FrozenSet` opportunities (static readonly `Dictionary`/`HashSet` never mutated after init)

## Output Format

Single Markdown report with the structure defined in `revela-reviewer.agent.md`:
- Header (date, scope, build/test/format status)
- Findings grouped by severity (🔴 Blocker / 🟠 Major / 🟡 Minor / 🟢 Nitpick)
- Metrics table (LOC, coverage, status per module)
- Recommendations (immediate / sprint / backlog)
- Collapsed subagent raw outputs

## Hand-off

End the report with:
> **Next steps:** Switch to **Revela Dev** agent and reference this report to apply fixes. Recommended order: Blockers → Major → Minor.

## Hard Constraints

- **Read-only.** Reviewer never writes code.
- **Cite or it didn't happen.** Every finding has a file:line.
- **No noise.** Skip categories with no findings.
- **Realistic severity.** A typo isn't a blocker.
