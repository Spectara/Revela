# Code Review — Revela Project

Review the provided code against Revela project conventions, .editorconfig rules, and .NET best practices.
Check each category and report issues found. Skip categories with no issues.

## 1. Naming Conventions (enforced by .editorconfig as warnings/errors)

- Private instance fields: `camelCase` — **NO underscore prefix!** (`logger`, not `_logger`)
- Const fields: `PascalCase`
- Static readonly fields: `PascalCase`
- Public members: `PascalCase`
- Async methods: `MethodNameAsync` suffix
- Interfaces: `I` prefix (`IMyService`)
- Type parameters: `T` prefix (`TResult`)
- Parameters & locals: `camelCase`
- **No public/protected fields** — use properties instead (enforced as error)

## 2. C# 14 / .NET 10 Patterns

- **File-scoped namespaces** — always (`namespace Spectara.Revela.Core;`)
- **Primary constructors** for DI — preferred (suggestion level)
- **Collection expressions** — use `[]` not `new List<>()` or `Array.Empty<>()`
- **`var`** — use everywhere, all three var rules are warning level
- **Nullable** — enabled globally, handle nulls properly
- **`using` directives** — outside namespace, System first (`dotnet_sort_system_directives_first`)
- **`sealed`** — prefer on all classes that aren't designed for inheritance
- **Pattern matching** — prefer `is`, `is not`, switch expressions (warning level)
- **Index/range operators** — prefer `^1` and `..` syntax (warning level)
- **Braces** — always required, even for single-line `if` (`csharp_prefer_braces = true:warning`)

## 3. Boolean & Null Checking (Revela Custom Rule)

- **Prefer explicit pattern matching over `!` operator:**
  ```csharp
  // ✅ PREFER
  if (value is true) { }
  if (value is false) { }
  if (value is null) { }
  if (value is not null) { }

  // ❌ AVOID
  if (!value) { }           // Ambiguous with null-forgiving
  if (value != null) { }    // Use 'is not null' instead
  ```
- **Null coalescing** — use `??` and `?.` operators (warning level)
- **`is null`** — prefer over `== null` / `ReferenceEquals` (warning level)

## 4. Async & Cancellation

- All async methods must accept `CancellationToken cancellationToken = default`
- Always pass `cancellationToken` to downstream calls
- Async methods must have `Async` suffix
- **No ConfigureAwait needed** — CA2007 is suppressed (application, not library)

## 5. Logging

- Use **LoggerMessage source generator** (class must be `partial`):
  ```csharp
  [LoggerMessage(Level = LogLevel.Information, Message = "Processing {Count} items")]
  private static partial void LogProcessing(ILogger logger, int count);
  ```
- **Never** use string interpolation in log calls (`logger.LogInformation($"...")`)
- Inject `ILogger<T>` via constructor

## 6. String & Culture

- **`StringComparison.Ordinal`** — always specify on `Contains()`, `Replace()`, `IndexOf()`, `StartsWith()`, `EndsWith()`
- **`CultureInfo.InvariantCulture`** — for number/date formatting
- **Prefer simplified interpolation** — `$"{x}"` not `$"{x.ToString()}"` (warning level)

## 7. Dependency Injection

- Constructor injection with primary constructors
- No `IServiceProvider` in business logic — resolve via constructor
- Register services in `ServiceCollectionExtensions`
- HttpClient: use **Typed Client pattern** (`services.AddHttpClient<T>()`)

## 8. Configuration

- Use `IOptions<T>` / `IOptionsMonitor<T>` pattern
- Config models: `sealed class` with `public const string SectionName`
- Use `DataAnnotations` for validation + `ValidateOnStart()`
- Plugin config: section name = full package ID (`Spectara.Revela.Plugin.X`)

## 9. Commands (System.CommandLine 2.0)

- Options: `new Option<string>("--name", "-n") { Description = "..." }`
- Add via `command.Options.Add(option)`
- Handler: `command.SetAction(parseResult => { ... })`
- Return `CommandDescriptor` with all 6 parameters when relevant

## 10. Console Output

- Use `OutputMarkers` from `Spectara.Revela.Sdk.Output`:
  - `OutputMarkers.Success` (green ✓), `OutputMarkers.Error` (red ✗)
  - `OutputMarkers.Warning` (yellow ⚠), `OutputMarkers.Info` (blue ℹ)
- **Never** use raw Spectre markup for status symbols
- Escape user data in Spectre markup: `[` → `[[`, `]` → `]]`

## 11. Paths

- **Never** hardcode `"source"` or `"output"` — use `IPathResolver`
- Non-configurable paths: use `ProjectPaths` constants (Cache, Themes, Plugins, etc.)

## 12. Code Style (enforced by .editorconfig)

- **`readonly`** on fields that are never reassigned (warning level)
- **Object/collection initializers** — prefer `new Foo { X = 1 }` over assignment (warning level)
- **Compound assignment** — prefer `+=`, `??=` etc. (warning level)
- **Inline variable declarations** — `if (int.TryParse(s, out var x))` (warning level)
- **Simple default** — `default` not `default(T)` (warning level)
- **Throw expressions** — `?? throw new` pattern (warning level)
- **Unused parameters** — all must be used or removed (warning level)
- **No `this.` qualification** — never prefix members with `this.` (warning level)
- **Predefined types** — `int` not `Int32`, `string` not `String` (warning level)
- **Accessibility modifiers** — required on non-interface members (warning level)
- **Auto-properties** — prefer over manual backing fields (warning level)

## 13. Testing

- **MSTest v4** + **NSubstitute** (no FluentAssertions)
- Modern assertions: `Assert.IsEmpty()`, `Assert.HasCount()`, `Assert.Contains()`
- HTTP mocking: `MockHttpMessageHandler` pattern
- `InternalsVisibleTo` for testing internal classes
- Test method naming: `MethodName_Condition_ExpectedResult`

## 14. Code Quality

- `TreatWarningsAsErrors=true` — no suppressed warnings without justification
- XML docs required for public APIs
- No dead code — delete instead of commenting out
- No `#pragma warning disable` without matching `#pragma warning restore`
- **No general exception catching** — avoid `catch (Exception)` in business logic (CA1031)

## Output Format

For each issue found, report:
- **File + location** (method/property name)
- **Rule violated** (from categories above)
- **Current code** → **Suggested fix**

End with a summary: total issues, severity breakdown (error/warning/suggestion).
