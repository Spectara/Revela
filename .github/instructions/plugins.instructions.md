---
applyTo: "src/Plugins/**/*.cs"
description: "Plugin development conventions — IPlugin lifecycle, CommandDescriptor, config"
---

# Plugin Conventions — Revela

External plugins live under `src/Plugins/`. **Built-in features (`Generate`, `Theme`, `Projects`) are NOT plugins** — they live in `src/Features/` and are registered via `AddRevelaCommands()`.

## Plugin Lifecycle (4 phases)
1. **Discovery** — `IPackageSource.LoadPlugins()` (Disk or Embedded).
2. **`ConfigureConfiguration`** *(optional, default no-op)* — usually unused; ENV vars auto-loaded with `SPECTARA__REVELA__` prefix.
3. **`ConfigureServices`** *(required)* — register services, options, HttpClients. Use `TryAdd*` for idempotent registration.
4. **`GetCommands(IServiceProvider)`** *(optional, default `[]`)* — yield `CommandDescriptor` records. Resolve commands directly from DI.

## Minimal Plugin
```csharp
namespace Spectara.Revela.Plugins.MyFeature;

public sealed class MyFeaturePlugin : IPlugin
{
    public PackageMetadata Metadata => new()
    {
        Id = "Spectara.Revela.Plugins.MyFeature",
        Name = "My Feature",
        Version = "1.0.0",
        Description = "What it does",
        Author = "Spectara"
    };

    public void ConfigureServices(IServiceCollection services)
    {
        // BindConfiguration must live in user-written source so the .NET
        // Configuration Binding Source Generator can intercept it (trim/AOT).
        services.AddOptions<MyFeatureConfig>()
            .BindConfiguration(MyFeatureConfig.Section);
        // Trim/AOT-safe DataAnnotations validation via [OptionsValidator].
        services.AddSingleton<IValidateOptions<MyFeatureConfig>, MyFeatureConfigValidator>();

        services.TryAddTransient<MyService>();
        services.TryAddTransient<MyCommand>();
        services.AddHttpClient<MyApiClient>();   // typed client
    }

    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider sp)
    {
        var cmd = sp.GetRequiredService<MyCommand>();
        yield return new CommandDescriptor(
            cmd.Create(),
            ParentCommand: "source",       // null = root, "source"/"generate"/"theme" common
            Order: 30,
            Group: "Content",
            RequiresProject: true,
            HideWhenProjectExists: false,
            IsSequentialStep: false        // true → discovered by `generate all`
        );
    }
}
```

## CommandDescriptor — All 7 Parameters
| Param | Meaning |
|-------|---------|
| `Command` | The `System.CommandLine.Command` instance (from `MyCommand.Create()`) |
| `ParentCommand` | `null` = root level, `"source"`/`"generate"`/etc. = subcommand. Parent created automatically if missing. **Multi-level paths supported** (`"info plugins"` → `revela info plugins <name>`). |
| `Order` | Sort order within parent (default 50; lower = earlier) |
| `Group` | Display group label in interactive menu |
| `RequiresProject` | `false` = available without `project.json` (e.g. `init`, `setup`) |
| `HideWhenProjectExists` | `true` = hidden inside a project (e.g. setup wizards) |
| `IsSequentialStep` | `true` = picked up by CLI `generate all` discovery. Pair with `IPipelineStep` for engine/MCP. |
| `InlineInMenu` | Host-only flag (`info` command tree). Plugins should not need this. |
| `InlineDefaultActionLabel` | Required when `InlineInMenu = true`. Plugins should not need this. |

## `info` Subcommands — Convention for Plugins
Plugins **may** contribute one read-only diagnostic subcommand under
`revela info plugins <plugin-name>` by registering with
`ParentCommand: "info plugins"`. This is opt-in; nothing breaks if you skip it.

```csharp
yield return new CommandDescriptor(
    myInfoCommand.Create(),
    ParentCommand: "info plugins",
    Order: 10);
```

Hard rules for `info` subcommands:
- **Read-only.** No prompts, no writes, no network calls that mutate state.
- **Compact.** Output sized for bug-report copy-paste — typically a single
  Spectre `Panel` with key/value lines. No tables that scroll.
