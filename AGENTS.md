# AGENTS.md — Revela

Quick orientation for AI coding agents (GitHub Copilot, Claude, Cursor, etc.) working on this repo.

> **Primary source of truth:** [`.github/copilot-instructions.md`](.github/copilot-instructions.md)
> Read it first — it contains the complete project conventions, plugin architecture, and code style rules.

---

## Project at a Glance

- **Revela** — Static site generator for photographers, built on **.NET 10 / C# 14**.
- **Status:** Pre-release — **no backward compatibility required**. Rename, restructure, refactor freely.
- **Architecture:** Vertical Slice + Plugin System (NuGet-loaded via `IPackageSource`).
- **Two entry points:**
  - `src/Cli/` — production (dynamic plugin loading via `DiskPackageSource`)
  - `src/Cli.Embedded/` — debugging (static plugin references via `EmbeddedPackageSource`) ← **start here for F5 debug**

## Where Things Live

| Path | Contents |
|------|----------|
| `src/Sdk/` | Public abstractions for plugin/theme authors (`IPlugin`, `ITheme`, `IRevelaEngine`, `IPathResolver`) |
| `src/Core/` | Shared kernel — services, package loading, configuration |
| `src/Commands/` | Host-owned CLI commands (`Config`, `Packages`, `Plugins`, `Restore`) |
| `src/Features/` | **Always built-in features**: `Generate`, `Theme`, `Projects` (NOT plugin-loaded) |
| `src/Plugins/` | External plugins: `Compress`, `Serve`, `Statistics`, `Calendar`, `Source/OneDrive`, `Source/Calendar` |
| `src/Themes/` | Themes: `Lumina` (base), `Lumina.Calendar`, `Lumina.Statistics` (extensions) |
| `src/Sdk.Generators/` | Roslyn source generators (e.g. `[RevelaConfig]`) |
| `tests/` | Mirrors `src/` — `Core`, `Commands`, `Integration`, `Plugins/*`, `Shared` (fixtures) |
| `samples/` | `revela-website`, `showcase`, `onedrive`, `calendar` — runnable example projects |
| `docs/` | Architecture, plugin development, setup, getting started |
| `benchmarks/` | BenchmarkDotNet projects |
| `scripts/` | `build-standalone.ps1`, `test-release.ps1` |

## Custom Agents (use these!)

| Agent | When to invoke |
|-------|----------------|
| **Revela Dev** | Implementation work — features, bug fixes, commands, plugins, services, tests, refactoring. Knows all conventions. |
| **Revela Reviewer** | Architecture / security / performance reviews. Read-only audit with structured report. |
| **Explore** | Read-only codebase exploration when chaining many searches. Safe to call in parallel. |

## Skills (`.github/skills/`)

| Skill | Purpose |
|-------|---------|
| `build-sample` | Build sample projects (`revela-website`, `showcase`, `onedrive`) with local CLI |
| `build-standalone` | Local self-contained release build (`scripts/build-standalone.ps1`) |
| `commit-changes` | Conventional Commits — wait for explicit user request |
| `create-release` | Bump version, update `CHANGELOG.md`, tag |
| `refactor` | Surgical refactors without behavior change |
| `review-code` | Per-file code review against `.editorconfig` and conventions |
| `test-release` | End-to-end release pipeline test (`scripts/test-release.ps1`) |

## Instruction Files (`.github/instructions/`)

Apply automatically based on `applyTo:` glob:

| File | Scope |
|------|-------|
| `csharp.instructions.md` | All `.cs` files — naming, async, logging, modern C# |
| `tests.instructions.md` | `tests/**/*.cs` — MSTest v4, NSubstitute, fixtures |
| `plugins.instructions.md` | `src/Plugins/**` — plugin lifecycle, `CommandDescriptor` |
| `themes.instructions.md` | `src/Themes/**` — theme conventions, Scriban templates |

## Prompt Files (`.github/prompts/`)

Reusable workflows — invoke with `/` in chat:

