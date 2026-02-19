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

## Documentation

- [Plugin Development Guide](https://github.com/spectara/revela/blob/main/docs/plugin-development.md)
- [Theme Development Guide](https://github.com/spectara/revela/blob/main/docs/theme-development.md)

## License

MIT License - see the main [Revela repository](https://github.com/spectara/revela) for details.
