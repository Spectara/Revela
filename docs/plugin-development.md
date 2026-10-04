# Plugin Development

A practical guide to building, configuring, testing, and publishing a Revela plugin.

> For the *why* behind the plugin system (architecture, ownership, what stays internal), see [Architecture](architecture.md).
> For the security rationale behind URL validation and trust, see [Security Model](security-model.md).

---

## What a plugin is

Revela is extensible through a **NuGet-based plugin system**. A plugin is a small .NET library that implements `IPlugin`, registers its services, and optionally contributes CLI commands.

- **Implement `IPlugin`** — metadata, service registration, and commands in one class.
- **Config via `IOptions`** — JSON and environment variables are auto-loaded for you.
- **Ship on NuGet** — pack and publish; users install with `revela plugin install`.

---

## Naming & trust

| Audience      | Package prefix              | Notes                                     |
| ------------- | --------------------------- | ----------------------------------------- |
| **Official**  | `Spectara.Revela.Plugins.*` | Reserved on NuGet.org, Spectara only      |
| **Community** | `YourName.Revela.Plugin.*`  | Your own prefix; install at your own risk |

> The `Spectara` prefix is reserved on NuGet.org and cannot be used by third parties.

The package **name** is only a convention. Revela detects a plugin from the `<PackageType>RevelaPlugin</PackageType>` marker in your project file (see the `.csproj` below), not from the ID. Pick any prefix you control; including a `.Revela.Plugin.` (or `.Revela.Plugins.`) segment also helps Revela classify your package in `revela plugin search` results before it is installed.

Plugins execute in the host process with the user's permissions; package naming and metadata are not a sandbox or load-time signature verification. Install only plugins whose authors and sources you trust.

---

## The `IPlugin` class

A plugin implements `IPlugin` from `Spectara.Revela.Sdk.Abstractions`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Sdk.Abstractions;

namespace YourName.Revela.Plugin.Example;