- **Fast.** No long-running work; user expects a tap-and-read response.
- **Safe without context.** Must not crash when invoked without an active
  project (e.g. report "no project loaded" instead of throwing).
- **No side effects on cache, auth, or files.** This is diagnostics, not
  troubleshooting tooling. Use a dedicated `doctor` or `check` command if
  you need active probing.

## Plugin Configuration
1. Create config class with `[RevelaConfig("Spectara.Revela.Plugins.MyFeature")]` (documentation marker) plus a hand-written `public const string Section = "Spectara.Revela.Plugins.MyFeature";` (CBSG needs to see the const in user-source).
2. **Property accessors must be `{ get; set; }`** (not `init`) and **collection properties getter-only with initializer** (`Dictionary<,> X { get; } = [];`). CBSG silently skips `init`-only properties and triggers CA2227 on settable collections.
3. Register from `ConfigureServices`:
   ```csharp
   services.AddOptions<MyFeatureConfig>().BindConfiguration(MyFeatureConfig.Section);
   services.AddSingleton<IValidateOptions<MyFeatureConfig>, MyFeatureConfigValidator>();
   ```
4. For DataAnnotations validation: add an empty `[OptionsValidator]`-marked partial class implementing `IValidateOptions<MyFeatureConfig>` — the `Microsoft.Extensions.Options` source generator emits the trim/AOT-safe Validate body. Do NOT use `OptionsBuilder.ValidateDataAnnotations()` (reflection-based, IL2026).
5. Inject `IOptionsMonitor<MyFeatureConfig>` into commands/services for hot-reload.
6. CLI args override config: `var url = urlOverride ?? config.CurrentValue.ApiUrl;`

Example `project.json`:
```json
{
  "Spectara.Revela.Plugins.MyFeature": {
    "ApiUrl": "https://api.example.com",
    "Timeout": 60
  }
}
```

ENV override: `SPECTARA__REVELA__SPECTARA__REVELA__PLUGINS__MYFEATURE__APIURL=...`

## Commands (System.CommandLine 2.0 — final, NOT beta!)
```csharp
public sealed partial class MyCommand(
    ILogger<MyCommand> logger,
    IOptionsMonitor<MyFeatureConfig> config,
    MyService service)
{
    public Command Create()
    {
        var command = new Command("mycommand", "Description");

        var nameOption = new Option<string>("--name", "-n") { Description = "Name" };
        command.Options.Add(nameOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var name = parseResult.GetValue(nameOption);
            return await ExecuteAsync(name, ct);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string? name, CancellationToken ct)
    {
        LogExecuting(logger, name ?? "default");
        await service.DoAsync(name, ct);
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Executing with name: {Name}")]
    private static partial void LogExecuting(ILogger logger, string name);
}
```

## Pipeline Steps (for `generate all`)
- Implement `IPipelineStep` (UI-free, pure service) — used by engine and MCP.
- Set `IsSequentialStep: true` on the `CommandDescriptor` — used by CLI `generate all`.

## HttpClient — Typed Client Only
```csharp
// ConfigureServices:
services.AddHttpClient<MyApiClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0");
});

// Service constructor: inject HttpClient DIRECTLY
public MyApiClient(HttpClient httpClient, ILogger<MyApiClient> logger) { ... }
```

❌ Never `new HttpClient()`, never inject `IHttpClientFactory` into a typed client, never cache `HttpClient` in a singleton field.

## Progress Display (Spectre.Console)
Two-phase pattern: `AnsiConsole.Status()` for unknown totals (scan), `AnsiConsole.Progress()` for known totals (download). Always escape user data with `Markup.Escape()`.

## Plugin Tests
Test project lives at `tests/Plugins/<Name>/` and references the plugin project. Use `Substitute.For<HttpMessageHandler>()` or the `MockHttpMessageHandler` pattern for HTTP. Use `TestProject` fixture for filesystem tests.

## Reference
- Full plugin guide: [`docs/plugin-development.md`](../../docs/plugin-development.md)
- Plugin system v2: [`docs/plugin-system-v2.md`](../../docs/plugin-system-v2.md)
- HttpClient pattern: [`docs/httpclient-pattern.md`](../../docs/httpclient-pattern.md)
