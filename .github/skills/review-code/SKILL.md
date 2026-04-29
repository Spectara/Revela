---
name: review-code
description: Reviews C# code against Revela project conventions, .editorconfig rules, and .NET 10 best practices. Covers naming, patterns, async, logging, DI, configuration, commands, testing, and code style. Use when reviewing code, suggesting improvements, or checking for convention violations in the Revela codebase.
---

# Code Review — Revela Project

Review code against Revela project conventions, .editorconfig rules, and .NET best practices.
Check each category and report issues found. Skip categories with no issues.

**General principle:** Always prefer the latest stable C# language features and .NET APIs over older patterns.
This project targets the newest .NET and C# versions — there is no backward compatibility requirement.
When reviewing, actively look for opportunities to modernize code using current language features,
newer BCL APIs, and modern idioms. If an older pattern has a modern replacement, flag it.

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

## 2. Modern C# & .NET Patterns

**Always use the newest C# language version and .NET APIs available.** This project targets **.NET 10 + C# 14**. Actively replace older patterns:

### Core language style
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
- **Expression bodies** — use for single-expression methods and properties
- **Method groups over passthrough lambdas** — when a lambda simply forwards all parameters to a method with an identical signature, use the method group directly:
  ```csharp
  // ❌ DON'T — redundant passthrough lambda
  Register((a, b, c) => OnRegistered(a, b, c));

  // ✅ DO — method group
  Register(OnRegistered);
  ```

### C# 14 features (flag opportunities)
- **`field` keyword** — replace manual backing fields when only adding a guard/transform in the setter:
  ```csharp
  // ❌ OLD
  private string name = "";
  public string Name { get => name; set => name = value ?? throw new ArgumentNullException(nameof(value)); }
  // ✅ C# 14
  public string Name { get; set => field = value ?? throw new ArgumentNullException(nameof(value)); }
  ```
- **`extension` blocks** — for static extension methods, static extension properties, and instance extension properties (replaces the older `this`-parameter-only static extension method pattern when you need more than just methods).
- **Null-conditional assignment** — `obj?.Prop = value`, `obj?.Field += 1`. Replaces `if (obj is not null) obj.Prop = value;`.
- **`nameof` with unbound generics** — `nameof(List<>)`. Useful in diagnostic/error messages.
- **Implicit `Span<T>` / `ReadOnlySpan<T>` conversions** — `string` and `T[]` flow into span parameters without explicit `.AsSpan()`. Prefer span-based BCL overloads in hot paths.
- **Lambda parameter modifiers without types** — `(text, out result) => int.TryParse(text, out result)`. The `params` modifier still requires explicit types.
- **Partial constructors and events** — needed by source generators; not common in handwritten code.

### Modern BCL APIs (.NET 9 / .NET 10 — replace older equivalents)
- **`System.Threading.Lock`** instead of `lock(new object())` — IDE0330 enforces this.
  ```csharp
  // ❌ OLD
  private readonly object gate = new();
  // ✅ C# 13 / .NET 9
  private readonly Lock gate = new();
  ```