public sealed class ExamplePlugin : IPlugin
{
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "YourName.Revela.Plugin.Example",
        Name = "Example",
        // Reported by `revela plugin list`; read from the built assembly so it never drifts.
        Version = PackageVersion.FromAssembly(typeof(ExamplePlugin).Assembly),
        Description = "Example plugin for Revela",
        Author = "Your Name",
    };

    // REQUIRED: register services before the host is built.
    // Use TryAdd* so registration stays idempotent.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<ExampleService>();
        services.TryAddTransient<ExampleCommand>();
    }

    // OPTIONAL: yield the commands this plugin contributes.
    // The IServiceProvider is passed in; no field storage needed.
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var command = services.GetRequiredService<ExampleCommand>();

        // ParentCommand decides where the command sits:
        // null = root, "generate" = a pipeline step, "source" = under source, …
        yield return new CommandDescriptor(command.Create(), ParentCommand: null);
    }
}
```

The plugin lifecycle has four phases: **discovery** → `ConfigureConfiguration` (optional) → `ConfigureServices` (required) → `GetCommands` (optional).

The [SDK package readme](../src/Sdk/README.md) contains a minimal plugin, configuration
section, theme and template model; its examples are compiled by the test suite.

---

## Commands

`GetCommands` yields one `CommandDescriptor` per command. The host builds the command
tree from them:

| Parameter | Meaning |
| --- | --- |
| `ParentCommand` | `null` = root; `"source"`, `"generate"`, `"clean"`, `"config"` or a multi-level path such as `"source onedrive"`. Missing parents are created. |
| `Order` | Sort order within the parent (default 50, lower first). For pipeline steps it is also the execution order — see [Pipeline steps](#pipeline-steps). |
| `Group` | Group label in the interactive menu (`"Build"`, `"Content"`, `"Setup"`, `"Addons"`, …). |
| `RequiresProject` | `true` (default) shows the command only inside a project. Keep it `true` for anything that reads or writes `project.json`, including `config <plugin>` commands. |
| `HideWhenProjectExists` | Hide one-time setup commands once a project exists. |
| `IsSequentialStep` | Include the command in `<parent> all` (for example `generate all`). |

Use System.CommandLine 2.0 (`new Option<T>("--name", "-n")`, `command.SetAction(...)`).
Commands that only exist to show something may write to the console; services and
pipeline steps must not.

---

## Console output

Revela runs in terminals, CI jobs and pipes. Inject `IConsoleCapabilities`
(`Spectara.Revela.Sdk.Hosting`) instead of checking `Console.IsOutputRedirected`:

- **`IsInteractive`** — stdin and stdout are a terminal. Only then may a command prompt
  (`AnsiConsole.Prompt`, `ConfirmAsync`). Without it, fail with exit code 1 and tell the
  user which options to pass; for destructive actions require an explicit flag such as
  `--yes` instead of asking.
- **`CanRenderLive`** — stdout can render live output. Gate `AnsiConsole.Progress()`,
  `Status()` and `Live()` on it and write plain lines (for example one per 10 %) otherwise.

```csharp
if (noOptionsGiven && !consoleCapabilities.IsInteractive)
{
    ErrorPanels.ShowError(
        "Settings Required",
        "This console is not interactive. Pass the settings as options, for example:\n" +
        "  [cyan]revela config example --api-url https://api.example.com[/]");
    return 1;
}
```

Escape user data with `Markup.Escape(...)` and use `OutputMarkers` / `ErrorPanels` from
the SDK for consistent output.

---

## Pipeline steps

A step of `generate all` is two things: a CLI command registered with
`IsSequentialStep: true`, and an `IPipelineStep` service that the engine (and other
programmatic callers) run without any console output. Usually one class implements
both, with `IPipelineStep` implemented explicitly. (`clean all` is not a sequence of steps:
it invalidates artifacts by kind, see [how cleaning works](#how-cleaning-works). A plugin's
`clean <name>` command is still registered as a sequential step for the menu marker.)

```csharp
internal sealed partial class SearchIndexStep(SearchIndexWriter writer) : IPipelineStep
{
    string IPipelineStep.Category => PipelineCategories.Generate;

    // Must match the command name.
    string IPipelineStep.Name => "search-index";

    async ValueTask<OperationResult> IPipelineStep.ExecuteAsync(CancellationToken cancellationToken)
    {
        var error = await writer.WriteAsync(cancellationToken);
        return error is null ? OperationResult.Ok() : OperationResult.Fail(error);
    }

    public Command Create() { /* CLI command "search-index" with console output */ }
}
```

```csharp
// ConfigureServices
services.TryAddTransient<SearchIndexStep>();
services.TryAddEnumerable(ServiceDescriptor.Transient<IPipelineStep, SearchIndexStep>());

// GetCommands — post-processing of the rendered site runs after the host's image step.
yield return new CommandDescriptor(
    services.GetRequiredService<SearchIndexStep>().Create(),
    ParentCommand: "generate",
    Order: PipelineOrder.Images + 100,
    IsSequentialStep: true);
```

`PipelineOrder` (`Scan` 100, `Pages` 300, `Images` 400) and `CleanPipelineOrder`
(`Output` 100, `Images` 150, `Cache` 200) are the host's slots. Steps that produce data
read by page rendering go between `Scan` and `Pages`; plugin clean commands are listed after
`Cache` in the menu. Declare a named constant relative to these slots rather than a bare number.

---

## Checks

`revela check` runs fast, structural checks — no network, no image decoding. Contribute
one by registering an `ICheck`; the host adds `check <name>` and includes it in
`check all`. An `Error` makes `revela check` exit with code 2. Checks only run when the
user invokes `check`; they never block `generate`, so your pipeline step must still fail
on its own when it cannot run.

```csharp
internal sealed class ExampleCheck(IPathResolver pathResolver) : ICheck
{
    public string Name => "example";

