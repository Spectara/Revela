# Plugin Development Guide

## Creating Plugins for Revela

Revela supports extensibility through a NuGet-based plugin system.

---

## 🔐 Official vs. Community Plugins

### Official Plugins (Spectara-Maintained)

**Package Prefix:** `Spectara.Revela.Plugin.*`

- ✅ Maintained by Spectara team
- ✅ Verified and trusted
- ✅ Official support
- ✅ Regular updates

**Example:** `Spectara.Revela.Plugin.Deploy`

### Community Plugins

**Package Prefix:** `YourName.Revela.Plugin.*` or `YourOrg.Revela.Plugin.*`

- ⚠️ Maintained by community developers
- ⚠️ Not officially verified
- ⚠️ Install at your own risk
- ⚠️ Support by plugin author

**Example:** `JohnDoe.Revela.Plugin.AWS`

**Important:** The `Spectara` prefix is **reserved** on NuGet.org and cannot be used by third parties.

---

## 🏗️ Plugin Architecture

### IPlugin Interface

All plugins must implement the `IPlugin` interface:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Core.Abstractions;

namespace YourName.Revela.Plugin.Example;

public class ExamplePlugin : IPlugin
{
    private IServiceProvider? _services;
    
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "Example",
        Version = "1.0.0",
        Description = "Example plugin for Revela",
        Author = "Your Name",
        ParentCommand = "example" // Optional: parent command name
    };
    
    // 1. ConfigureServices - Register services BEFORE ServiceProvider is built
    public void ConfigureServices(IServiceCollection services)
    {
        // Register plugin-specific services
        services.AddHttpClient<MyHttpService>(client =>
        {
            client.BaseAddress = new Uri("https://api.example.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        
        services.AddSingleton<IMyService, MyService>();
    }
    
    // 2. Initialize - Called AFTER ServiceProvider is built
    public void Initialize(IServiceProvider services)
    {
        _services = services;
        // Perform initialization that requires resolved services
    }
    
    // 3. GetCommands - Return CLI commands
    public IEnumerable<Command> GetCommands()
    {
        if (_services == null)
            throw new InvalidOperationException("Plugin not initialized");
        
        yield return CreateExampleCommand();
    }
    
    private Command CreateExampleCommand()
    {
        var command = new Command("example", "Example command");
        
        command.SetAction(parseResult =>
        {
            var myService = _services.GetService<IMyService>();
            myService?.DoSomething();
            return 0;
        });
        
        return command;
    }
}
```

---

## 📦 Plugin Project Structure

```
YourName.Revela.Plugin.Example/
├── YourName.Revela.Plugin.Example.csproj
├── ExamplePlugin.cs
├── Commands/
│   ├── ExampleCommand.cs
│   └── ExampleInitCommand.cs
├── Templates/
│   └── config.json            # Embedded as resource
└── README.md
```

---

## 🎯 Plugin .csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    
    <!-- Package Metadata -->
    <PackageId>YourName.Revela.Plugin.Example</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Example plugin for Revela</Description>
    <PackageTags>revela;plugin;example</PackageTags>
    <PackageProjectUrl>https://github.com/yourname/revela-plugin-example</PackageProjectUrl>
    <RepositoryUrl>https://github.com/yourname/revela-plugin-example</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference Revela.Core for abstractions -->
    <PackageReference Include="Spectara.Revela.Core" Version="1.0.0" />
    
    <!-- System.CommandLine for CLI -->
    <PackageReference Include="System.CommandLine" Version="2.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Embed template files as resources -->
    <EmbeddedResource Include="Templates\**\*" />
  </ItemGroup>
</Project>
```

---

## 🚀 Plugin Workflow

### 1. User Installs Plugin

```bash
revela plugin install yourname.example
```

Revela expands short name to: `YourName.Revela.Plugin.Example`

### 2. Plugin is Loaded

- Plugin DLL is downloaded to `%APPDATA%/Revela/plugins/`
- Revela discovers plugins matching pattern: `*.Revela.Plugin.*.dll`
- Plugin's `IPlugin` implementation is instantiated

### 3. Plugin Initializes

```csharp
public void Initialize(IServiceProvider services)
{
    // Access DI container
    var logger = services.GetService<ILogger<ExamplePlugin>>();
    logger.LogInformation("Example plugin initialized");
}
```

### 4. Commands are Registered

Plugin commands are added to Revela's CLI:

```bash
revela example --help
```

---

## 💾 Plugin Configuration

### Framework Auto-Load

The Revela framework automatically loads:
1. All `plugins/*.json` files from the working directory
2. Environment variables with prefix `SPECTARA__REVELA__`

This happens **before** plugin initialization, so plugins don't need to register config sources.

**Default filename:** Use the full Package-ID to avoid conflicts.

Example: `plugins/YourName.Revela.Plugin.Example.json`

```json
{
  "YourName.Revela.Plugin.Example": {
    "ApiUrl": "https://api.example.com",
    "Timeout": 30
  }
}
```

### ConfigureConfiguration - Usually Empty

Since JSON files and ENV vars are auto-loaded, plugins typically don't need to do anything:

```csharp
public void ConfigureConfiguration(IConfigurationBuilder configuration)
{
    // Nothing to do - framework handles everything:
    // - JSON files: auto-loaded from plugins/*.json
    // - ENV vars: auto-loaded with SPECTARA__REVELA__ prefix
}
```

### Using IOptions Pattern

```csharp
// Config model - SectionName = Package-ID (no prefix)
public sealed class ExampleConfig
{
    public const string SectionName = "YourName.Revela.Plugin.Example";
    
    [Required]
    public string ApiUrl { get; init; } = string.Empty;
    
    public int Timeout { get; init; } = 30;
}

// In ConfigureServices
public void ConfigureServices(IServiceCollection services)
{
    services.AddOptions<ExampleConfig>()
        .BindConfiguration(ExampleConfig.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();  // Fail-fast at startup
}
```

### Environment Variables

ENV vars are mapped using double-underscore as separator:

```bash
# For config section "YourName.Revela.Plugin.Example"
SPECTARA__REVELA__YOURNAME_REVELA_PLUGIN_EXAMPLE__APIURL=https://...
```

### Providing Config Templates

Plugins can include embedded config templates:

```csharp
// In plugin's init command
public class ExampleInitCommand
{
    private const string DefaultFileName = "YourName.Revela.Plugin.Example.json";
    
    private static void Execute(string? customName)
    {
        var fileName = string.IsNullOrWhiteSpace(customName)
            ? DefaultFileName
            : customName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? customName
                : $"{customName}.json";
        
        var assembly = typeof(ExamplePlugin).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "YourName.Revela.Plugin.Example.Templates.config.json");
        
        if (stream == null)
            throw new FileNotFoundException("Config template not found");
        
        Directory.CreateDirectory("plugins");
        using var fileStream = File.Create(Path.Combine("plugins", fileName));
        stream.CopyTo(fileStream);
        
        Console.WriteLine($"✨ Example plugin configured: plugins/{fileName}");
    }
}
```

### Loading Config

Config is automatically loaded via IOptions. Simply inject it:

```csharp
public class ExampleCommand(IOptionsMonitor<ExampleConfig> config)
{
    public void Execute()
    {
        var current = config.CurrentValue;
        Console.WriteLine($"API URL: {current.ApiUrl}");
    }
}
```

---

## 📚 Best Practices

### ✅ DO

- ✅ Use your own NuGet prefix (`YourName.Revela.Plugin.*`)
- ✅ Implement `IPlugin` interface
- ✅ Provide an `init` command to create config files
- ✅ Include XML documentation
- ✅ Add a README with usage instructions
- ✅ Version your plugin semantically (SemVer)
- ✅ Test with different Revela versions

### ❌ DON'T

- ❌ Use `Spectara` prefix (reserved!)
- ❌ Access Revela internals (use abstractions only)
- ❌ Assume specific directory structures
- ❌ Hard-code paths
- ❌ Skip error handling
- ❌ Forget to document your plugin

---

## 🧪 Testing Your Plugin

### Local Testing

1. Build your plugin:
   ```bash
   dotnet build -c Release
   ```

2. Copy DLL to Revela plugins directory:
   ```bash
   copy bin/Release/net10.0/YourName.Revela.Plugin.Example.dll %APPDATA%/Revela/plugins/
   ```

3. Test with Revela:
   ```bash
   revela plugin list
   revela example --help
   ```

### Unit Testing

```csharp
[TestClass]
public class ExamplePluginTests
{
    [TestMethod]
    public void Plugin_ShouldHaveCorrectMetadata()
    {
        var plugin = new ExamplePlugin();
        
        Assert.AreEqual("Example", plugin.Metadata.Name);
        Assert.AreEqual("1.0.0", plugin.Metadata.Version);
    }
    
    [TestMethod]
    public void Plugin_ShouldProvideCommands()
    {
        var plugin = new ExamplePlugin();
        var commands = plugin.GetCommands().ToList();
        
        Assert.IsNotEmpty(commands);
        Assert.IsTrue(commands.Exists(c => c.Name == "example"));
    }
}
```

---

## 📦 Publishing Your Plugin

### 1. Pack Your Plugin

```bash
dotnet pack -c Release
```

### 2. Publish to NuGet.org

```bash
dotnet nuget push bin/Release/YourName.Revela.Plugin.Example.*.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### 3. Announce Your Plugin

- Add to [Community Plugins Wiki](https://github.com/spectara/revela/wiki/Community-Plugins)
- Share on social media with `#Revela` hashtag
- Create GitHub repository with examples

---

## 🆘 Support

### For Plugin Development Questions

- 📖 [Documentation](https://revela.website/docs)
- 💬 [GitHub Discussions](https://github.com/spectara/revela/discussions)
- 🐛 [Report Issues](https://github.com/spectara/revela/issues)

### For Official Plugin Proposals

Contact Spectara team to discuss official plugin development.

---

## 📄 Example Plugins

### Reference Implementations

- [Spectara.Revela.Plugin.Deploy](https://github.com/spectara/revela-plugin-deploy) - SSH/SFTP deployment
- [Spectara.Revela.Plugin.OneDrive](https://github.com/spectara/revela-plugin-onedrive) - OneDrive sync

---

**Happy Plugin Development!** 🎉

Built with ❤️ by the Spectara community
