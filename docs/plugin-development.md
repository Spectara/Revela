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
using Spectara.Revela.Core.Abstractions;

namespace YourName.Revela.Plugin.Example;

public class ExamplePlugin : IPlugin
{
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "Example",
        Version = "1.0.0",
        Description = "Example plugin for Revela",
        Author = "Your Name"
    };
    
    public void Initialize(IServiceProvider services)
    {
        // Initialize plugin (DI, configuration, etc.)
    }
    
    public IEnumerable<Command> GetCommands()
    {
        // Return CLI commands this plugin provides
        yield return CreateExampleCommand();
    }
    
    private Command CreateExampleCommand()
    {
        var command = new Command("example", "Example command");
        
        command.SetAction(parseResult =>
        {
            Console.WriteLine("Hello from Example Plugin!");
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

### Providing Config Templates

Plugins can include embedded config templates:

```csharp
// In plugin's init command
public class ExampleInitCommand
{
    private static void Execute()
    {
        var assembly = typeof(ExamplePlugin).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "YourName.Revela.Plugin.Example.Templates.config.json");
        
        if (stream == null)
            throw new FileNotFoundException("Config template not found");
        
        Directory.CreateDirectory("plugins");
        using var fileStream = File.Create("plugins/example.json");
        stream.CopyTo(fileStream);
        
        Console.WriteLine("✨ Example plugin configured!");
        Console.WriteLine("Edit plugins/example.json to configure.");
    }
}
```

### Loading Config

```csharp
public void Initialize(IServiceProvider services)
{
    var configPath = Path.Combine("plugins", "example.json");
    if (File.Exists(configPath))
    {
        var json = File.ReadAllText(configPath);
        _config = JsonSerializer.Deserialize<ExampleConfig>(json);
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
        
        plugin.Metadata.Name.Should().Be("Example");
        plugin.Metadata.Version.Should().Be("1.0.0");
    }
    
    [TestMethod]
    public void Plugin_ShouldProvideCommands()
    {
        var plugin = new ExamplePlugin();
        var commands = plugin.GetCommands().ToList();
        
        commands.Should().NotBeEmpty();
        commands.Should().Contain(c => c.Name == "example");
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