    public string Title => "Example data";

    public ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var dataFile = Path.Combine(pathResolver.SourcePath, "example.json");
        IReadOnlyList<ValidationDiagnostic> diagnostics = File.Exists(dataFile)
            ? []
            : [new ValidationDiagnostic
            {
                Severity = ValidationSeverity.Error,
                Message = "example.json is missing.",
                File = dataFile,
                Suggestion = "Run 'revela source example fetch' first.",
            }];
        return ValueTask.FromResult(diagnostics);
    }
}

// ConfigureServices
services.TryAddEnumerable(ServiceDescriptor.Transient<ICheck, ExampleCheck>());
```

Return every finding in one pass. Use `Warning` or `Hint` for problems that still
produce a correct site.

---

## Page templates

An `IPageTemplate` adds `revela create page <name>`, which writes an `_index.revela`
file with frontmatter. Each `TemplateProperty` becomes a CLI option and, when it has a
`FrontmatterKey`, a frontmatter entry:

```csharp
public sealed class ExamplePageTemplate : IPageTemplate
{
    public string Name => "example";                // revela create page example <path>
    public string DisplayName => "Example Page";
    public string Description => "Create a page that shows example data";
    public string TemplateName => "example/page";   // written as template = "example/page"

    public IReadOnlyList<TemplateProperty> PageProperties { get; } =
    [
        new()
        {
            Name = "title",
            Aliases = ["--title", "-t"],
            Type = typeof(string),
            DefaultValue = "Example",
            Description = "Page title (example: 'My Data')",
            FrontmatterKey = "title",
        },
    ];
}

// ConfigureServices
services.TryAddEnumerable(ServiceDescriptor.Singleton<IPageTemplate, ExamplePageTemplate>());
```

The Calendar and Statistics plugins ship page templates you can use as a reference.

---

## Setup wizard steps

`IWizardStep` adds a step to the interactive project setup. Optional steps (the
default) are offered as checkboxes after the required host steps. Keep the actual
work in your `config` command and delegate to it, so the wizard and the command behave
the same:

```csharp
internal sealed class ExampleWizardStep(
    ConfigExampleCommand configCommand,
    IOptionsMonitor<ExampleConfig> config) : IWizardStep
{
    public string Name => "Example Source";
    public string Description => "Import data from the example service";
    public int Order => 100;   // 100–199: source providers

    public bool ShouldPrompt() => string.IsNullOrEmpty(config.CurrentValue.ApiUrl);

    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        configCommand.ExecuteInteractiveAsync(cancellationToken);
}

// ConfigureServices
services.TryAddEnumerable(ServiceDescriptor.Transient<IWizardStep, ExampleWizardStep>());
```

---

## Host information

`IBuildInfo` (`Spectara.Revela.Sdk.Hosting`) describes the running Revela host:
`Version` (for example `0.0.1-beta.21`), `InformationalVersion` (with build metadata),
`Kind` (`Full` with package management, or `Standalone`), framework and runtime. Use it
instead of hardcoding a Revela version, for example in an HTTP `User-Agent`
(see [the typed-client pattern](#register-a-typed-client)). Your own package version
comes from `PackageVersion.FromAssembly(...)`.

---

## Reading the scanned site

Plugins that work from the scanned site (statistics, calendars) inject `IManifestReader`
(`Spectara.Revela.Sdk.Abstractions`) instead of reading `.revela/core/manifest.json`:

```csharp
var snapshot = await manifestReader.TryLoadAsync(cancellationToken);
if (snapshot is null)
{
    // No usable scan yet (missing, corrupt or from an older Revela) — ask for `revela generate scan`.
    return OperationResult.Fail("No scan found. Run 'revela generate scan' first.");
}

