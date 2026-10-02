---
applyTo: "src/Plugins/**/*.cs"
description: "Plugin development conventions — IPlugin lifecycle, CommandDescriptor, config"
---

# Plugin Conventions — Revela

External plugins live under `src/Plugins/`. **Built-in features (`Generate`, `Packages`, `Theme`) are NOT plugins** — they live in `src/Features/` and are registered via `AddRevelaCommands()`.

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
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.MyFeature",
        Name = "My Feature",
        // Built package version (no build metadata) — never hardcode it.
        Version = PackageVersion.FromAssembly(typeof(MyFeaturePlugin).Assembly),
        Description = "What it does",
        Author = "Spectara"
    };

    public void ConfigureServices(IServiceCollection services)
    {
        // BindConfiguration must live in user-written source so the .NET
        // Configuration Binding Source Generator can intercept it (trim/AOT).
        services.AddOptions<MyFeatureConfig>()
            .BindConfiguration(MyFeatureConfig.Section);
        // Trim/AOT-safe DataAnnotations validation via [OptionsValidator]; TryAddEnumerable keeps it idempotent.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MyFeatureConfig>, MyFeatureConfigValidator>());

        services.TryAddTransient<MyService>();
        services.TryAddTransient<MyCommand>();
        services.AddHttpClient<MyApiClient>();   // typed client
    }

    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider sp)
    {
        var cmd = sp.GetRequiredService<MyCommand>();
        yield return new CommandDescriptor(
            cmd.Create(),
            ParentCommand: "source",       // null = root, "source"/"generate"/"clean"/"config" common
            Order: 30,
            Group: "Content",
            RequiresProject: true,
            HideWhenProjectExists: false,
            IsSequentialStep: false        // true → discovered by `generate all`
        );
    }
}
```

## CommandDescriptor — Parameters
| Param | Meaning |
|-------|---------|
| `Command` | The `System.CommandLine.Command` instance (from `MyCommand.Create()`) |
| `ParentCommand` | `null` = root level, `"source"`/`"generate"`/`"clean"`/`"config"` = subcommand. Parent created automatically if missing. Multi-level paths (`"<parent> <sub>"`) are supported. |
| `Order` | Sort order within parent (default 50; lower = earlier). For pipeline steps also the execution order — use a named constant relative to `PipelineOrder`/`CleanPipelineOrder` (see below). |
| `Group` | Display group label in interactive menu |
| `RequiresProject` | `true` (default) = only inside a project. Keep `true` for anything that reads or writes `project.json`, including every `config <plugin>` command. `false` only for commands that work without a project (e.g. one-time setup commands, usually with `HideWhenProjectExists: true`). |
| `HideWhenProjectExists` | `true` = hidden inside a project (e.g. setup wizards) |
| `IsSequentialStep` | `true` = picked up by CLI `generate all` / `clean all`. Pair with `IPipelineStep` for engine/MCP. |

Package listings are host-owned (`revela plugin list`, `revela theme list`); plugins don't add diagnostic subcommands under `info`. Use an `ICheck` (`revela check <name>`) for active probing.

## Public SDK Surface
The SDK (`src/Sdk`) is the contract for plugin and theme authors; host-only contracts live in `src/Core`. `Microsoft.CodeAnalysis.PublicApiAnalyzers` tracks every public member: adding or removing one fails the build (RS0016/RS0017) until `src/Sdk/PublicAPI.Unshipped.txt` is updated in the same change. Only make something public in the SDK when an official plugin or theme needs it; read the scanned site through `IManifestReader`, never `.revela/cache/manifest.json`. Reproducible plugin data goes below `ProjectPaths.Cache`, records of what a plugin produced in the output below `ProjectPaths.State`; never write Revela-internal files into the output (it is published).

## Plugin Configuration
1. Create config class with `[RevelaConfig("plugins:myFeature")]` plus a hand-written `public const string Section = "plugins:myFeature";` (CBSG needs to see the const in user-source). All plugin settings live below the host-owned `plugins` node; the key must match `^[a-z][a-zA-Z0-9]*$` (camelCase, no `.`/`:`/`/`/`_`). The SDK generator reports `REVELA001` for any other section in a plugin/theme assembly and `REVELA002` if attribute and const differ; it also emits the ownership claim the host uses to reject two packages claiming the same key.
2. **Property accessors must be `{ get; set; }`** (not `init`) and **collection properties getter-only with initializer** (`Dictionary<,> X { get; } = [];`). CBSG silently skips `init`-only properties and triggers CA2227 on settable collections.
3. Register from `ConfigureServices`:
   ```csharp
   services.AddOptions<MyFeatureConfig>().BindConfiguration(MyFeatureConfig.Section);
   services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MyFeatureConfig>, MyFeatureConfigValidator>());
   ```
4. For DataAnnotations validation: add an empty `[OptionsValidator]`-marked partial class implementing `IValidateOptions<MyFeatureConfig>` — the `Microsoft.Extensions.Options` source generator emits the trim/AOT-safe Validate body. Do NOT use `OptionsBuilder.ValidateDataAnnotations()` (reflection-based, IL2026).
5. Inject `IOptions<MyFeatureConfig>` (read once) or `IOptionsMonitor<MyFeatureConfig>.CurrentValue` (when the same process may write the config first, e.g. the interactive menu after a `config` command). Config files are not watched; Revela's own writers reload the configuration after writing.
6. CLI options override config inside the command: `var url = urlOverride ?? config.CurrentValue.ApiUrl;` (command-line values are never a configuration layer).

7. Persist settings from a `config` command via `configService.UpdateProjectConfigAsync(PluginConfigSection.CreateUpdate(MyFeatureConfigKeys.Section, settings))`. Such commands write `project.json`, so keep `RequiresProject: true`. Without options on a non-interactive console (`!IConsoleCapabilities.IsInteractive`), don't prompt — show the options to pass and return 1.

Example `project.json`:
```json
{
  "plugins": {
    "myFeature": {
      "apiUrl": "https://api.example.com",
      "timeout": 60
    }
  }
}
```

ENV override: `SPECTARA__REVELA__PLUGINS__MYFEATURE__APIURL=...`

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
- Order: declare a named constant relative to the host slots, e.g. `private const int GenerateOrder = PipelineOrder.Scan + 100;` / `CleanPipelineOrder.Cache + 100`. `PipelineOrder` only has host slots (`Scan`, `Pages`, `Images`); data that pages read goes between `Scan` and `Pages`, output post-processing after `Images`.
- Producing a derived artifact? Register an `IArtifactInvalidator` and delete old files with `DerivedFiles.DeleteAll` (never follows reparse points).
- Checks: an `ICheck` error only sets the exit code of `revela check` (2); `generate` doesn't run checks, so the step must fail on its own.

## HttpClient — Typed Client Only
```csharp
// ConfigureServices:
services.AddHttpClient<MyApiClient>((serviceProvider, client) =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    var version = serviceProvider.GetRequiredService<IBuildInfo>().Version;
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"Revela/{version} (Static Site Generator)");
});

