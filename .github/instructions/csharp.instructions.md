---
applyTo: "**/*.cs"
description: "C# code style and conventions enforced for all .cs files in Revela"
---

# C# Conventions — Revela

These rules apply to every `.cs` file. They are enforced by `.editorconfig` (warnings → errors via `TreatWarningsAsErrors=true`).

## Naming
- **Private fields:** `camelCase` — NO underscore prefix (`logger`, never `_logger`).
- **Const fields, static readonly:** `PascalCase`.
- **Async methods:** suffix `Async` (`ProcessAsync`).
- **Interfaces:** `I` prefix (`IMyService`).
- **No public/protected fields** — properties only.

## Modern C# (use the newest available — C# 14 on .NET 10)
- **File-scoped namespaces** always (`namespace Spectara.Revela.X;`).
- **Primary constructors** for DI.
- **`var` everywhere** — never spell out the type.
- **Collection expressions:** `[]` not `new List<>()` or `Array.Empty<>()`.
- **`sealed`** by default on classes not designed for inheritance.
- **Pattern matching:** `is null`, `is not null`, `is true`, `is false` (never `== null`, `!= null`, `!value`).
- **Expression bodies** for single-expression members.
- **Braces required** even for single-line `if`/`else`/`for`/`while`.
- **Method groups** over passthrough lambdas: `Register(OnRegistered)` not `Register((a,b) => OnRegistered(a,b))`.
- **Frozen collections** (`FrozenDictionary`, `FrozenSet`) for static readonly lookups.
- **No `this.` qualification.**
- **Predefined types** (`int`, `string`) not BCL aliases (`Int32`, `String`).

### C# 14 features (prefer when applicable)
- **`field` keyword in properties** — replaces manual backing fields when you only need to add a guard/notification:
  ```csharp
  // ✅ C# 14
  public string Name
  {
      get;
      set => field = value ?? throw new ArgumentNullException(nameof(value));
  }
  ```
- **`extension` blocks** — for adding static methods, static properties, or instance properties to existing types:
  ```csharp
  public static class StringExtensions
  {
      extension(string s)
      {
          public bool IsBlank => string.IsNullOrWhiteSpace(s);
      }
  }
  ```
- **Null-conditional assignment** — `obj?.Prop = value` and `obj?.Field += 1` (skip on null target).
- **`nameof` with unbound generics** — `nameof(List<>)` returns `"List"`, useful for diagnostics.
- **Lambda parameter modifiers without types** — `(text, out result) => int.TryParse(text, out result)` instead of typing every parameter.
- **Implicit Span conversions** — `T[]` and `string` flow into `Span<T>` / `ReadOnlySpan<T>` without manual `.AsSpan()`. Prefer span-based overloads in hot paths.
- **Partial constructors and events** — round out partial members for source generators.

### Modern BCL APIs (.NET 9/10 — replace older equivalents)
- **`System.Threading.Lock`** instead of `lock(new object())` — IDE0330 enforces this. Use a `private Lock fieldLock = new();` field.
- **`Random.Shared`** instead of `new Random()`.
- **`TimeProvider`** instead of `DateTime.UtcNow` in code that needs to be testable.
- **`SearchValues<T>`** for repeated `IndexOfAny` over a fixed character set.
- **`Regex.EnumerateMatches`** instead of `Regex.Matches` (zero-alloc).
- **`params` collections (C# 13)** — `params Span<int>`, `params IEnumerable<T>` etc. instead of `params T[]`.
- **`OrderedDictionary<TKey, TValue>`** with `TryAdd(key, value, out int index)` for index-aware operations (.NET 10).
- **Async ZIP APIs (.NET 10)** — `ZipFile.ExtractToDirectoryAsync`, `ZipArchive.CreateAsync`, `ZipArchiveEntry.OpenAsync` — use these in the Compress plugin and any unzip code paths.
- **`CompareOptions.NumericOrdering` (.NET 10)** — for natural sort like `"file2" < "file10"`.
- **`JsonSerializerOptions.AllowDuplicateProperties = false` (.NET 10)** — set explicitly for stricter JSON parsing of `project.json` / `site.json`.

## Strings & Culture
- **`StringComparison.Ordinal`** on every `Contains`, `Replace`, `IndexOf`, `StartsWith`, `EndsWith` overload that takes a string.
  - Exception: char overloads — use `StartsWith('-')` directly (CA1865).
- **`CultureInfo.InvariantCulture`** for every number/date format.
- **Simplified interpolation:** `$"{x}"` not `$"{x.ToString()}"`.

## Async & Cancellation
- All async methods accept `CancellationToken cancellationToken = default` and pass it downstream.
- **No `ConfigureAwait(false)`** — CA2007 is suppressed (this is an app).
- **No fake-async** — if a method never awaits, drop `async` and the `Async` suffix.
- **Wait loops:** never `while + Task.Delay(100)`. Use `Task.Delay(Timeout.Infinite, token)` with a linked `CancellationTokenSource`.

## Logging
- Use **`LoggerMessage` source generator** — class must be `partial`:
  ```csharp
  [LoggerMessage(Level = LogLevel.Information, Message = "Processing {Count} items")]
  private static partial void LogProcessing(ILogger logger, int count);
  ```
- **NEVER** use string interpolation in log calls (`logger.LogInformation($"...")`).
- Inject `ILogger<T>` via primary constructor.

## Dependency Injection
- Primary constructor injection — no `IServiceProvider` in business logic.
- **HttpClient:** Typed Client pattern (`services.AddHttpClient<MyService>()`), inject `HttpClient` directly. Never `new HttpClient()`, never `IHttpClientFactory` inside a typed client.
- Plugin services should use `TryAdd*` for idempotent registration.

## Configuration
- `IOptions<T>` / `IOptionsMonitor<T>` with `[RevelaConfig]` attribute (source generator handles registration).
- Use `DataAnnotations` for validation + `ValidateOnStart()`.

## Paths
- **Never hardcode `"source"` or `"output"`** — inject `IPathResolver` and use `SourcePath` / `OutputPath`.
- Non-configurable paths: use `ProjectPaths` constants (`Cache`, `Themes`, `Plugins`, `SharedImages`, `Static`).

## Console Output (Spectre.Console)
- Use `OutputMarkers.Success/Error/Warning/Info` from `Spectara.Revela.Sdk.Output`.
- Use `Markup.Escape(userInput)` — never custom `Replace("[", "[[")` escaping.
- Use `PanelStyles` extensions: `WithInfoStyle()`, `WithWarningStyle()`, etc. — never manual `BoxBorder.Rounded` styling.
- Use `ErrorPanels.ShowError()` / `ErrorPanels.ShowException()` — never build error panels manually.

## Code Quality — Fix, Don't Suppress
- `TreatWarningsAsErrors=true` — fix root cause instead of `#pragma warning disable` or `[SuppressMessage]`:
  - `CA2227` → `Dictionary<K,V>` → `IReadOnlyDictionary<K,V>`
  - `CA1002` → `List<T>` → `IReadOnlyList<T>`
  - `CA1056` → `string? Url` → `Uri?` (System.Text.Json deserializes `Uri` natively)
  - `CA1819` → `T[]` property → `IReadOnlyList<T>`
- No dead code — delete it. No commented-out code.
- Every `#pragma warning disable` MUST have a matching `restore`.

## Mandatory Post-Edit Gate
After edits, before reporting done:
1. `dotnet build`
2. Relevant `dotnet test`
3. `dotnet format --verify-no-changes` — must exit clean.