foreach (var (sourcePath, image) in snapshot.Images) { /* ... */ }
```

The snapshot is read-only; only Revela itself writes the manifest.

---

## Where plugins keep files and how clean works

### Your owner folder

Every file a plugin keeps outside the output lives in its own folder, `.revela/<owner>/`.
The owner is the owner part of your artifact ids (`acme.captions/text` → `acme.captions`),
so pick one distinctive owner name and use it for all your artifacts. Resolve the folder
through the SDK, never hardcode `.revela`:

```csharp
var folder = Path.Combine(
    project.Value.Path,                                   // IOptions<ProjectEnvironment>
    ProjectPaths.GetOwnerDirectory(MyArtifacts.Data.Owner));
```

Owner names are lowercase letters and digits, optionally separated by single `.` or `-`
(`ProjectPaths.IsValidOwner`); `core` belongs to Revela. Files in
`.revela/<owner>/<page>/<name>.json` are what a page's `data = { … }` front matter reads.
Only files of the published site go into the output (`IPathResolver.OutputPath`):
everything there is uploaded, so never write bookkeeping files into it.

### Declare a kind for every artifact

Every file set you produce is an artifact with an `IArtifactInvalidator`. Its `Kind` says
how long it lives, and therefore which clean command removes it:

| Kind | Rule | Example | Removed by |
|------|------|---------|------------|
| `ArtifactKind.Cache` | Reproducible from the source (and durable artifacts); may be deleted at any time, losing it only costs time | `.revela/statistics/<page>/statistics.json` (Statistics) | `clean cache`, `clean all` |
| `ArtifactKind.Output` | Part of the output or describes it; goes together with the output | `.revela/compress/ownership.json` and the `.gz`/`.br` sidecars (Compress) | `clean output`, `clean all` |
| `ArtifactKind.Durable` | Expensive or impossible to reproduce | captions an AI service wrote for each photo | only your own `clean <name>` |

One owner folder may hold artifacts of several kinds. A durable artifact may only depend on
other durable artifacts; otherwise a rescan would invalidate it.

### Keep the manifest rebuildable

The scan manifest (`core/manifest`) is a cache artifact: `clean cache` deletes it and the
next scan rebuilds it from the source files. Never make the manifest the only copy of
something expensive. A plugin that enriches photos (for example AI captions or keywords)
stores its results as a durable artifact in its own folder and merges them into what it
provides during the scan.

### How cleaning works

The clean commands do not delete folders; they call the owners. `clean cache`,
`clean output` and `clean all` invalidate every registered artifact of their kinds
(core and plugin alike) plus everything that depends on them, dependents first.
Your own `clean <name>` step removes exactly your artifact through the same invalidator:

```csharp
internal sealed class CleanSearchIndexCommand(IArtifactLifecycle lifecycle)
{
    public ValueTask<OperationResult> ExecuteAsync(CancellationToken cancellationToken) =>
        lifecycle.InvalidateAsync(ExampleArtifacts.SearchIndex, cancellationToken);
}
```

Your files must tolerate disappearing between runs: a missing cache file is rebuilt, a
missing output record means "nothing in the output is known to be mine".

---

## Derived output artifacts

Plugins that create files derived from generated output must declare and invalidate
those artifacts. Revela invokes registered invalidators before an input artifact is
replaced, including through direct CLI commands and `IRevelaEngine` calls.

Artifact IDs are case-sensitive and use an `owner/name` form; the owner also names your
folder (see above). Revela's own artifacts are published by `CoreArtifacts`. A plugin
publishes IDs for its own artifacts from its package so dependent plugins can reference the
same typed value.

A `DependsOn` edge means "must be invalidated when the dependency is replaced". Only
declare it when the dependency's new version makes your artifact wrong; reading a file is not
enough.

```csharp
using Spectara.Revela.Sdk.Artifacts;

public static class ExampleArtifacts
{
    public static ArtifactId SearchIndex { get; } =
        new("yourname.search/index");
}

