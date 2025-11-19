# GitHub Copilot - Revela Context

**Purpose:** Help GitHub Copilot understand this project better in future sessions.

---

## Project Overview

**Website:** https://revela.website

**Revela** is a modern static site generator for photographers, built with .NET 10.

This is a **complete rewrite** of the original Bash-based revela project:
- **Original Project:** https://github.com/kirkone/Expose
- **Original Language:** Bash/Shell scripts
- **New Implementation:** .NET 10 / C# 14

### Migration Goals
- **Keep the same output** - Generated sites should look identical
- **Improve performance** - NetVips is 3-5× faster than the original
- **Add extensibility** - Plugin system for optional features
- **Cross-platform** - Works on Windows, Linux, macOS
- **Modern tooling** - .NET ecosystem, IDE support

### Key Characteristics
- **Target:** Photographers wanting fast, beautiful portfolio sites
- **Focus:** Performance (large images), simplicity, extensibility
- **Architecture:** Vertical Slice + Plugin System
- **Technology:** .NET 10, NetVips, Scriban, System.CommandLine 2.0

### Differences from Original
| Aspect | Original (Bash) | Revela |
|--------|-----------------|------------|
| **Config** | Bash variables | JSON + IConfiguration |
| **Templates** | Mustache-Light (regex) | Scriban (full-featured) |
| **Markdown** | Perl script | Markdig (C#) |
| **Images** | VIPS CLI | NetVips (library) |
| **EXIF** | ExifTool CLI | NetVips built-in |
| **Plugins** | ❌ None | ✅ NuGet-based |
| **GUI** | ❌ None | ✅ Planned (WPF/MAUI) |

**Important:** The output (HTML, images) should look the same, but the internal structure and files can differ!

---

## Code Style & Conventions

### General
- **Language:** C# 14
- **Framework:** .NET 10
- **Namespaces:** File-scoped (`namespace Revela.Core.Models;`)
- **Nullable:** Enabled globally
- **Async:** Always use `async/await`, include `CancellationToken`

### Naming
- **Private fields:** `_camelCase` (underscore prefix)
- **Public members:** `PascalCase`
- **Async methods:** `MethodNameAsync` (Async suffix)
- **Interfaces:** `IInterfaceName` (I prefix)
- **Constants:** `PascalCase`

### Patterns
- **Configuration:** Options Pattern (`IOptions<T>`)
- **Logging:** `ILogger<T>` (Microsoft.Extensions.Logging)
- **DI:** Constructor injection
- **Commands:** System.CommandLine 2.0 API

### Code Quality
- **XML docs:** Required for public APIs
- **Tests:** MSTest v4 + FluentAssertions + Moq
- **Warnings:** Treat as errors (TreatWarningsAsErrors=true)

---

## Project Structure

```
src/
├── Revela.Core/              # Shared kernel (models, abstractions, plugin system)
├── Revela.Infrastructure/    # External services (NetVips, Scriban, Markdig)
├── Revela.Features/          # Vertical slices (GenerateSite, ManagePlugins)
├── Revela.Cli/               # Entry point (.NET Tool)
└── Revela.Plugins/           # Optional plugins (Deploy, OneDrive)
```

### Key Files
- **Models:** `src/Revela.Core/Models/` - Domain entities
- **Config:** `src/Revela.Core/Configuration/RevelaConfig.cs` - Configuration model
- **Plugins:** `src/Revela.Core/PluginLoader.cs` + `PluginManager.cs`

---

## Important Implementation Details

### 1. Image Processing (NetVips)
```csharp
// NetVips is our image processor
using NetVips;

var image = Image.NewFromFile(path);
var resized = image.ThumbnailImage(width);
resized.WriteToFile(output);
```

### 2. Template Engine (Scriban)
```csharp
// Scriban for templates
using Scriban;

var template = Template.Parse(content);
var result = template.Render(model);
```

### 3. CLI Commands (System.CommandLine 2.0)

**IMPORTANT:** System.CommandLine 2.0 API is different from beta versions!

```csharp
// ✅ CORRECT - System.CommandLine 2.0 API
using System.CommandLine;

// Create command
var command = new Command("mycommand", "Description of command");

// Create option with aliases
var option = new Option<string>("--name", "-n")
{
    Description = "Option description",
    Required = false  // Optional: make required
};

// Add option to command
command.Options.Add(option);

// Set action handler
command.SetAction(parseResult =>
{
    var value = parseResult.GetValue(option);
    
    // Execute logic
    Console.WriteLine($"Value: {value}");
    
    return 0; // Exit code
});

// Add subcommands
command.Subcommands.Add(subCommand);

// Parse and invoke
var rootCommand = new RootCommand("Description");
rootCommand.Subcommands.Add(command);
return rootCommand.Parse(args).Invoke();
```

**Command Registration Pattern:**
- **Core Commands:** Manual registration in Program.cs (3-5 commands)
  ```csharp
  rootCommand.Subcommands.Add(InitCommand.Create());
  rootCommand.Subcommands.Add(GenerateCommand.Create());
  ```
- **Plugin Commands:** Dynamic loading via PluginLoader (Reflection)
  ```csharp
  var plugins = pluginLoader.GetLoadedPlugins();
  foreach (var plugin in plugins)
  {
      plugin.Initialize(serviceProvider);
      foreach (var cmd in plugin.GetCommands())
          rootCommand.Subcommands.Add(cmd);
  }
  ```

**Reason:** Core commands are stable and few, plugins need dynamic discovery

### 4. Plugin Interface
```csharp
// All plugins implement IPlugin
public interface IPlugin
{
    IPluginMetadata Metadata { get; }
    void Initialize(IServiceProvider services);
    IEnumerable<Command> GetCommands();
}
```

### 5. Configuration
```csharp
// Options Pattern
public class RevelaConfig
{
    public ProjectSettings Project { get; init; }
    public SiteSettings Site { get; init; }
    public BuildSettings Build { get; init; }
}

// Usage via DI
public class MyService
{
    private readonly RevelaConfig _config;
    
    public MyService(IOptions<RevelaConfig> options)
    {
        _config = options.Value;
    }
}
```

---

## Common Tasks

### Adding a New Model
Location: `src/Revela.Core/Models/`

```csharp
namespace Revela.Core.Models;

/// <summary>
/// Description of the model
/// </summary>
public sealed class MyModel
{
    public required string Name { get; init; }
    public List<Item> Items { get; init; } = [];
}
```

### Adding a New Command
Location: `src/Revela.Features/{FeatureName}/`

```csharp
namespace Revela.Features.MyFeature;

public static class MyCommand
{
    public static Command Create(IServiceProvider services)
    {
        var command = new Command("mycommand", "Description");
        
        var option = new Option<string>("--name");
        command.AddOption(option);
        
        command.SetHandler(async (name, ct) =>
        {
            // Implementation
            await Task.CompletedTask;
        }, option);
        
        return command;
    }
}
```

### Adding a New Service
Location: `src/Revela.Infrastructure/`

```csharp
namespace Revela.Infrastructure.MyService;

public interface IMyService
{
    Task DoSomethingAsync(int count, CancellationToken cancellationToken = default);
}

public sealed partial class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    public async Task DoSomethingAsync(int count, CancellationToken cancellationToken = default)
    {
        LogProcessingStarted(_logger, count);
        await Task.CompletedTask;
    }
    
    // High-performance logging (LoggerMessage source generator)
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing {Count} items")]
    private static partial void LogProcessingStarted(ILogger logger, int count);
}
```

### Adding Tests
Location: `tests/Revela.Core.Tests/`

```csharp
namespace Revela.Core.Tests;

[TestClass]
public sealed class MyServiceTests
{
    [TestMethod]
    public async Task DoSomethingAsync_ShouldProcessItems()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MyService>>();
        var service = new MyService(logger);
        
        // Act
        await service.DoSomethingAsync(10);
        
        // Assert (using FluentAssertions)
        result.Should().Be(expected);
        
        // Verify logger was called (NSubstitute)
        logger.Received().LogInformation(Arg.Any<string>());
    }
}
```

### Registering Services
Location: `src/Revela.Features/{FeatureName}/ServiceCollectionExtensions.cs`

```csharp
namespace Revela.Features.MyFeature;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyFeature(this IServiceCollection services)
    {
        services.AddScoped<IMyService, MyService>();
        return services;
    }
}
```

---

## Dependencies

### Core Framework
- `Microsoft.Extensions.Hosting` (10.0.0)
- `Microsoft.Extensions.Configuration.Json` (10.0.0)
- `Microsoft.Extensions.Logging` (10.0.0)

### CLI
- `System.CommandLine` (2.0.0) - **FINAL release, not beta!**
- `Spectre.Console` (0.49.1) - Rich console output (progress bars, tables, panels)

### Image Processing
- `NetVips` (3.1.0)
- `NetVips.Native` (8.17.3)

### Templating
- `Scriban` (6.5.1)
- `Markdig` (0.43.0)

### Logging
- `Microsoft.Extensions.Logging` (10.0.0) - Built-in logging
- `Microsoft.Extensions.Logging.Console` (10.0.0)
- `Microsoft.Extensions.Logging.Debug` (10.0.0)

### Plugin Management
- `NuGet.Protocol` (7.0.0)
- `NuGet.Packaging` (7.0.0)
- `NuGet.Configuration` (7.0.0)

### Testing
- `MSTest` (4.0.2) - Modern test framework with Microsoft.Testing.Platform
- `MSTest.Analyzers` (4.0.2)
- `FluentAssertions` (8.8.0)
- `NSubstitute` (5.3.0) - Mocking framework (preferred over Moq due to security concerns)
- `coverlet.collector` (6.0.4) - Code coverage

**Note:** All versions centrally managed in `Directory.Packages.props`

---

## Build & Test

### Build
```bash
dotnet restore
dotnet build
```

### Test
```bash
# MSTest v4 with Microsoft.Testing.Platform
# Run as executables (recommended for .NET 10)
dotnet run --project tests/Revela.Core.Tests
dotnet run --project tests/Revela.IntegrationTests

# Alternative: Using artifacts
dotnet artifacts/bin/Revela.Core.Tests/Debug/net10.0/Revela.Core.Tests.dll
```

### Run CLI
```bash
dotnet run --project src/Revela.Cli -- --help
dotnet run --project src/Revela.Cli -- generate -p samples/example-site
```

### Package as Tool
```bash
dotnet pack src/Revela.Cli -c Release
dotnet tool install -g --add-source ./artifacts/packages Expose
```

---

## Known Issues & TODOs

### Current Build Issues
- Code style warnings (CA1848, IDE0055) due to TreatWarningsAsErrors=true
- LoggerMessage delegates need implementation
- Formatting issues in PluginLoader/PluginManager

### Immediate Next Steps
1. Implement NetVipsImageProcessor
2. Implement ScribanTemplateEngine
3. Create GenerateCommand
4. Wire up Program.cs with Hosting

---

## Context for AI Assistants

### When Starting New Conversation
1. Read `DEVELOPMENT.md` for current status
2. Read `docs/architecture.md` for design decisions
3. Check open files in IDE for current work
4. **CHECK FOR DEPENDENCY UPDATES** - Run `dotnet outdated` proactively
5. If updates available, inform user and suggest update strategy

### Dependency Management (IMPORTANT!)
**Always check for package updates at session start!**

```bash
# Check for outdated packages
dotnet outdated

# If updates found:
# 1. Inform user about available updates
# 2. Categorize by severity (Patch/Minor/Major)
# 3. Recommend update strategy (see .github/DEPENDENCY_MANAGEMENT.md)
# 4. Highlight security-critical updates
```

**Update Strategy:**
- ✅ **Patch Updates (x.x.X)** - Always safe, recommend immediate update
- 🟡 **Minor Updates (x.X.x)** - Usually safe, recommend with testing
- 🔴 **Major Updates (X.x.x)** - Breaking changes possible, recommend careful review

**Security Updates:** ALWAYS highlight and recommend immediate action!

**Automated Checks:**
- Weekly GitHub Action runs every Monday 6:00 UTC
- Creates GitHub Issues for available updates
- See `.github/workflows/dependency-update-check.yml`

**Manual Update Commands:**
```bash
# Safe updates (patch only)
dotnet outdated -u --version-lock Major

# Patch + Minor updates
dotnet outdated -u --version-lock Minor

# Interactive selection
dotnet outdated -u:prompt
```

**After Updates:**
```bash
dotnet restore
dotnet build
dotnet run --project tests/Core.Tests
```

**Documentation:** See `.github/DEPENDENCY_MANAGEMENT.md` for full details

### Code Style Rules (IMPORTANT!)

**EditorConfig Decisions:**
- **using directive placement:** `outside_namespace:warning` (Microsoft C# 10 Standard)
  ```csharp
  // ✅ CORRECT
  using System;
  using Spectre.Console;
  
  namespace Revela.Features.Init;
  
  public class MyCommand { }
  ```
- **File-scoped namespaces:** Required (`csharp_style_namespace_declarations = file_scoped:warning`)
- **Reason:** Microsoft C# 10+ best practice, cleaner with file-scoped namespaces

**Code Analysis - Microsoft Only:**
- **Microsoft.CodeAnalysis.NetAnalyzers:** Built-in via .NET SDK (1000+ CA-Rules)
- **NO third-party analyzers:** StyleCop, Roslynator removed (not maintained, beta packages)
- **Configuration:** 
  - `EnableNETAnalyzers=true`
  - `AnalysisLevel=latest-all` 
  - `EnforceCodeStyleInBuild=true`
- **Fine-tuning:** `.editorconfig` with `dotnet_diagnostic.CAxxxx.severity`

### When Generating Code
- Follow .editorconfig rules (especially `using` placement!)
- Use file-scoped namespaces (always!)
- Add XML documentation
- Include cancellation token parameters
- Use primary constructors where appropriate (C# 14)
- Prefer collection expressions `[]` over `new List<>()`
- **Use LoggerMessage source generator** for high-performance logging (mark class `partial`)

### When Suggesting Changes
- Explain WHY (architecture/performance/maintainability)
- Reference existing patterns in codebase
- Consider backward compatibility
- Think about testability

---

**Last Updated:** 2025-01-20

**For detailed architecture, see:** `docs/architecture.md`  
**For development status, see:** `DEVELOPMENT.md`  
**For dependency management, see:** `.github/DEPENDENCY_MANAGEMENT.md`