// Service constructor: inject HttpClient DIRECTLY
public MyApiClient(HttpClient httpClient, ILogger<MyApiClient> logger) { ... }
```

❌ Never `new HttpClient()`, never inject `IHttpClientFactory` into a typed client, never cache `HttpClient` in a singleton field, never hardcode the Revela version in the User-Agent.

## Console Output (Spectre.Console)
Two-phase pattern: `AnsiConsole.Status()` for unknown totals (scan), `AnsiConsole.Progress()` for known totals (download). Always escape user data with `Markup.Escape()`.

Inject `IConsoleCapabilities` (`Spectara.Revela.Sdk.Hosting`) — never check `Console.IsOutputRedirected` yourself:
- **Prompts** (`AnsiConsole.Prompt`, confirmations) only when `IsInteractive`. Otherwise fail with exit code 1 and say which options to pass; destructive actions need an explicit `--yes`.
- **Live output** (`Status()`, `Progress()`, `Live()`) only when `CanRenderLive`. Otherwise print plain progress lines (e.g. one per 10 %). `Live()` throws on redirected output, so it must never run unguarded.

## Plugin Tests
Test project lives at `tests/Plugins/<Name>/` and references the plugin project. Use `Substitute.For<HttpMessageHandler>()` or the `MockHttpMessageHandler` pattern for HTTP. Use `TestProject` fixture for filesystem tests.

## Reference
- Full plugin guide: [`docs/plugin-development.md`](../../docs/plugin-development.md)
- Plugin architecture: [`docs/architecture.md`](../../docs/architecture.md)
- HttpClient pattern: [`docs/plugin-development.md` — typed-client pattern](../../docs/plugin-development.md#making-http-calls-the-typed-client-pattern)
- SDK package readme (compiled examples): [`src/Sdk/README.md`](../../src/Sdk/README.md)