internal sealed class SearchIndexInvalidator(IOptions<ProjectEnvironment> project) : IArtifactInvalidator
{
    public ArtifactId Artifact => ExampleArtifacts.SearchIndex;

    // Rebuilt from the rendered site at any time.
    public ArtifactKind Kind => ArtifactKind.Cache;

    public IReadOnlyCollection<ArtifactId> DependsOn { get; } =
        [CoreArtifacts.RenderedSite];

    public ValueTask<OperationResult> InvalidateAsync(
        CancellationToken cancellationToken = default)
    {
        // Delete every file owned by SearchIndex. Return failure if cleanup is incomplete.
        var folder = Path.Combine(project.Value.Path, ProjectPaths.GetOwnerDirectory(Artifact.Owner));
        var deletion = DerivedFiles.DeleteAll(folder, "search-index.json", cancellationToken);
        return ValueTask.FromResult(deletion.Failures.Count == 0
            ? OperationResult.Ok()
            : OperationResult.Fail(deletion.Failures[0].Message));
    }
}
```

`DerivedFiles.DeleteAll` removes every file with that name below a directory and never
follows symbolic links or junctions, so a link inside your owner folder cannot make Revela
delete files elsewhere.

Register the invalidator as an enumerable service:

```csharp
services.TryAddEnumerable(
    ServiceDescriptor.Transient<IArtifactInvalidator, SearchIndexInvalidator>());
```

Registration does not intercept arbitrary plugin writes. Before replacing an artifact,
the producer must first protect downstream consumers and then remove its own previous
artifact completely:

```csharp
var preparation = await artifactLifecycle.PrepareToReplaceAsync(
    ExampleArtifacts.SearchIndex,
    cancellationToken);
if (!preparation.Success)
{
    return preparation;
}

var cleanup = await searchIndexInvalidator.InvalidateAsync(cancellationToken);
if (!cleanup.Success)
{
    return cleanup;
}

await WriteSearchIndexAsync(cancellationToken);
```

Cleanup failure must abort the write. This guarantees that a successful producer run
never mixes files from different artifact versions.

Dependencies are invalidated transitively, from the furthest dependent artifact back
to the artifact being replaced. Missing dependencies, duplicate artifact owners, and
cycles stop generation with an error.

Changing the installed or enabled plugin set is an explicit consistency boundary.
After installing, removing, or disabling a plugin, run `revela clean all` before
generating again; Revela does not persist ownership metadata for unloaded plugins.

---

## Project setup

```
YourName.Revela.Plugin.Example/
├── YourName.Revela.Plugin.Example.csproj
├── ExamplePlugin.cs
├── Commands/ExampleCommand.cs
└── README.md
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>

    <PackageId>YourName.Revela.Plugin.Example</PackageId>
    <PackageType>RevelaPlugin</PackageType>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Example plugin for Revela</Description>
    <PackageTags>revela;plugin;example</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Spectara.Revela.Sdk" Version="0.0.1-beta.21" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.12" />
  </ItemGroup>
