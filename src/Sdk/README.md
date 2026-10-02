# Spectara.Revela.Sdk

SDK for building plugins and themes for [Revela](https://github.com/spectara/revela),
a static site generator for photographers.

This package contains the contracts the Revela host loads (`IPlugin`, `ITheme`),
the extension points plugins can contribute to (commands, pipeline steps, checks,
page templates, setup wizard steps, derived artifacts), configuration helpers and
the compile-time generators for configuration sections and template models.

## Installation

The SDK is **not on NuGet.org** yet. Download `Spectara.Revela.Sdk.<version>.nupkg` from
[GitHub Releases](https://github.com/spectara/revela/releases) into a local folder and add it
from there:

```bash
dotnet add package Spectara.Revela.Sdk --version <version> --source ./revela-packages
```

For repeatable restores, add that folder as a package source in your `NuGet.Config`.
Mark the project as a plugin (or theme) so Revela recognizes the package:

```xml
<PropertyGroup>
    <PackageType>RevelaPlugin</PackageType> <!-- or RevelaTheme -->
</PropertyGroup>
```

## Creating a plugin

A plugin implements `IPlugin`: metadata, service registration (use `TryAdd*` so it
stays idempotent) and, optionally, the commands it contributes.

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Sdk.Abstractions;

namespace MyCompany.Revela.Plugin.Hello;

public sealed class HelloPlugin : IPlugin
{
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "MyCompany.Revela.Plugin.Hello",
        Name = "Hello",
        Version = PackageVersion.FromAssembly(typeof(HelloPlugin).Assembly),
        Description = "Says hello",
        Author = "My Company",
    };

    public void ConfigureServices(IServiceCollection services) =>
        services.TryAddTransient<HelloCommand>();

    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var hello = services.GetRequiredService<HelloCommand>();

        // revela hello — ParentCommand: null registers at the root.
        yield return new CommandDescriptor(hello.Create(), ParentCommand: null, Group: "Addons");
    }
}

internal sealed class HelloCommand
{
    public Command Create()
    {
        var name = new Option<string>("--name", "-n") { Description = "Who to greet" };
        var command = new Command("hello", "Say hello");
        command.Options.Add(name);
        command.SetAction(parseResult =>
        {
            Console.WriteLine($"Hello, {parseResult.GetValue(name) ?? "world"}!");
            return 0;
        });
        return command;
    }
}
```

`PackageVersion.FromAssembly` reports the version your package was built with, so
`revela plugin list` always matches the installed package.

## Plugin configuration

Plugin settings live below `plugins:<key>` in `project.json`. Declare the section with
`[RevelaConfig]` **and** a hand-written `Section` constant; the SDK generator reports
`REVELA001` for an invalid plugin section and `REVELA002` when the two differ. Bind it
in `ConfigureServices` and inject `IOptions<T>` / `IOptionsMonitor<T>`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Sdk.Abstractions;

namespace MyCompany.Revela.Plugin.Greeting;

[RevelaConfig("plugins:greeting")]
public sealed class GreetingConfig
{
    public const string Section = "plugins:greeting";

    // Writable properties: the configuration binding generator skips init-only setters.
    public string Salutation { get; set; } = "Hello";
}

public static class GreetingRegistration
{
    public static void AddGreeting(this IServiceCollection services) =>
        services.AddOptions<GreetingConfig>().BindConfiguration(GreetingConfig.Section);
}
```

```json
{ "plugins": { "greeting": { "salutation": "Hi" } } }
```

## Reading the scanned site

Inject `IManifestReader` to read what the last `revela generate scan` found. It returns
`null` when there is no usable scan, so report that the scan must run first:

```csharp
var manifest = await manifestReader.TryLoadAsync(cancellationToken);
if (manifest is null)
{
    return OperationResult.Fail("Manifest not found — run 'revela generate scan' first");
}

var imageCount = manifest.Images.Count;   // keyed by source path
var root = manifest.Root;                 // page tree (home page first)
```

## Creating a theme

A theme is an assembly with its files as embedded resources and a `manifest.json`.
Derive from `EmbeddedTheme`; metadata, file access and extraction come from the base class.
The same base class serves theme extensions (`targetTheme` and `prefix` in the manifest).

```csharp
using Spectara.Revela.Sdk.Themes;

namespace MyCompany.Revela.Themes.Minimal;

public sealed class MinimalTheme() : EmbeddedTheme(typeof(MinimalTheme).Assembly);
```

## Template models

The SDK package includes the compile-time generator for `[RevelaTemplateModel]`. It
emits trim-safe `ToScriptObject()` / `ToScriptArray()` extensions in
`Spectara.Revela.Sdk.TemplateModels`; public properties become snake_case keys,
`[ScriptName("custom")]` overrides a key and `[ScriptIgnore]` skips a property.
Generated code uses Scriban types, so reference Scriban explicitly (the SDK does not
add it as a runtime dependency):

```xml
<ItemGroup>
    <PackageReference Include="Spectara.Revela.Sdk" Version="0.0.1-beta.21" />
    <PackageReference Include="Scriban" Version="7.4.0" />
</ItemGroup>
```

```csharp
using Scriban;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.TemplateModels;

namespace MyCompany.Revela.Plugin.Gallery;

[RevelaTemplateModel]
public sealed class GalleryModel
{
    public required string DisplayName { get; init; }

    [ScriptName("photos")]
    public int PhotoCount { get; init; }

    [ScriptIgnore]
    public string InternalId { get; init; } = "";
}

public static class GalleryRendering
{
    // Renders "Alpine Gallery (12)".
    public static string Render(GalleryModel model)
    {
        var context = new TemplateContext();
        context.PushGlobal(model.ToScriptObject());
        return Template.Parse("{{ display_name }} ({{ photos }})").Render(context);
    }
}
```

Models do not need to be `partial`; the generator creates separate extension classes
in the consuming assembly.

## Documentation

- [Plugin Development Guide](https://github.com/spectara/revela/blob/main/docs/plugin-development.md) —
  commands, pipeline steps, checks, page templates, wizard steps, derived artifacts,
  HTTP clients, console output and testing
- [Architecture](https://github.com/spectara/revela/blob/main/docs/architecture.md)

## License

MIT License - see the main [Revela repository](https://github.com/spectara/revela) for details.