- **`Random.Shared`** instead of `new Random()`.
- **`TimeProvider`** instead of `DateTime.UtcNow` in code that needs to be testable.
- **`SearchValues<T>`** for repeated `IndexOfAny` over a fixed character set.
- **`Regex.EnumerateMatches`** instead of `Regex.Matches` (zero-alloc).
- **`FrozenDictionary` / `FrozenSet`** for static readonly lookups never mutated after init.
- **`params` collections (C# 13)** — `params Span<int>`, `params IEnumerable<T>` instead of `params T[]`.
- **`OrderedDictionary<TKey, TValue>`** with `TryAdd(key, value, out int index)` (.NET 10).
- **Async ZIP APIs (.NET 10)** — `ZipFile.ExtractToDirectoryAsync`, `ZipArchive.CreateAsync`, `ZipArchiveEntry.OpenAsync`. Flag any `ZipFile.ExtractToDirectory` (sync) in async contexts — especially in the Compress plugin.
- **`CompareOptions.NumericOrdering` (.NET 10)** — for natural sort (`"file2"` before `"file10"`).
- **`JsonSerializerOptions.AllowDuplicateProperties = false` (.NET 10)** — set explicitly when parsing config files for stricter validation.

When in doubt, check if there is a newer API or language feature that replaces older code.

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
- **No `ConfigureAwait(false)`** — CA2007 is suppressed (application, not library). Find and remove all occurrences.
- **No fake-async** — never wrap synchronous code in `Task.FromResult()` with an `Async` suffix. If a method never awaits, make it synchronous:
  ```csharp
  // ❌ DON'T — fake async
  public static Task<Result> DoWorkAsync(CancellationToken ct = default)
  {
      _ = ct;
      return Task.FromResult(SyncWork());
  }

  // ✅ DO — synchronous method, no Async suffix
  public static Result DoWork() => SyncWork();
  ```
- **Shutdown/wait loops** — never poll with `while + Task.Delay(100)`. Use `CancellationTokenSource.CreateLinkedTokenSource` + `Task.Delay(Timeout.Infinite, token)` instead:
  ```csharp
  // ❌ AVOID — CPU wakeups, 100ms latency
  while (running) { await Task.Delay(100, CancellationToken.None); }

  // ✅ PREFER — zero-CPU, instant response
  using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
  try { await Task.Delay(Timeout.Infinite, cts.Token); }
  catch (OperationCanceledException) { }
  ```

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
- **Exception: char overloads** — `StartsWith(char)` and `EndsWith(char)` have no `StringComparison` parameter (char comparison is inherently ordinal). Using `StartsWith("-", StringComparison.Ordinal)` triggers CA1865 requiring the char overload. Use `StartsWith('-')` directly.
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
- Plugin config: section name = full package ID (`Spectara.Revela.Plugins.X`)

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
- Escape user data in Spectre markup: use `Markup.Escape()` — **never** write custom escape methods
  ```csharp
  // ❌ DON'T — custom escape method
  text.Replace("[", "[[").Replace("]", "]]");

  // ✅ DO — built-in Spectre method
  Markup.Escape(userInput)
  ```
- **Use `PanelStyles` extension methods** — never manually set `.Border(BoxBorder.Rounded).BorderStyle(...)`. Use `WithInfoStyle()`, `WithWarningStyle()`, `WithErrorStyle()`, `WithSuccessStyle()` from `Spectara.Revela.Sdk.PanelStyles`
  ```csharp
  // ❌ DON'T — manual panel styling
  panel.Border(BoxBorder.Rounded).BorderStyle(new Style(Color.Cyan1));

  // ✅ DO — consistent SDK styles
  panel.WithInfoStyle();
  ```
- **Use `ErrorPanels`** for error/warning display — `ErrorPanels.ShowError(title, message)`, `ErrorPanels.ShowException(ex)`, `ErrorPanels.ShowWarning(title, message)` from `Spectara.Revela.Sdk`. Don't build custom error panels manually.

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
- **Prefer clean implementation over suppression** — when a code analyzer flags a warning, fix the root cause instead of adding `#pragma warning disable` or `[SuppressMessage]`. Common fixes:
  - CA2227 (collection setter): `Dictionary<K,V>` → `IReadOnlyDictionary<K,V>`
  - CA1002 (generic list): `List<T>` → `IReadOnlyList<T>`
  - CA1056 (URI string): `string? Url` → `Uri?` (STJ deserializes `Uri` natively)
  - CA1819 (array property): `T[]` → `IReadOnlyList<T>`
  - CA1849 (sync in async): use async API or restructure to avoid mixing sync/async
  
  Only suppress when no clean alternative exists (e.g., CA1054 for user-facing URI input strings).
- **No general exception catching** — avoid `catch (Exception)` in business logic (CA1031)
- **No swallowed exceptions** — always log/report, never empty `catch` blocks
- **Thread-safety** — never use plain `bool` flags across threads. Use `volatile`, `CancellationTokenSource`, or `Interlocked`
- **Verify all code paths are reachable and useful:**
  - Trace every field, parameter, method, and class — is it actually used?
  - Remove unused fields, methods, parameters, and imports (don't just suppress IDE0051/IDE0052)
  - Check if helper methods duplicate functionality already in the framework or project (e.g. custom string escape vs. `Markup.Escape()`)
  - Verify README/docs match actual code — remove documented features that don't exist
  - Question every code path: if a branch can never be reached, remove it
- **Async file I/O** — use `FileStream` with `useAsync: true` + `CopyToAsync` for large files, never `File.ReadAllBytes` + sync write:
  ```csharp
  // ❌ AVOID — loads entire file into memory, blocks thread
  var bytes = File.ReadAllBytes(path);
  stream.Write(bytes, 0, bytes.Length);

  // ✅ PREFER — streaming, async, configurable buffer
  await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
  await fs.CopyToAsync(outputStream);
  ```

## 15. Documentation Consistency

- **README matches code** — verify plugin/theme README documents only features that actually exist in code
- **Website docs match code** — check `docs/plugins/`, `docs/revela/`, and `samples/revela-website/` for outdated info
- **CLI options documented** — all `--option` flags in README must exist in the command definition
- **Config examples valid** — JSON examples in docs must match actual config models (property names, types, defaults)
- **Sample projects current** — samples should work with current codebase without errors

## Output Format

For each issue found, report:
- **File + location** (method/property name)
- **Rule violated** (from categories above)
- **Current code** → **Suggested fix**

End with a summary: total issues, severity breakdown (error/warning/suggestion).