</Project>
```

Choose an SDK package version compatible with the Revela host you target; the versions above are examples, not a latest-release declaration. The HTTP package supports the typed-client examples below. The SDK includes its generators as compile-time analyzers; no separate Revela generator package is needed.

> **SDK source:** `Spectara.Revela.Sdk` is not published on NuGet.org yet. Download `Spectara.Revela.Sdk.<version>.nupkg` from [GitHub Releases](https://github.com/spectara/revela/releases) and add its folder as a package source in your `NuGet.Config` so the `PackageReference` above can restore.

---

## Configuration

The host loads global `revela.json`, local `project.json`, `site.json` (under `site`), an optional `logging.json` and environment variables prefixed `SPECTARA__REVELA__` — nothing else (no `appsettings*.json`, no `plugins/*.json`, and command-line options are not a configuration layer). No source watches its file; Revela's own config writers reload the configuration after writing. You usually don't override `ConfigureConfiguration`; use it only to add an explicit configuration source. See the [configuration chain](architecture.md#configuration-and-paths) for precedence and the `site.json` split.

All plugin settings live below the host-owned `plugins` node. Your plugin **declares its own key** and binds the section `plugins:<key>`:

- **Key rule:** `^[a-z][a-zA-Z0-9]*$` — camelCase letters and digits, no `.`, `:`, `/` or `_` (e.g. `example`, `oneDrive`). Pick a short, descriptive key; it is what users type in `project.json` and in environment variables (`SPECTARA__REVELA__PLUGINS__EXAMPLE__APIURL`).
- **Compile-time enforcement:** in a project with `PackageType` `RevelaPlugin` or `RevelaTheme`, the SDK source generator reports `REVELA001` (error) for any other section (package-ID names, dotted or nested keys, core sections such as `generate`) and `REVELA002` when the `[RevelaConfig]` argument and the `Section` const differ.
- **Ownership:** the generator records the key in your assembly (`[assembly: RevelaPluginConfigKey("example")]`). The host reads these claims before any plugin configures services; if two installed packages claim the same key, loading fails with an error naming both packages. Other plugins may still *read* your node — only claiming is exclusive.
- **Unknown keys:** a key below `plugins` that no installed plugin claims (e.g. the typo `plugins:serv`) produces a warning with the closest claimed key as suggestion.

Keep properties writable (`set`, not `init`) for generated binding, and declare `Section` by hand so the .NET configuration binding generator can resolve it:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Abstractions;

[RevelaConfig("plugins:example")]
public sealed class ExampleConfig
{
    public const string Section = "plugins:example";

    [Required]
    public string ApiUrl { get; set; } = string.Empty;

    [Range(1, 300)]
    public int Timeout { get; set; } = 30;
}

[OptionsValidator]
internal sealed partial class ExampleConfigValidator : IValidateOptions<ExampleConfig>;
```

In `ConfigureServices`, bind and register the generated validator explicitly:

```csharp
services.AddOptions<ExampleConfig>()
    .BindConfiguration(ExampleConfig.Section);
services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IValidateOptions<ExampleConfig>, ExampleConfigValidator>());
```

Keep `BindConfiguration` in handwritten source and enable `EnableConfigurationBindingGenerator` as shown above. `[OptionsValidator]` generates a trim/AOT-safe `IValidateOptions<T>` implementation from the annotations, avoiding reflection-based validation. `[RevelaConfig]` alone does not bind options or register a validator. Validation occurs when options are read; `[Required]` does not replace outbound URL safety checks.

Inject `IOptions<ExampleConfig>` and read `.Value`, or use `IOptionsMonitor<ExampleConfig>.CurrentValue` when the same process may change the configuration before reading it (for example the interactive menu after a `config` command). Users configure it in `project.json` (or user-wide in `revela.json`):

```json
{
  "plugins": {
    "example": {
      "apiUrl": "https://api.example.com",
      "timeout": 30
    }
  }
}
```

Or for a single run: `SPECTARA__REVELA__PLUGINS__EXAMPLE__TIMEOUT=60`.

**Which config source for which use case?**
- Own plugin config: `[RevelaConfig("plugins:myPlugin")]` + `IOptions<MyPluginConfig>`
- Build/hosting settings (base URL, subpath, output path): `IOptions<ProjectConfig>`
- Site identity (title, description, author, language): `IOptions<SiteCoreConfig>`
- Theme-specific site properties: not from plugins — that's theme territory

### Persisting config from a CLI command

If your plugin contributes a `config <plugin>` command, persist settings through `IConfigService.UpdateProjectConfigAsync(...)`, not a separate file writer. It validates and deep-merges a `JsonObject` patch into `project.json`, preserving unrelated settings. Include only intended changes under your section; omission leaves an existing value unchanged, while `null` deletes a key. Use generated keys for the camelCase property names and `PluginConfigSection.CreateUpdate` to nest them below `plugins:<key>`:

```csharp
using System.Text.Json.Nodes;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Configuration.Keys;

var settings = new JsonObject();
if (!string.IsNullOrEmpty(apiUrl))
{
    settings[ExampleConfigKeys.ApiUrl] = apiUrl;
}

// { "plugins": { "example": { ... } } }
var updates = PluginConfigSection.CreateUpdate(ExampleConfigKeys.Section, settings);
await configService.UpdateProjectConfigAsync(updates, cancellationToken);
```

The SDK's `ConfigKeysGenerator` emits an internal `<Poco>Keys` class in `Spectara.Revela.Sdk.Configuration.Keys` for local `[RevelaConfig]` classes. For an options type declared in another assembly, opt in with `[assembly: RevelaConfigKeys(typeof(ThatConfig))]`. These constants are generated in your assembly for JSON writers; use the handwritten `ExampleConfig.Section`, not a generated constant, at the `BindConfiguration` call site.

---

## Making HTTP calls (the typed-client pattern)

If your plugin makes HTTP calls (syncing from a cloud source, fetching a feed, calling an API), use the **typed client** pattern, which is the approach Microsoft recommends. It gives you connection pooling, DNS-aware handler rotation, per-service configuration, and easy testing.

### Register a typed client

Register the client in your plugin's `ConfigureServices`. Configure the timeout and headers once; build the `User-Agent` from the running host's `IBuildInfo` instead of hardcoding a version:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddHttpClient<ExampleService>((serviceProvider, client) =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.BaseAddress = new Uri("https://api.example.com");
        var version = serviceProvider.GetRequiredService<IBuildInfo>().Version;
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Revela/{version}");
    });
}
```

### Inject `HttpClient` directly

The typed client injects a ready-configured `HttpClient` straight into your service, with no `IHttpClientFactory` needed:

```csharp
internal sealed class ExampleService(HttpClient httpClient, ILogger<ExampleService> logger)
{
    public async Task<string> GetDataAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/api/endpoint", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
```

Then resolve the service from DI inside your command. Keep services **transient** so each run gets a fresh, properly managed `HttpClient`.

### Validate user-supplied URLs (SSRF prevention)

If your plugin fetches **user-supplied URLs** (OneDrive shares, iCal feeds, RSS…), validate them with `UrlSafety` from `Spectara.Revela.Sdk.Validation` **before** the request. Otherwise a malicious URL could target the host's loopback interface, internal network, or a cloud metadata service.

```csharp
using Spectara.Revela.Sdk.Validation;

internal sealed class ExampleFetcher(HttpClient httpClient)
{
    public async Task FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !UrlSafety.IsSafeOutboundUrl(uri, allowHttp: false))
        {
            throw new InvalidOperationException(
                $"URL '{url}' is not a safe outbound target.");
        }

        using var response = await httpClient.GetAsync(uri, cancellationToken);
        // …
    }
}
```

`UrlSafety.IsSafeOutboundUrl(uri, allowHttp: false)` rejects non-HTTPS schemes, loopback, private/CGN ranges, link-local (including the cloud metadata IP), and more. For the full rejection list and the reasoning behind it, see [Security Model → What Revela protects against](security-model.md). To validate just a host string (for example in a prompt), use `UrlSafety.IsSafeOutboundHost(uri.Host)`.

This checks the literal host, not DNS resolution. A permitted hostname can resolve to a private address. For user-supplied targets, disable automatic redirects and validate each redirect before following it; use network egress policy when stronger isolation is required. Avoid logging full URLs that may contain credentials or tokens.

### Advanced

Retries and timeouts come from `Microsoft.Extensions.Http.Resilience` (Polly v8):

```csharp
services.AddHttpClient<ExampleService>(client => { /* … */ })
    .SetHandlerLifetime(TimeSpan.FromMinutes(10))   // default is 2 minutes
    .AddResilienceHandler("example-retry", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        });
        builder.AddTimeout(TimeSpan.FromMinutes(2));   // per attempt
    });
