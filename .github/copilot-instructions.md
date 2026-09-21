# GitHub Copilot — Revela

> **Detailed conventions live in scoped instruction files** under [`.github/instructions/`](./instructions/).
> They auto-apply via `applyTo:` globs. This file is the always-on baseline only — keep it short.

---

## Project Overview

**Revela** ([revela.website](https://revela.website)) is a **.NET 10 / C# 14** static site generator for photographers. It is a complete rewrite of the original Bash project [`Expose`](https://github.com/kirkone/Expose) — same output, modern internals.

| Aspect | Original (Bash) | Revela |
|--------|-----------------|--------|
| Templates | Mustache-Light (regex) | Scriban |
| Markdown | Perl | Markdig |
| Images | VIPS CLI | NetVips |
| EXIF | ExifTool | NetVips built-in |
| Plugins | ❌ | ✅ NuGet-based |

**Status:** Pre-release — **no users, no backward compatibility required.** Rename, restructure, refactor freely. No migration scripts, no deprecation warnings.

---

## Project Structure

```
src/
├── Sdk/              # Public abstractions for plugin/theme authors
├── Sdk.Generators/   # Roslyn source generators ([RevelaConfig], etc.)
├── Core/             # Shared kernel — services, package loading, configuration
├── Commands/         # Host-owned CLI commands (Config, Packages, Plugins, Restore)
├── Features/         # Always built-in (Generate, Theme, Projects) — NOT plugins
├── Plugins/          # External plugins (Calendar, Compress, Serve, Source/*, Statistics)
├── Themes/           # Lumina (base) + Lumina.Calendar, Lumina.Statistics (extensions)
├── Cli/              # Entry point — dynamic plugin loading (DiskPackageSource)
└── Cli.Embedded/     # Entry point — static plugin refs (EmbeddedPackageSource) ← F5 debug
tests/                # Mirrors src/ + Shared (fixtures)
samples/              # revela-website, showcase, onedrive, calendar
benchmarks/           # BenchmarkDotNet projects
docs/                 # Architecture, plugin dev, setup
scripts/              # build-release.ps1, test-release.ps1
```

**Two entry points, one bootstrap:** `Cli` and `Cli.Embedded` differ only in their `IPackageSource`. All shared setup lives in `src/Cli/Hosting/HostBootstrap.cs`.

---

## Custom Agents & Skills (use these!)

| Agent (`.github/agents/`) | When |
|---------------------------|------|
| **Revela Dev** | All implementation work — features, fixes, tests, refactoring |
| **Revela Reviewer** | Read-only audits — architecture, security, performance, conventions |
| **Revela Docs** | Website documentation — write/maintain product docs, persona paths (#86), migrate docs/ → website, keep docs in sync with code |
| **UX Advocate** | Read-only UX assessment for the assigned audience and browser verification of affected journeys on an assigned local preview; reports observed findings and untested conditions, without implementing fixes |
| **Spike Analyst** | New feature ideas — sharpens problem, weighs trade-offs, compares prior art, produces decision-ready spike report (read-only) |
| **Explore** | Fast read-only codebase exploration (subagent — call in parallel) |
| **Pattern Finder** | Subagent — finds 2-3 canonical examples to mirror; dispatched by Dev before implementing something new |
| **Revela Scout MAI** | Internal read-only subagent — finds the owning code path, one hypothesis, and one focused falsifying check for a bounded existing behavior |
| **Revela Worker MAI** | Internal implementation subagent — changes only an explicit allowed scope and runs one exact acceptance check; Dev owns integration and the final gate |
| **Convention Sentry** | Subagent — anti-pattern scanner (underscore fields, log interpolation, missing `StringComparison`, hardcoded paths) |
| **Plugin Auditor** | Subagent — single-plugin lifecycle/convention check; dispatch one per plugin in parallel |
| **Test Doctor** | Subagent — test-quality auditor (FluentAssertions/Moq leftovers, missing assertions, tautologies) |
| **Security Scout** | Subagent — OWASP scanner (secrets, vulnerable packages, path traversal, SSRF, weak crypto) |

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

| Prompt file | Invocation |
|-------------|------------|
| `/full-review` | Multi-phase deep review → routes to Revela Reviewer |
| `/new-plugin` | Scaffold a new plugin → routes to Revela Dev |
| `/new-theme` | Scaffold a new theme → routes to Revela Dev |
| `/release-notes` | Generate CHANGELOG entries from commits |

---

## Reasoned Delegation

Use the [reasoned delegation workflow](../docs/subagent-patterns.md#reasoned-delegation) and [assignment contract](../docs/subagent-patterns.md#assignment-contract) for scoped outcomes, reasons, protected behaviors, resource ownership, and acceptance. Workers own reversible local details and challenge contradicted assumptions with evidence; the parent owns decisions, integration, and final verification.

Keep **Revela Scout MAI** and **Revela Worker MAI** pinned to **MAI Code 1.1**, as explained by the [delegation rationale](../docs/subagent-patterns.md#reasoned-delegation). Configuration alone does not prove runtime availability or general reliability.

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
cd samples/showcase ; dotnet run --project ../../src/Cli -- generate all
```

**Task-scoped post-edit gates:**

- **.NET code/build changes:** `dotnet build` -> relevant `dotnet test` -> `dotnet format --verify-no-changes` remains mandatory.
- **Theme/site changes:** fresh generation with the target configuration, generated output/link/image/asset checks, and browser checks for affected journeys and viewports.
- **Documentation/agent-only changes:** local links, applicable YAML frontmatter, routing/tool availability, and instruction consistency; no unrelated .NET checks.
- **Mixed changes:** the union of applicable gates. Unavailable checks are reported as verification gaps, not passes.

The parent owns integration and the applicable final gates; a Worker runs its assigned focused check. See [Revela Dev](./agents/revela-dev.agent.md#post-edit-workflow-mandatory-gate) and the [verification workflow](../docs/subagent-patterns.md#shared-resources-and-verification) for details.

---

## Architecture Quick Reference

### Plugin lifecycle (4 phases)
1. **Discovery** — `IPackageSource.LoadPlugins()` (Disk or Embedded)
2. **`ConfigureConfiguration`** *(optional)* — usually no-op; ENV vars auto-loaded with `SPECTARA__REVELA__` prefix
3. **`ConfigureServices`** *(required)* — register services, options, HttpClients (use `TryAdd*` for idempotency)
4. **`GetCommands(IServiceProvider)`** *(optional)* — yield `CommandDescriptor` records

### Configuration chain (merged in order)
1. C# property defaults
2. `revela.json` (global, `%APPDATA%/Revela/`)
3. `project.json` (local, project root)
4. `logging.json` (optional, project root)
5. Environment variables (`SPECTARA__REVELA__*`)
6. CLI arguments

> **Note:** `site.json` is NOT loaded via `IConfiguration` — it's loaded dynamically by `RenderService`.

### Path resolution
- **Configurable paths** (`source`, `output`) → inject `IPathResolver`
- **Fixed paths** (`Cache`, `Themes`, `Plugins`, `SharedImages`, `Static`) → `ProjectPaths` constants

### HttpClient
Always **Typed Client pattern** — `services.AddHttpClient<MyService>()` then inject `HttpClient` directly. Never `new HttpClient()`, never `IHttpClientFactory` inside a typed client.

### Template context (Scriban)
- **Global** — `image_formats`, `site`, `basepath`, `assets_basepath`, `base_url`, `nav_items`
- **Per page** — `gallery`, `page_content`, `images`
- **Per image** — `sizes`, `placeholder`
- **Functions** — `find_image`, `page_url`, `absolute_url`, `asset_url`, `variant_url`, `format_date`, `format_filesize`, `markdown`

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
- Plugin development: [revela.website/docs/developers/plugin-development](https://revela.website/docs/developers/plugin-development/)
- HttpClient pattern: [revela.website/docs/developers/httpclient-pattern](https://revela.website/docs/developers/httpclient-pattern/)
- Subagent patterns: [`docs/subagent-patterns.md`](../docs/subagent-patterns.md)
- Project structure: [`docs/project-structure.md`](../docs/project-structure.md)
