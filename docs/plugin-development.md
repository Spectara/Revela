# Plugin Development

A practical guide to building, configuring, testing, and publishing a Revela plugin.

> For the *why* behind the plugin system (architecture, layers, what stays internal), see [Plugin System](plugin-system-v2.md).
> For the security rationale behind URL validation and trust, see [Security Model](security-model.md).

---

## What a plugin is

Revela is extensible through a **NuGet-based plugin system**. A plugin is a small .NET library that implements `IPlugin`, registers its services, and optionally contributes CLI commands.

- **Implement `IPlugin`** — metadata, service registration, and commands in one class.
- **Config via `IOptions`** — JSON and environment variables are auto-loaded for you.
- **Ship on NuGet** — pack and publish; users install with `revela plugin install`.

---

## Naming & trust

| Audience | Package prefix | Notes |
|----------|----------------|-------|
| **Official** | `Spectara.Revela.Plugins.*` | Reserved on NuGet.org, Spectara only |
| **Community** | `YourName.Revela.Plugin.*` | Your own prefix; install at your own risk |

> The `Spectara` prefix is reserved on NuGet.org and cannot be used by third parties.

The package **name** is only a convention. Revela detects a plugin from the `<PackageType>RevelaPlugin</PackageType>` marker in your project file (see the `.csproj` below), not from the ID. Pick any prefix you control; including a `.Revela.Plugin.` (or `.Revela.Plugins.`) segment also helps Revela classify your package in `revela plugin search` results before it is installed.

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
    public PluginMetadata Metadata => new()
    {
        Name = "Example",
        Version = "1.0.0",
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

    <PackageId>YourName.Revela.Plugin.Example</PackageId>
    <PackageType>RevelaPlugin</PackageType>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Example plugin for Revela</Description>
    <PackageTags>revela;plugin;example</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
    <!-- Public plugin/theme abstractions (IPlugin, CommandDescriptor, …) -->
    <PackageReference Include="Spectara.Revela.Sdk" Version="1.0.0" />
    <PackageReference Include="System.CommandLine" Version="2.0.8" />
  </ItemGroup>
</Project>
```

---

## Configuration

Revela auto-loads plugin configuration before your plugin initializes, pulling from `project.json` (and any `plugins/*.json`) plus environment variables prefixed `SPECTARA__REVELA__`. You usually don't override `ConfigureConfiguration` at all.

Bind a strongly-typed options class whose section name is your package ID:

```csharp
public sealed class ExampleConfig
{
    public const string SectionName = "YourName.Revela.Plugin.Example";

    [Required]
    public string ApiUrl { get; init; } = string.Empty;
    public int Timeout { get; init; } = 30;
}

// in ConfigureServices
services.AddOptions<ExampleConfig>()
    .BindConfiguration(ExampleConfig.SectionName)
    .ValidateDataAnnotations();
```

Inject it with `IOptionsMonitor<ExampleConfig>` and read `.CurrentValue`. Users configure it in `project.json`:

```json
{
  "YourName.Revela.Plugin.Example": {
    "ApiUrl": "https://api.example.com",
    "Timeout": 30
  }
}
```

**Which config source for which use case?**
- Own plugin config (e.g. `calendar.locale`): `[RevelaConfig("my.plugin")]` + `IOptions<MyPluginConfig>`
- Build/hosting settings (base URL, subpath, output path): `IOptions<ProjectConfig>`
- Site identity (title, description, author, language): `IOptions<SiteCoreConfig>`
- Theme-specific site properties: not from plugins — that's theme territory

---

## Making HTTP calls (the typed-client pattern)

If your plugin makes HTTP calls (syncing from a cloud source, fetching a feed, calling an API), use the **typed client** pattern, which is the approach Microsoft recommends. It gives you connection pooling, DNS-aware handler rotation, per-service configuration, and easy testing.

### Register a typed client

Register the client in your plugin's `ConfigureServices`. Configure the timeout and headers once:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddHttpClient<ExampleService>(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.BaseAddress = new Uri("https://api.example.com");
        client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0");
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

### Advanced

```csharp
services.AddHttpClient<ExampleService>(client => { /* … */ })
    .SetHandlerLifetime(TimeSpan.FromMinutes(10));   // default is 2 minutes
```

You can also chain `.AddPolicyHandler(...)` for retries or `.ConfigurePrimaryHttpMessageHandler(...)` for custom handler settings.

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

For HTTP code, mock the transport, not the typed client, by injecting a fake handler:

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

---

## Best practices

- ✅ Use your own package prefix (`YourName.Revela.Plugin.*`).
- ✅ Depend only on `Spectara.Revela.Sdk` abstractions, never on Revela internals.
- ✅ Use `TryAdd*` in `ConfigureServices` to stay idempotent.
- ✅ Version with SemVer; pre-release tags (`-beta.1`) are never auto-installed.
- ✅ Validate every user-supplied URL with `UrlSafety` before fetching.
- ❌ Don't use the reserved `Spectara` prefix.
- ❌ Don't `new HttpClient()` (socket exhaustion) or cache it in a singleton (stale DNS).
- ❌ Don't hardcode paths or assume a specific directory layout.

---

## See also

- [Plugin System](plugin-system-v2.md) — architecture and design of the plugin system
- [Security Model](security-model.md) — trust assumptions and URL-safety rationale
- [Development Guide](development.md) — building and testing Revela itself
