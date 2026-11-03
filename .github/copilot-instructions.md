# GitHub Copilot — Revela

> **Detailed conventions live in scoped instruction files** under [`.github/instructions/`](./instructions/).
> They auto-apply via `applyTo:` globs. This file is the always-on baseline only — keep it short.

---

## Project Overview

**Revela** ([revela.website](https://revela.website)) is a **.NET 10 / C# 14**
static site generator for photographers, using Scriban, Markdig, NetVips and
NuGet-based plugins.

**Status:** Pre-release — **no users, no backward compatibility required.** Rename, restructure, refactor freely. No migration scripts, no deprecation warnings.

---

## Project Structure

The [project structure](../docs/project-structure.md) owns the directory/build
map. Public contracts live in `src/Sdk`; host-only code lives in `src/Core`.
Built-in `src/Features` are not external `src/Plugins`.

**Two entry points, one bootstrap:** `Cli` uses `DiskPackageSource`;
`Cli.Embedded` uses static `EmbeddedPackageSource` references and is the F5/sample
host. Shared setup lives in `src/Cli/Hosting/HostBootstrap.cs`.

---

## Custom Agents & Skills (use these!)

| Agent (`.github/agents/`) | When |
|---------------------------|------|
| **Revela Dev** | All implementation work — features, fixes, tests, refactoring |
| **Revela Reviewer** | Read-only audits — architecture, security, performance, conventions |
| **Revela Docs** | Website documentation — write/maintain product docs, persona paths (#86), migrate docs/ → website, keep docs in sync with code |
| **UX Advocate** | Read-only UX assessment for the assigned audience and browser verification of affected journeys on an assigned local preview; reports observed findings and untested conditions, without implementing fixes |
| **Spike Analyst** | New feature ideas — sharpens problem, weighs trade-offs, compares prior art, produces decision-ready spike report (read-only) |
| **Explore** | Broader read-only research when a substantial independent question needs separate context |
| **Pattern Finder** | Finds 2-3 canonical examples when the applicable implementation pattern is unclear |
| **Revela Scout MAI** | Internal read-only subagent — finds the owning code path, one hypothesis, and one focused falsifying check for a bounded existing behavior |
| **Revela Worker MAI** | Internal implementation subagent — changes only an explicit allowed scope and runs one exact acceptance check; Dev owns integration and the final gate |
| **Convention Sentry** | Subagent — anti-pattern scanner (underscore fields, log interpolation, missing `StringComparison`, hardcoded paths) |
| **Plugin Auditor** | Bounded single-plugin lifecycle/convention check when specialist depth is useful |
| **Test Doctor** | Subagent — test-quality auditor (FluentAssertions/Moq leftovers, missing assertions, tautologies) |
| **Security Scout** | Subagent — OWASP scanner (secrets, vulnerable packages, path traversal, SSRF, weak crypto) |

These roles describe ownership, not a requirement to spawn an agent per task.
Handle small reviews, known-file edits and one continuous behavior trace directly.
Delegate only concrete work that needs substantial separate context; parallelize
only independent scopes. Explicit skills keep their named-agent routing, without
delegating back to an agent that is already active.

| Skill (`.github/skills/`) | Purpose |
|---------------------------|---------|
| `build-sample` | Build sample projects with local CLI |
| `build-release` | Local release build (`scripts/build-release.ps1`) |
| `commit-changes` | Conventional Commits — wait for explicit user request |
| `create-release` | Bump version, update CHANGELOG, tag |
| `review-code` | Per-file code review against conventions |
| `test-release` | End-to-end release pipeline test |

| Instructions file | Scope |
|-------------------|-------|
| [`csharp.instructions.md`](./instructions/csharp.instructions.md) | All `*.cs` — naming, async, logging, modern C# |
| [`tests.instructions.md`](./instructions/tests.instructions.md) | `tests/**/*.cs` — MSTest v4, NSubstitute, fixtures |
| [`plugins.instructions.md`](./instructions/plugins.instructions.md) | `src/Plugins/**` — plugin lifecycle, CommandDescriptor |
| [`themes.instructions.md`](./instructions/themes.instructions.md) | `src/Themes/**` — Scriban templates, manifest, partials |

| Explicit-invocation skill | Workflow |
|-------------|------------|
| [`/full-review`](./skills/full-review/SKILL.md) | Multi-phase deep review → explicitly delegates to Revela Reviewer |
| [`/new-plugin`](./skills/new-plugin/SKILL.md) | Scaffold a new plugin → explicitly delegates to Revela Dev |
| [`/new-theme`](./skills/new-theme/SKILL.md) | Scaffold a new theme → explicitly delegates to Revela Dev |
| [`/release-notes`](./skills/release-notes/SKILL.md) | Generate CHANGELOG entries from commits → explicitly delegates to Revela Dev |

These skills are invoked explicitly, not loaded automatically. Skills do not switch
personas through `agent:` frontmatter; each workflow requests the named custom agent.

---

## Reasoned Delegation

Use the [reasoned delegation workflow](../docs/subagent-patterns.md#reasoned-delegation) and [assignment contract](../docs/subagent-patterns.md#assignment-contract) for scoped outcomes, reasons, protected behaviors, resource ownership, and acceptance. Workers own reversible local details and challenge contradicted assumptions with evidence; the parent owns decisions, integration, and final verification.

Include the target repository, execution cwd and required instruction paths in
delegated assignments. Subagents read the target's baseline and scoped instructions
explicitly; parent context is not guaranteed to transfer. See
[instruction loading](../docs/subagent-patterns.md#repository-instructions-for-subagents).

Preserve the models configured in the Scout/Worker agent definitions; the
[delegation rationale](../docs/subagent-patterns.md#reasoned-delegation) defines
experiment attribution and model-change verification. Do not repeat model
versions in shared instructions or silently substitute another model.

---

## Hard Rules — Top 10 (most frequent agent mistakes)

These are enforced by `.editorconfig` (warnings → errors via `TreatWarningsAsErrors=true`). Full details in [`csharp.instructions.md`](./instructions/csharp.instructions.md).

1. **`var` everywhere** — never spell out the type.
2. **Private fields = `camelCase`** — NO underscore prefix.
3. **`StringComparison.Ordinal`** on every string method (except char overloads like `StartsWith('-')`).
4. **`CultureInfo.InvariantCulture`** on every formatting call.
5. **`LoggerMessage` source generator** — never `logger.LogInformation($"...")`.
6. **Never hardcode `"source"` / `"output"`** — inject `IPathResolver`.
7. **System.CommandLine 2.0 final API** — NOT beta. `new Option<T>("--name", "-n")`, `command.SetAction(...)`.
8. **No `ConfigureAwait(false)`** — CA2007 is suppressed (this is an app, not a library).
9. **Fix root cause, don't suppress** — convert `List<T>` → `IReadOnlyList<T>`, `string url` → `Uri?`, etc.
10. **`Markup.Escape(input)`** for Spectre output — never custom escaping.

---

## Git — Hard Rule

**Never run `git commit`, `git push`, `git tag`, `git reset --hard`, `git rebase`, `git merge`, or any other history-mutating git command without an explicit user request for that exact action.** This applies to all agents and all skills.

- "Save my work" / "make a commit" / "tag this" → explicit, OK to proceed (use the `commit-changes` skill).
- "Run the tests" / "fix this bug" / "format the code" → NOT a commit request. Stop after the work is done. Show what changed and wait.
- `git add`, `git status`, `git diff`, `git log` are read/staging-only and always allowed.

If unsure whether the user wants a commit, **ask** — don't commit speculatively.

---

## Build / Test / Format

```pwsh
dotnet build                                # full solution
dotnet test                                 # all tests
dotnet test tests/Core                      # one project
dotnet format                               # auto-fix style for .NET code/build changes
dotnet format --verify-no-changes           # required gate for .NET code/build changes

# Run CLI against a sample
Set-Location samples\showcase
dotnet run --project ..\..\src\Cli.Embedded -- generate all
```

**Task-scoped post-edit gates:**

- **.NET code/build changes:** `dotnet build` -> relevant `dotnet test` -> `dotnet format --verify-no-changes` remains mandatory.
- **Theme/site changes:** fresh generation with the target configuration, generated output/link/image/asset checks, and browser checks for affected journeys and viewports.
- **Documentation/agent-only changes:** local links, applicable YAML frontmatter, routing/tool availability, and instruction consistency; no unrelated .NET checks.
- **Mixed changes:** the union of applicable gates. Unavailable checks are reported as verification gaps, not passes.

The parent owns integration and the applicable final gates; a Worker runs its assigned focused check. See [Revela Dev](./agents/revela-dev.agent.md#post-edit-workflow-mandatory-gate) and the [verification workflow](../docs/subagent-patterns.md#shared-resources-and-verification) for details.

---

## Architecture Quick Reference

### Plugin contracts
The [scoped plugin instructions](./instructions/plugins.instructions.md) own
lifecycle, command registration, typed HttpClients, configuration binding and
artifact ownership. Plugins never touch `IConfiguration`: bind only their own
`[RevelaConfig]` section and persist it via `IPluginSettingsWriter<T>`.

### Configuration chain (merged in order, later wins)
1. C# property defaults
2. `revela.json` (global: next to the executable when writable, else `%APPDATA%/Revela/` / `~/.config/Revela/`)
3. `project.json` (local, project root)
4. `site.json` (local) — re-keyed under `site` by `AddSiteJson` and bound to `SiteCoreConfig`
5. `logging.json` (optional, project root)
6. Environment variables (`SPECTARA__REVELA__*`, also for logging and plugin/theme install)

Nothing else: no `appsettings*.json`, no unprefixed env vars, CLI options are never a configuration layer (commands apply their own overrides). No source watches its file — `ConfigFileWriter` reloads `IConfigurationRoot` after Revela writes a config file.

> **Note:** `site.json` has two consumers — the typed identity core via `IConfiguration` (`SiteCoreConfig`, section `site`), and `RenderService`, which reads the whole document dynamically for theme-specific properties.

### Path resolution
- **Configurable paths** (`source`, `output`) → inject `IPathResolver`
- **Fixed paths** (`Themes`, `Plugins`, `SharedImages`, `Static`) → `ProjectPaths` constants
- **Produced non-published files** → `ProjectPaths.GetOwnerDirectory(artifact.Owner)` and registered artifact ownership/lifetimes; see [plugin file ownership](./instructions/plugins.instructions.md#where-plugins-keep-files-and-how-clean-works)

### Template context (Scriban)
The [scoped theme instructions](./instructions/themes.instructions.md) own
template variables/functions, escaping, URL/image helpers, localization and
Native AOT restrictions. Page HTML is `gallery.body`, not `page_content`.

---

## Session Startup

Choose baseline checks for the task and target repository, not automatically for every conversation. Reuse verified results when their inputs have not changed:

- **Revela .NET code/build work:** establish relevant build/format baselines as needed.
- **Theme/site work:** identify the generator version, target configuration, owned output directory, and browser acceptance criteria.
- **Documentation/agent work or questions:** use relevant local checks; do not start .NET builds or dependency audits automatically.

Reserve mutable resources before execution; never run concurrent mutations of shared build outputs, generated directories, or browser pages. Dependency freshness/security checks belong to dependency work or an explicit audit; `dotnet outdated` alone is not a vulnerability scan.

Report issues concisely. Baselines do not replace post-edit gates. See [Revela Dev startup](./agents/revela-dev.agent.md#session-startup) and the [resource and verification workflow](../docs/subagent-patterns.md#shared-resources-and-verification).

---

## Documentation

- Architecture: [`docs/architecture.md`](../docs/architecture.md)
- Plugin development: [`docs/plugin-development.md`](../docs/plugin-development.md)
- HttpClient pattern: [`docs/plugin-development.md` — typed-client pattern](../docs/plugin-development.md#making-http-calls-the-typed-client-pattern)
- Subagent patterns: [`docs/subagent-patterns.md`](../docs/subagent-patterns.md)
- Project structure: [`docs/project-structure.md`](../docs/project-structure.md)