```

Use `.AddStandardResilienceHandler()` for the default pipeline, and
`.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })`
when you validate redirects yourself (see above). The OneDrive plugin shows all of this
together.

---

## Testing

Plugin tests live alongside the plugin and use **MSTest v4 + NSubstitute**:

```csharp
[TestClass]
public sealed class ExamplePluginTests
{
    [TestMethod]
    public void Plugin_exposes_metadata()
    {
        var plugin = new ExamplePlugin();

        Assert.AreEqual("Example", plugin.Metadata.Name);
    }

    [TestMethod]
    public void Plugin_contributes_commands()
    {
        var plugin = new ExamplePlugin();
        var services = new ServiceCollection();
        plugin.ConfigureServices(services);

        var descriptors = plugin.GetCommands(services.BuildServiceProvider()).ToList();

        Assert.IsNotEmpty(descriptors);
    }
}
```

For HTTP code, mock the transport, not the typed client, by injecting a fake handler (this example uses the RichardSzalay.MockHttp package):

```csharp
[TestMethod]
public async Task GetDataAsync_returns_payload()
{
    var handler = new MockHttpMessageHandler();
    handler.When("https://api.example.com/*")
        .Respond("application/json", "{\"data\":\"test\"}");

    var service = new ExampleService(handler.ToHttpClient(), Substitute.For<ILogger<ExampleService>>());

    var result = await service.GetDataAsync(CancellationToken.None);

    Assert.Contains("test", result);
}
```

---

## Packaging & publishing

Pack the plugin and test it locally before publishing:

```bash
dotnet pack -c Release -o ./nupkgs

