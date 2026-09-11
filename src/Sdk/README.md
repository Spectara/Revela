# Spectara.Revela.Sdk

SDK for building Revela plugins and themes.

## Overview

This package provides the interfaces, models, and base classes needed to create plugins and themes for [Revela](https://github.com/spectara/revela), a modern static site generator for photographers.

## Installation

```bash
dotnet add package Spectara.Revela.Sdk
```

## Creating a Plugin

```csharp
using Spectara.Revela.Sdk.Abstractions;

public class MyPlugin : IPlugin
{
    public PluginMetadata Metadata => new()
    {
        Name = "My Plugin",
        Version = "1.0.0",
        Description = "My custom plugin",
        Author = "Your Name"
    };

    public void ConfigureServices(IServiceCollection services)
    {
        // Register your services
    }

    // Optional: Override to provide CLI commands
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Return your CLI commands
        yield break;
    }
}
```

## Creating a Theme

```csharp
using Spectara.Revela.Sdk.Themes;

public class MyTheme : EmbeddedThemePlugin
{
    public override ThemeMetadata Metadata => new()
    {
        Name = "My Theme",
        Version = "1.0.0",
        Description = "My custom theme",
        Author = "Your Name"
    };

    protected override Assembly ResourceAssembly => typeof(MyTheme).Assembly;
    protected override string ResourcePrefix => "MyTheme.Resources";
}
```

## Template Model Generation

The SDK package includes the compile-time generator for `[RevelaTemplateModel]`.
NuGet loads it automatically; no generator package, project reference, or manual
analyzer registration is needed. Roslyn assemblies are not runtime dependencies.

Generated conversions return Scriban's `ScriptObject` and `ScriptArray` types.
Consumers using this feature must reference Scriban explicitly; the SDK does not
add Scriban as a runtime dependency. For a .NET 10 / C# 14 project:

```xml
<ItemGroup>
    <PackageReference Include="Spectara.Revela.Sdk" Version="0.0.1-beta.21" />
    <PackageReference Include="Scriban" Version="7.4.0" />
</ItemGroup>
```

Import `Spectara.Revela.Sdk.TemplateModels` to call the generated extensions:

```csharp
using Scriban;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.TemplateModels;

var model = new GalleryModel { DisplayName = "Alpine Gallery" };
var scriptObject = model.ToScriptObject();
var context = new TemplateContext();
context.PushGlobal(scriptObject);
var rendered = Template.Parse("{{ display_name }}").Render(context);
Console.WriteLine(rendered);

[RevelaTemplateModel]
public sealed class GalleryModel
{
        public required string DisplayName { get; init; }
}
```

This renders `Alpine Gallery`. Models do not need to be `partial`: the generator
creates separate extension classes in the consumer assembly. Public properties
are projected with direct access and snake_case keys; `[ScriptName("custom")]`
overrides a key and `[ScriptIgnore]` excludes a property.

## Documentation

- [Plugin Development Guide](https://revela.website/docs/developers/plugin-development/)
- [Theme Customization Guide](https://revela.website/docs/guide/themes/)

## License

MIT License - see the main [Revela repository](https://github.com/spectara/revela) for details.