| Prompt | Purpose |
|--------|---------|
| `new-plugin` | Scaffold a new plugin (project, csproj, plugin class, tests) |
| `new-theme` | Scaffold a new theme or theme extension |
| `full-review` | Multi-phase deep review (architecture → quality → security → performance) |
| `release-notes` | Generate `CHANGELOG.md` entries from commits |

## Build / Test / Run

```pwsh
dotnet build                                                # full solution
dotnet test                                                 # all tests
dotnet test tests/Core                                      # one project
dotnet format                                               # MUST pass before commit
dotnet format --verify-no-changes                           # CI gate

# Run CLI against a sample
cd samples/showcase ; dotnet run --project ../../src/Cli -- generate all
```

**Mandatory post-edit gate:** `dotnet build` → relevant `dotnet test` → `dotnet format --verify-no-changes`.

## Hard Rules (frequent agent mistakes)

1. **`var` everywhere** — never spell out the type.
2. **Private fields = `camelCase`** — NO underscore prefix.
3. **`StringComparison.Ordinal`** on every string method (except char overloads like `StartsWith('-')`).
4. **`CultureInfo.InvariantCulture`** on every formatting call.
5. **`LoggerMessage` source generator** — never `logger.LogInformation($"...")`.
6. **Never hardcode `"source"` / `"output"`** — inject `IPathResolver`.
7. **System.CommandLine 2.0 final API** — NOT beta. `new Option<T>("--name", "-n")`, `command.SetAction(...)`.
8. **No `ConfigureAwait(false)`** — CA2007 is suppressed (this is an app, not a lib).
9. **Fix root cause, don't suppress** — convert `List<T>` → `IReadOnlyList<T>`, `string url` → `Uri?`, etc.
10. **`Markup.Escape(input)`** for Spectre output — never custom escaping.

## Modern C# 14 / .NET 10 Cheat Sheet

Prefer when applicable (full list in [`csharp.instructions.md`](.github/instructions/csharp.instructions.md)):

| Use this | Instead of |
|----------|-----------|
| `field` keyword in property setter | manual private backing field |
| `extension` blocks | only static extension methods |
| `obj?.Prop = value` | `if (obj is not null) obj.Prop = value;` |
| `private Lock gate = new();` | `private readonly object gate = new();` |
| `Random.Shared` | `new Random()` |
| `TimeProvider` | `DateTime.UtcNow` (in testable code) |
| `FrozenDictionary` / `FrozenSet` | static `Dictionary` / `HashSet` |
| `SearchValues<T>` | repeated `IndexOfAny` |
| `Regex.EnumerateMatches` | `Regex.Matches` |
| `params Span<T>` / `params IEnumerable<T>` | `params T[]` |
| `ZipFile.ExtractToDirectoryAsync` | `ZipFile.ExtractToDirectory` (in async paths) |
| `JsonSerializerOptions.AllowDuplicateProperties = false` | implicit duplicate-tolerant parsing |

## Subagent Patterns

Use `runSubagent` to keep the main conversation lean and run audits in parallel:

```text
Phase 1 (parallel) — three explorers:
  → Explore: "Plugin lifecycle audit — list every IPlugin and verify ConfigureServices uses TryAdd*"
  → Explore: "Find every hardcoded 'source'/'output' path string in src/"
  → Explore: "List every HttpClient instantiation outside AddHttpClient<T>()"

Phase 2 (sequential) — synthesize findings into report.
```

Each subagent is stateless — give it a precise task and tell it exactly what to return.

## Documentation

- Architecture: [`docs/architecture.md`](docs/architecture.md)
- HttpClient pattern: [`docs/httpclient-pattern.md`](docs/httpclient-pattern.md)
- Plugin development: [`docs/plugin-development.md`](docs/plugin-development.md)
- Plugin system v2: [`docs/plugin-system-v2.md`](docs/plugin-system-v2.md)
- Project structure: [`docs/project-structure.md`](docs/project-structure.md)
- **Subagent patterns**: [`docs/subagent-patterns.md`](docs/subagent-patterns.md) — how to use parallel subagents for reviews
