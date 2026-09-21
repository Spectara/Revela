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

## Routing And Conventions

Use Revela Dev for implementation, Revela Reviewer for read-only audits, Revela
Docs for product documentation and UX Advocate for assigned browser checks.
The [baseline instructions](.github/copilot-instructions.md) and
[agent definitions](.github/agents/) describe the remaining specialized roles.

Load only the scoped conventions relevant to the task:

- [C#](.github/instructions/csharp.instructions.md): all C# files; `.editorconfig` is authoritative.
- [Plugins](.github/instructions/plugins.instructions.md): lifecycle, commands and configuration.
- [Tests](.github/instructions/tests.instructions.md): MSTest, fixtures and meaningful assertions.
- [Themes](.github/instructions/themes.instructions.md): manifests, Scriban, assets and browser checks.

See [Project Structure](docs/project-structure.md) for the directory/build map and
[Architecture](docs/architecture.md) for ownership boundaries. Built-in features
are not dynamically loaded plugins.

## Reasoned Delegation

Use the [reasoned delegation workflow](docs/subagent-patterns.md#reasoned-delegation) and [assignment contract](docs/subagent-patterns.md#assignment-contract) for scoped outcomes, reasons, protected behaviors, resource ownership, and acceptance. Workers own reversible local details and challenge contradicted assumptions with evidence; the parent owns decisions, integration, and final verification.

Keep **Revela Scout MAI** and **Revela Worker MAI** pinned to **MAI Code 1.1**, as explained by the [delegation rationale](docs/subagent-patterns.md#reasoned-delegation). Configuration alone does not prove runtime availability or general reliability.

## Build / Test / Run

```pwsh
dotnet build
dotnet test --solution Spectara.Revela.slnx
dotnet format --verify-no-changes
```

**Task-scoped post-edit gates:**

- **.NET code/build changes:** `dotnet build` -> relevant `dotnet test` -> `dotnet format --verify-no-changes` remains mandatory.
- **Theme/site changes:** fresh generation with the target configuration, generated output/link/image/asset checks, and browser checks for affected journeys and viewports.
- **Documentation/agent-only changes:** local links, applicable YAML frontmatter, routing/tool availability, and instruction consistency; no unrelated .NET checks.
- **Mixed changes:** the union of applicable gates. Unavailable checks are reported as verification gaps, not passes.

The parent owns integration and final gates; a Worker runs its focused check.
See the [verification workflow](docs/subagent-patterns.md#shared-resources-and-verification)
and [Development Guide](docs/development.md) for sample, packaging and runtime checks.

## Git Consent

Follow the [Git consent rules](.github/copilot-instructions.md#git--hard-rule).
Every history-mutating Git command requires explicit user approval for that exact action.