# Install the local package into Revela
revela plugin install ./nupkgs/YourName.Revela.Plugin.Example.1.0.0.nupkg
```

Publish to NuGet.org:

```bash
dotnet nuget push ./nupkgs/YourName.Revela.Plugin.Example.*.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_NUGET_API_KEY
```

Or automate it with GitHub Actions on a tag push:

```yaml
name: Release Plugin
on:
  push:
    tags: ['v*']
jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet pack -c Release -o ./nupkgs -p:PackageVersion=${GITHUB_REF_NAME#v}
      - run: dotnet nuget push "./nupkgs/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate
```

> Store your NuGet API key as a repository secret (`NUGET_API_KEY`), never in source.

Any other NuGet v3 feed works too (GitHub Packages, Azure Artifacts, a self-hosted server), as long as it is served over `https://`: Revela refuses plain `http://` package sources except on `localhost`, because a plugin is code that runs on the user's machine. See [Security Model](security-model.md#plugin-trust).

---

## Best practices

- ✅ Use your own package prefix (`YourName.Revela.Plugin.*`).
- ✅ Depend only on `Spectara.Revela.Sdk` abstractions, never on Revela internals.
- ✅ Use `TryAdd*` in `ConfigureServices` to stay idempotent.
- ✅ Version with SemVer; pre-release tags (`-beta.1`) are never auto-installed.
- ✅ Validate every user-supplied URL with `UrlSafety` before fetching.
- ✅ Read your version with `PackageVersion.FromAssembly` and the host version from `IBuildInfo`.
- ✅ Gate prompts on `IConsoleCapabilities.IsInteractive` and live output on `CanRenderLive`.
- ❌ Don't use the reserved `Spectara` prefix.
- ❌ Don't `new HttpClient()` (socket exhaustion) or cache it in a singleton (stale DNS).
- ❌ Don't hardcode paths or assume a specific directory layout.

---

## See also

- [Architecture](architecture.md) — ownership boundaries and package lifecycle
- [Security Model](security-model.md) — trust assumptions and URL-safety rationale
- [Development Guide](development.md) — building and testing Revela itself
