# Code Review — Revela Project

Review the provided code against Revela project conventions and .NET best practices.
Check each category and report issues found. Skip categories with no issues.

## 1. Naming Conventions

- Private instance fields: `camelCase` — **NO underscore prefix!** (`logger`, not `_logger`)
- Const fields: `PascalCase`
- Static readonly fields: `PascalCase`
- Public members: `PascalCase`
- Async methods: `MethodNameAsync` suffix
- Interfaces: `I` prefix
- Parameters & locals: `camelCase`

## 2. C# 14 / .NET 10 Patterns

- **File-scoped namespaces** — always (`namespace Spectara.Revela.Core;`)
- **Primary constructors** for DI — preferred over manual field assignment
- **Collection expressions** — use `[]` not `new List<>()` or `Array.Empty<>()`
- **`var`** — use everywhere (type is apparent or not relevant)
- **Nullable** — enabled globally, handle nulls properly
- **`using` directives** — outside namespace (not inside)

## 3. Async & Cancellation

- All async methods must accept `CancellationToken cancellationToken = default`
- Always pass `cancellationToken` to downstream calls
- Async methods must have `Async` suffix

## 4. Logging

- Use **LoggerMessage source generator** (class must be `partial`):
  ```csharp
  [LoggerMessage(Level = LogLevel.Information, Message = "Processing {Count} items")]
  private static partial void LogProcessing(ILogger logger, int count);
  ```
- **Never** use string interpolation in log calls (`logger.LogInformation($"...")`)
- Inject `ILogger<T>` via constructor

## 5. Dependency Injection

- Constructor injection with primary constructors
- No `IServiceProvider` in business logic — resolve via constructor
- Register services in `ServiceCollectionExtensions`
- HttpClient: use **Typed Client pattern** (`services.AddHttpClient<T>()`)

## 6. Configuration

- Use `IOptions<T>` / `IOptionsMonitor<T>` pattern
- Config models: `sealed class` with `public const string SectionName`
- Use `DataAnnotations` for validation + `ValidateOnStart()`
- Plugin config: section name = full package ID (`Spectara.Revela.Plugin.X`)

## 7. Commands (System.CommandLine 2.0)

- Options: `new Option<string>("--name", "-n") { Description = "..." }`
- Add via `command.Options.Add(option)`
- Handler: `command.SetAction(parseResult => { ... })`
- Return `CommandDescriptor` with all 6 parameters when relevant

## 8. Console Output

- Use `OutputMarkers` from `Spectara.Revela.Sdk.Output`:
  - `OutputMarkers.Success` (green ✓), `OutputMarkers.Error` (red ✗)
  - `OutputMarkers.Warning` (yellow ⚠), `OutputMarkers.Info` (blue ℹ)
- **Never** use raw Spectre markup for status symbols
- Escape user data in Spectre markup: `[` → `[[`, `]` → `]]`

## 9. Paths

- **Never** hardcode `"source"` or `"output"` — use `IPathResolver`
- Non-configurable paths: use `ProjectPaths` constants (Cache, Themes, Plugins, etc.)
- Use `CultureInfo.InvariantCulture` for formatting numbers/dates

## 10. Testing

- **MSTest v4** + **NSubstitute** (no FluentAssertions)
- Modern assertions: `Assert.IsEmpty()`, `Assert.HasCount()`, `Assert.Contains()`
- HTTP mocking: `MockHttpMessageHandler` pattern
- `InternalsVisibleTo` for testing internal classes
- Test method naming: `MethodName_Condition_ExpectedResult`

## 11. Code Quality

- `TreatWarningsAsErrors=true` — no suppressed warnings without justification
- XML docs required for public APIs
- No dead code — delete instead of commenting out
- No `#pragma warning disable` without matching `#pragma warning restore`

## Output Format

For each issue found, report:
- **File + location** (method/property name)
- **Rule violated** (from categories above)
- **Current code** → **Suggested fix**

End with a summary: total issues, severity breakdown (error/warning/suggestion).
