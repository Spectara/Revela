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
- **Status:** Pre-release, no public users yet

### Breaking Changes Policy
> ⚠️ **No backward compatibility required!**
> 
> This project has **no users yet**. Feel free to:
> - Rename classes, methods, properties without migration
> - Change configuration formats (JSON structure, property names)
> - Restructure folders and namespaces
> - Remove deprecated code immediately
> - Change CLI command names and options
> 
> **Don't waste time on:** Migration scripts, deprecation warnings, or compatibility layers.
> **Focus on:** Clean design, best practices, and getting it right the first time.

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
- **Namespaces:** File-scoped (`namespace Spectara.Revela.Core.Models;`)
- **Nullable:** Enabled globally
- **Async:** Always use `async/await`, include `CancellationToken`

### Naming
- **Private instance fields:** `camelCase` (NO underscore!)
- **Const fields:** `PascalCase`
- **Static readonly fields:** `PascalCase`
- **Public members:** `PascalCase`
- **Async methods:** `MethodNameAsync` (Async suffix)
- **Interfaces:** `IInterfaceName` (I prefix)
- **Local constants:** `camelCase`
- **Parameters & locals:** `camelCase`

### Patterns
- **Configuration:** Options Pattern (`IOptions<T>`)
- **Logging:** `ILogger<T>` (Microsoft.Extensions.Logging)
- **DI:** Constructor injection with Primary Constructors (C# 12)
- **Commands:** Instance classes with DI (System.CommandLine 2.0 API)

### Code Quality
- **XML docs:** Required for public APIs
- **Tests:** MSTest v4 + NSubstitute (built-in assertions)
- **Warnings:** Treat as errors (TreatWarningsAsErrors=true)

---

## Project Structure

```
src/
├── Core/                     # Shared kernel (services, plugin loading, configuration)
├── Commands/                 # CLI commands (Generate, Clean, Config, Plugins, etc.)
├── Cli/                      # Entry point, hosting, interactive mode
├── Sdk/                      # SDK for plugin development (abstractions, models, services)
├── Plugins/
│   ├── Plugin.Compress/      # Gzip/Brotli pre-compression
│   ├── Plugin.Serve/         # Local development server
│   ├── Plugin.Source.OneDrive/  # OneDrive shared folder source
│   └── Plugin.Statistics/    # EXIF statistics functionality
└── Themes/
    ├── Theme.Lumina/         # Default photography portfolio theme
    └── Theme.Lumina.Statistics/  # Statistics extension for Lumina theme
tests/
├── Core.Tests/               # Unit tests for Core
├── Commands.Tests/           # Unit tests for Commands
├── IntegrationTests/         # Integration tests
├── Plugin.Compress.Tests/    # Compression plugin tests
├── Plugin.Serve.Tests/       # Serve plugin tests
├── Plugin.Source.OneDrive.Tests/  # OneDrive plugin tests
├── Plugin.Statistics.Tests/  # Statistics plugin tests
└── Shared/                   # Shared test utilities
```

### Key Files
- **Plugin Abstractions:** `src/Sdk/Abstractions/` - IPlugin, IGenerateStep, IWizardStep, CommandDescriptor
- **Models:** `src/Sdk/Models/Manifest/` - ImageContent, GalleryContent, ManifestMeta
- **Config:** `src/Core/Configuration/` + `src/Sdk/Configuration/` - Configuration models
- **Plugin Loading:** `src/Core/PluginLoader.cs` + `PluginManager.cs`
- **Path Resolution:** `src/Sdk/Services/IPathResolver.cs` + `PathResolver.cs`
- **Output Helpers:** `src/Sdk/Output/OutputMarkers.cs`, `src/Sdk/ProjectPaths.cs`
- **CLI Hosting:** `src/Cli/Hosting/` - HostBuilderExtensions, ProjectResolver, InteractiveMenuService

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
- **Core Commands:** Registered via `UseRevelaCommands()` extension
  ```csharp
  return host.UseRevelaCommands().Parse(args).Invoke();
  ```
- **Plugin Commands:** Automatic via `AddPlugins()` (registered in `UseRevelaCommands()`)
  ```csharp
  builder.Services.AddPlugins(builder.Configuration, filteredArgs);
  ```

**Reason:** All commands resolved from DI, unified registration via extension method

### 4. Plugin System with Host.CreateApplicationBuilder

**MODERN PATTERN:** Use `Host.CreateApplicationBuilder` for full .NET hosting features!

#### Complete Program.cs Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;

// ✅ Standalone mode: Resolve project BEFORE building host
var (projectPath, filteredArgs, shouldExit) = ProjectResolver.ResolveProject(args);
if (shouldExit) return 1;

// ✅ Detect interactive mode: no arguments AND interactive terminal
var isInteractiveMode = filteredArgs.Length == 0
    && !Console.IsInputRedirected
    && !Console.IsOutputRedirected
    && Environment.UserInteractive;

// ✅ Create builder with correct ContentRootPath
var settings = new HostApplicationBuilderSettings
{
    Args = filteredArgs,
    ContentRootPath = projectPath ?? Directory.GetCurrentDirectory(),
};
var builder = Host.CreateApplicationBuilder(settings);

// ✅ Pre-build: Load configuration and register services
builder.AddRevelaConfiguration();       // revela.json → project.json → logging.json
builder.Services.AddRevelaConfigSections(); // IOptions<T> for all config models
builder.Services.AddRevelaCommands();    // All CLI commands
builder.Services.AddInteractiveMode();   // Interactive menu system
builder.Services.AddPlugins(builder.Configuration, filteredArgs);

// Register ProjectEnvironment (runtime info about project location)
builder.Services.AddOptions<ProjectEnvironment>()
    .Configure<IHostEnvironment>((env, host) => env.Path = host.ContentRootPath);

// ✅ Build host
var host = builder.Build();

// ✅ Post-build: Create CLI and execute
var rootCommand = host.UseRevelaCommands();

// If interactive mode, run menu directly
if (isInteractiveMode)
{
    var interactiveService = host.Services.GetRequiredService<IInteractiveMenuService>();
    interactiveService.RootCommand = rootCommand;
    return await interactiveService.RunAsync(CancellationToken.None);
}

return await rootCommand.Parse(filteredArgs).InvokeAsync();
```

**Key Components:**
- **`ProjectResolver`:** Detects standalone mode, parses `--project` arg, resolves project path before host build
- **`AddRevelaConfiguration()`:** Loads config chain: `revela.json` (global %APPDATA%) → `project.json` (local) → `logging.json`. Note: `site.json` is NOT loaded via IConfiguration — it's loaded dynamically by RenderService.
- **`AddRevelaConfigSections()`:** Registers `IOptions<T>` for ProjectConfig, ThemeConfig, GenerateConfig, PathsConfig, etc. Also registers `IPathResolver`.
- **`AddInteractiveMode()`:** Registers CommandOrderRegistry, CommandGroupRegistry, IInteractiveMenuService
- **`ProjectEnvironment`:** Runtime info — `Path` (project dir), `IsInitialized` (project.json exists)

#### Plugin Lifecycle - 4 Phases

**IMPORTANT:** Plugins have a specific initialization order!

```csharp
public interface IPlugin
{
    IPluginMetadata Metadata { get; }
    
    // 1. ConfigureConfiguration - Called BEFORE BuildServiceProvider
    //    Usually empty - framework handles JSON + ENV loading
    //    NOTE: Plugin config loaded from project.json sections
    //    NOTE: ENV vars auto-loaded with SPECTARA__REVELA__ prefix
    void ConfigureConfiguration(IConfigurationBuilder configuration);
    
    // 2. ConfigureServices - Called BEFORE BuildServiceProvider
    //    Plugin registers services (HttpClient, Commands, IOptions)
    void ConfigureServices(IServiceCollection services);
    
    // 3. Initialize - Called AFTER host.Build()
    //    Plugin receives built ServiceProvider for initialization
    void Initialize(IServiceProvider services);
    
    // 4. GetCommands - Returns CLI command descriptors for registration
    IEnumerable<CommandDescriptor> GetCommands();
}
```

#### AddPlugins() Extension Method

**Automatic Plugin Lifecycle Management:**

```csharp
// Extension method handles all plugin lifecycle phases
public static IPluginContext AddPlugins(
    this IServiceCollection services,
    IConfigurationBuilder configuration,
    string[] args,
    Action<PluginOptions>? configure = null)
{
    // 1. Load plugin assemblies
    var pluginLoader = new PluginLoader(logger);
    pluginLoader.LoadPlugins();
    var plugins = pluginLoader.GetLoadedPlugins();
    
    // 2. Plugin config is loaded from project.json sections
    // Each plugin uses its package ID as section name:
    //   "Spectara.Revela.Plugin.Statistics": { "MaxEntriesPerCategory": 15 }
    // project.json is already loaded by AddRevelaConfiguration()
    
    // 3. Environment variables with prefix override config
    // Allows: SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL=https://...
    
    // 4. Plugins may register additional config sources (optional)
    foreach (var plugin in plugins)
    {
        plugin.ConfigureConfiguration(configuration);
    }
    
    // 5. Plugins register services
    foreach (var plugin in plugins)
    {
        plugin.ConfigureServices(services);
    }
    
    // 6. Return context for Initialize() and RegisterCommands()
    return new PluginContext(plugins);
}
```

**Benefits:**
- ✅ **Configuration:** project.json sections, environment variables (SPECTARA__REVELA__*)
- ✅ **Logging:** Configuration-driven logging levels
- ✅ **Dependency Injection:** Full DI container with all features
- ✅ **IOptions:** Validation, hot-reload, fail-fast
- ✅ **Environment:** Development/Production/Staging support
- ✅ **Clean Code:** 72% less code in Program.cs (130 lines → 36 lines)

#### Parent Command Pattern

Plugins specify **parent command** in `CommandDescriptor`, NOT in metadata:

```csharp
// PluginMetadata has NO ParentCommand - only Name, Version, Description, Author
public IPluginMetadata Metadata => new PluginMetadata
{
    Name = "Source OneDrive",
    Version = "1.0.0",
    Description = "OneDrive shared folder source",
    Author = "Spectara"
};
```

**Plugin returns CommandDescriptor with optional parent, order, and group:**
```csharp
// CommandDescriptor is a record with 6 parameters:
// record CommandDescriptor(Command, ParentCommand?, Order=50, Group?, RequiresProject=true, HideWhenProjectExists=false)

public IEnumerable<CommandDescriptor> GetCommands()
{
    // ✅ ParentCommand specified here, not in metadata!
    // "source" = registered under source (revela source onedrive)
    yield return new CommandDescriptor(
        new Command("onedrive", "OneDrive plugin"),
        ParentCommand: "source",
        Order: 30,
        Group: "Content");
    
    // null = registered at root level (revela mycommand)
    // RequiresProject: false = available without project.json
    yield return new CommandDescriptor(
        new Command("sync", "Sync command"),
        ParentCommand: null,
        RequiresProject: false);
}
```

**Program.cs creates parent automatically:**
- Checks if parent exists
- Creates if missing
- Detects duplicate commands
- Result: `revela source onedrive download`

### 5. HttpClient Pattern (Microsoft Best Practice)

**ALWAYS use Typed Client pattern for plugins!**

```csharp
// Plugin.ConfigureServices() - Register Typed HttpClient
public void ConfigureServices(IServiceCollection services)
{
    services.AddHttpClient<SharedLinkProvider>(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0");
    });
}

// Service Constructor - Direct HttpClient injection
public SharedLinkProvider(HttpClient httpClient, ILogger<SharedLinkProvider> logger)
{
    this.httpClient = httpClient;  // ✅ Pre-configured by plugin!
    this.logger = logger;
}
```

**Why Typed Client?**
- ✅ Type-safe - Compiler checks dependencies
- ✅ Configured per service - Each plugin has own config
- ✅ Connection pooling - Automatic handler reuse
- ✅ Testable - Easy to mock HttpClient
- ✅ Microsoft recommended pattern

**❌ DON'T:**
```csharp
// DON'T: Manual HttpClient creation
using var client = new HttpClient();  // ❌ Socket exhaustion!

// DON'T: IHttpClientFactory in Typed Client
public MyService(IHttpClientFactory factory)  // ❌ Defeats purpose!
{
    _httpClient = factory.CreateClient();
}

// DON'T: Cache HttpClient in Singleton
[Singleton]
public class MyService
{
    private readonly HttpClient _client;  // ❌ Captive dependency!
}
```

**See:** `docs/httpclient-pattern.md` for complete guide

### 6. Logging Configuration

**Logging uses defaults in code with optional user override:**

#### **LoggingConfig with Defaults**
```csharp
// src/Core/Configuration/LoggingConfig.cs
public sealed class LoggingConfig
{
    public const string SectionName = "Logging";
    
    // ✅ Defaults in C# Properties (Warning to keep console clean)
    public Dictionary<string, string> LogLevel { get; init; } = new()
    {
        ["Default"] = "Warning",
        ["Spectara.Revela"] = "Warning",
        ["Microsoft"] = "Warning",
        ["System"] = "Warning"
    };
}
```

#### **Optional logging.json (Working Directory)**
```json
// D:\MyPhotos\logging.json (optional - for debugging)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Spectara.Revela": "Debug"
    }
  }
}
```

#### **Program.cs loads config**
```csharp
// AddRevelaConfiguration() handles all config loading:
// 1. revela.json (global - from %APPDATA%/Revela/)
// 2. project.json (local - project-specific settings)
// 3. logging.json (local - optional logging overrides)
// Note: site.json is NOT loaded via IConfiguration — loaded by RenderService

// Apply config OR defaults
var loggingConfig = new LoggingConfig();
builder.Configuration.GetSection(LoggingConfig.SectionName).Bind(loggingConfig);

// Configure logging
foreach (var (category, level) in loggingConfig.LogLevel)
{
    if (Enum.TryParse<LogLevel>(level, ignoreCase: true, out var logLevel))
    {
        if (category == "Default")
        {
            builder.Logging.SetMinimumLevel(logLevel);
        }
        else
        {
            builder.Logging.AddFilter(category, logLevel);
        }
    }
}
```

**Configuration Sources (in priority order):**
1. C# Defaults (LoggingConfig.LogLevel property)
2. `revela.json` (global, from `%APPDATA%/Revela/`)
3. `project.json` (local, project directory)
4. `logging.json` (optional, project directory)
5. Environment variables (`REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug`)

**Benefits:**
- ✅ No appsettings.json needed (defaults in code)
- ✅ Optional user override (logging.json)
- ✅ Environment variables support
- ✅ Perfect for Global Tools (ContentRoot = Working Directory)

### 7. Plugin Configuration System

**Plugin config is stored in `project.json` under the plugin's package ID as section name.**
**Environment variables can override settings with the `SPECTARA__REVELA__` prefix.**

#### **Step 1: Create Config Model**
```csharp
// src/Plugins/Plugin.{Name}/Configuration/{PluginName}Config.cs
using System.ComponentModel.DataAnnotations;

namespace Spectara.Revela.Plugin.{Name}.Configuration;

public sealed class MyPluginConfig
{
    /// <summary>
    /// Configuration section name - uses full package ID directly
    /// </summary>
    /// <remarks>
    /// Format: {FullPackageId} (no Plugins: prefix)
    /// This allows direct mapping from JSON root key and ENV variables.
    /// </remarks>
    public const string SectionName = "Spectara.Revela.Plugin.MyPlugin";
    
    [Required(ErrorMessage = "ApiUrl is required")]
    [Url(ErrorMessage = "Must be a valid URL")]
    public string ApiUrl { get; init; } = string.Empty;
    
    public int Timeout { get; init; } = 30;
}
```

#### **Step 2: Plugin Registers Config Sources (usually empty)**
```csharp
public sealed class MyPlugin : IPlugin
{
    // ConfigureConfiguration - usually empty!
    // Framework handles all configuration loading:
    // - project.json: plugin sections loaded automatically
    // - ENV vars: auto-loaded with SPECTARA__REVELA__ prefix
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Nothing to do - framework handles everything
    }
    
    // Register IOptions
    public void ConfigureServices(IServiceCollection services)
    {
        // Bind config from all sources (framework-loaded JSON + ENV vars)
        services.AddOptions<MyPluginConfig>()
            .BindConfiguration(MyPluginConfig.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();  // Fail-fast at startup
        
        // Register other services...
    }
}
```

**Plugin config section in `project.json`:**
```json
{
  "Spectara.Revela.Plugin.MyPlugin": {
    "ApiUrl": "https://api.example.com",
    "Timeout": 60
  }
}
```

#### **Step 3: Commands Use IOptions**
```csharp
public sealed partial class MyCommand(
    ILogger<MyCommand> logger,
    IOptionsMonitor<MyPluginConfig> config)  // Injected config!
{
    private async Task ExecuteAsync(string? urlOverride)
    {
        // Get current config (hot-reload support)
        var current = config.CurrentValue;
        
        // CLI overrides config
        var url = urlOverride ?? current.ApiUrl;
        
        // Use config values...
    }
}
```

**Configuration Hierarchy (merged in order):**
1. C# Property Defaults (in Config class)
2. `project.json` section (e.g., `"Spectara.Revela.Plugin.MyPlugin": { ... }`)
3. Environment variables: `SPECTARA__REVELA__PLUGIN__MYPLUGIN__*`
4. CLI arguments (override in command)

**Benefits:**
- ✅ All config in one file (project.json)
- ✅ Hot-reload support (`IOptionsMonitor<T>`)
- ✅ Multi-source config (JSON, ENV, CLI)
- ✅ Data Annotations validation
- ✅ Fail-fast at startup

### 8. Progress Display (Spectre.Console)

**Two-Phase Progress Pattern (Scan + Download):**

```csharp
// Phase 1: Scan with Status Spinner
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("[yellow]Scanning...[/]", async ctx =>
    {
        var items = await ScanAsync();
        ctx.Status($"[green]✓[/] Found {items.Count} items");
        await Task.Delay(500); // Brief pause to show result
    });

// Phase 2: Download with Progress Bar
await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn(),
        new SpinnerColumn()
    )
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[green]Downloading[/]");
        task.IsIndeterminate = true; // Until total known
        
        var progress = new Progress<(int current, int total, string name)>(report =>
        {
            if (task.IsIndeterminate && report.total > 0)
            {
                task.IsIndeterminate = false;
                task.MaxValue = report.total;
            }
            task.Value = report.current;
            
            // Escape Spectre markup in user data!
            var safeName = report.name
                .Replace("[", "[[", StringComparison.Ordinal)
                .Replace("]", "]]", StringComparison.Ordinal);
            
            task.Description = $"[green]Downloading[/] ({report.current}/{report.total}) {safeName}";
        });
        
        await DownloadAsync(progress);
    });
```

**Why Two Phases?**
- Phase 1 (Scan) - Total unknown, use spinner
- Phase 2 (Download) - Total known, show progress bar
- Brief pause after scan to show result before starting download

### 9. Token/Auth Caching

**Simple caching pattern (single-threaded command execution):**

```csharp
private string? cachedToken;
private DateTime tokenExpiry = DateTime.MinValue;

private async Task<string> GetTokenAsync(CancellationToken ct)
{
    // Return cached if valid
    if (cachedToken != null && DateTime.UtcNow < tokenExpiry)
    {
        LogUsingCachedToken(logger);
        return cachedToken;
    }
    
    // Fetch new token
    LogRequestingToken(logger);
    var token = await FetchNewTokenAsync(ct);
    
    cachedToken = token;
    tokenExpiry = DateTime.UtcNow.AddDays(6);  // Token valid for 7 days
    
    return cachedToken;
}
```

**Important:** No lock needed if:
- Token fetched **once** per command execution
- Passed as parameter through recursive calls
- Downloads use pre-authenticated URLs (no token needed)

**Example Flow:**
```csharp
// 1. Scan phase - get token once
var token = await GetTokenAsync(cancellationToken);
await ScanAsync(shareUrl, token, cancellationToken);  // Pass as param

// 2. Download phase - no token needed
await DownloadAsync(item.DownloadUrl);  // Pre-authenticated URL
```

### 10. Path Resolution (IPathResolver)

**IMPORTANT:** Never hardcode "source" or "output" paths! Use `IPathResolver`.

#### Why IPathResolver?
Users can configure custom paths in `project.json`:
```json
{
  "paths": {
    "source": "D:\\OneDrive\\Photos",
    "output": "/var/www/html"
  }
}
```

#### Usage Pattern
```csharp
// ✅ CORRECT - Inject IPathResolver
public sealed class MyService(IPathResolver pathResolver)
{
    public void Process()
    {
        var sourcePath = pathResolver.SourcePath;  // Resolves to configured path
        var outputPath = pathResolver.OutputPath;  // Supports relative & absolute
    }
}

// ❌ WRONG - Hardcoded paths
var sourcePath = Path.Combine(projectPath, "source");  // Ignores user config!
```

#### Path Resolution Logic
- **Relative paths:** Resolved against project root (e.g., "source" → "D:\MyProject\source")
- **Absolute paths:** Used directly (e.g., "D:\OneDrive\Photos" → "D:\OneDrive\Photos")
- **Hot-reload:** Uses `IOptionsMonitor<PathsConfig>` - changes apply without restart

#### ProjectPaths Constants
`ProjectPaths` contains ONLY non-configurable paths:
- `ProjectPaths.Cache` → ".cache"
- `ProjectPaths.Themes` → "themes"  
- `ProjectPaths.Plugins` → "plugins"
- `ProjectPaths.SharedImages` → "_images"
- `ProjectPaths.Static` → "_static"

**❌ `ProjectPaths.Source` and `ProjectPaths.Output` were REMOVED!**
Use `IPathResolver.SourcePath` and `IPathResolver.OutputPath` instead.

---

## Common Tasks

### Adding a New Model
Location: `src/Core/Models/` or `src/Sdk/Models/`

```csharp
namespace Spectara.Revela.Core.Models;

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
Location: `src/Commands/{FeatureName}/` or `src/Plugins/Plugin.*/Commands/`

**MODERN PATTERN (with DI):**
```csharp
namespace Spectara.Revela.Commands.MyFeature;

/// <summary>
/// Command implementation with Dependency Injection
/// </summary>
/// <remarks>
/// Uses C# 12 Primary Constructor for DI.
/// Dependencies are explicitly visible and fully testable.
/// </remarks>
public sealed partial class MyCommand(
    ILogger<MyCommand> logger,
    IMyService myService)
{
    public Command Create()
    {
        var command = new Command("mycommand", "Description");
        
        var option = new Option<string>("--name", "-n")
        {
            Description = "Name option"
        };
        command.Options.Add(option);
        
        command.SetAction(async parseResult =>
        {
            var name = parseResult.GetValue(option);
            await ExecuteAsync(name);
            return 0;
        });
        
        return command;
    }
    
    private async Task ExecuteAsync(string? name)
    {
        LogExecuting(logger, name ?? "default");
        await myService.ProcessAsync(name);
    }
    
    [LoggerMessage(Level = LogLevel.Information, Message = "Executing with name: {Name}")]
    private static partial void LogExecuting(ILogger logger, string name);
}
```

**Registration in Plugin/Feature:**
```csharp
// In ConfigureServices:
services.AddTransient<MyCommand>();

// In GetCommands:
var cmd = serviceProvider.GetRequiredService<MyCommand>();
yield return cmd.Create();
```

**Benefits:**
- ✅ Explicit dependencies (visible in constructor)
- ✅ Fully testable (mock dependencies)
- ✅ No IServiceProvider in methods
- ✅ Type-safe with Primary Constructor

### Adding a New Service
Location: `src/Core/Services/`

```csharp
namespace Spectara.Revela.Core.Services;

public interface IMyService
{
    Task DoSomethingAsync(int count, CancellationToken cancellationToken = default);
}

public sealed partial class MyService : IMyService
{
    private readonly ILogger<MyService> logger;
    
    public MyService(ILogger<MyService> logger)
    {
        this.logger = logger;
    }
    
    public async Task DoSomethingAsync(int count, CancellationToken cancellationToken = default)
    {
        LogProcessingStarted(logger, count);
        await Task.CompletedTask;
    }
    
    // High-performance logging (LoggerMessage source generator)
    [LoggerMessage(Level = LogLevel.Information, Message = "Processing {Count} items")]
    private static partial void LogProcessingStarted(ILogger logger, int count);
}
```

### Adding Tests
Location: `tests/{ProjectName}.Tests/`

```csharp
namespace Spectara.Revela.Core.Tests;

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
        
        // Assert (using MSTest built-in assertions)
        Assert.AreEqual(expected, result);
    }
}
```

### Testing Internal Classes

**Use `InternalsVisibleTo` for testing internal classes:**

```csharp
// src/Plugins/Plugin.Source.OneDrive/AssemblyInfo.cs
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spectara.Revela.Plugin.Source.OneDrive.Tests")]
```

**Important:** Use the full assembly name (with `Spectara.Revela.` prefix)!

### MSTest v4 Assertion Patterns

**Use modern MSTest v4 assertions instead of classic patterns:**

```csharp
// ❌ DON'T - Classic patterns (trigger MSTEST0037 warning)
Assert.AreEqual(0, list.Count);
Assert.AreEqual(3, list.Count);
Assert.IsTrue(list.Count > 0);
Assert.IsTrue(text.Contains("foo"));
Assert.IsFalse(text.Contains("bar"));

// ✅ DO - MSTest v4 assertions (clearer intent, better error messages)
Assert.IsEmpty(list);
Assert.HasCount(3, list);
Assert.IsNotEmpty(list);
Assert.Contains("foo", text);
Assert.DoesNotContain("bar", text);
```

**Available MSTest v4 Collection Assertions:**
- `Assert.IsEmpty(collection)` - Collection has no elements
- `Assert.IsNotEmpty(collection)` - Collection has at least one element
- `Assert.HasCount(expected, collection)` - Collection has exact count
- `Assert.Contains(expected, collection)` - Collection contains element
- `Assert.DoesNotContain(expected, collection)` - Collection doesn't contain element

**String Assertions:**
- `Assert.Contains(substring, text)` - String contains substring
- `Assert.DoesNotContain(substring, text)` - String doesn't contain substring

### HTTP Mocking Pattern

**For testing services with HttpClient:**

```csharp
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<Uri, HttpResponseMessage> responses = [];

    public void AddResponse(Uri uri, HttpResponseMessage response) =>
        responses[uri] = response;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (responses.TryGetValue(request.RequestUri!, out var response))
            return Task.FromResult(response);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

// Usage in test:
var handler = new MockHttpMessageHandler();
handler.AddResponse(new Uri("https://api.example.com/data"), 
    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

var httpClient = new HttpClient(handler);
var service = new MyService(httpClient, logger);
```

### Culture-Independent Formatting

**Always use `CultureInfo.InvariantCulture` for consistent output:**

```csharp
// ❌ DON'T - Culture-dependent (1,5 vs 1.5)
return $"{value:0.##} MB";

// ✅ DO - Culture-independent
return string.Format(CultureInfo.InvariantCulture, "{0:0.##} MB", value);
```

### Console Output Standards

**Use `OutputMarkers` from `Spectara.Revela.Sdk.Output` for consistent console feedback:**

```csharp
using Spectara.Revela.Sdk.Output;

// Success: completed actions, successful operations
AnsiConsole.MarkupLine($"{OutputMarkers.Success} Operation completed");
// Output: ✓ Operation completed (green)

// Error: failed operations, validation errors
AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to process file");
// Output: ✗ Failed to process file (red)

// Warning: skipped items, potential issues, non-critical problems
AnsiConsole.MarkupLine($"{OutputMarkers.Warning} File already exists");
// Output: ⚠ File already exists (yellow)

// Info: informational messages, hints, neutral status updates
AnsiConsole.MarkupLine($"{OutputMarkers.Info} Processing 5 items...");
// Output: ℹ Processing 5 items... (blue)
```

**Available Constants:**
| Constant | Symbol | Color | Use For |
|----------|--------|-------|---------|
| `OutputMarkers.Success` | ✓ | Green | Completed actions, passed validation |
| `OutputMarkers.Error` | ✗ | Red | Failed operations, errors |
| `OutputMarkers.Warning` | ⚠ | Yellow | Skipped items, potential issues |
| `OutputMarkers.Info` | ℹ | Blue | Informational messages, hints |

**❌ DON'T use raw markup strings:**
```csharp
// ❌ DON'T - Inconsistent symbols
AnsiConsole.MarkupLine("[green]OK[/] Done");
AnsiConsole.MarkupLine("[red]x[/] Failed");
AnsiConsole.MarkupLine("[yellow]![/] Warning");

// ✅ DO - Use OutputMarkers
AnsiConsole.MarkupLine($"{OutputMarkers.Success} Done");
AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed");
AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Warning");
```

### Registering Services
Location: `src/Commands/{FeatureName}/Extensions/` or `src/Core/Extensions/`

```csharp
namespace Spectara.Revela.Commands.MyFeature;

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
- `Microsoft.Extensions.Hosting` (10.0.3)
- `Microsoft.Extensions.Configuration.Json` (10.0.3)
- `Microsoft.Extensions.Logging` (10.0.3)

### CLI
- `System.CommandLine` (2.0.3) - **FINAL release, not beta!**
- `Spectre.Console` (0.54.0) - Rich console output (progress bars, tables, panels)

### Image Processing
- `NetVips` (3.2.0)
- `NetVips.Native` (8.18.0)

### Templating
- `Scriban` (6.5.2)
- `Markdig` (0.45.0)

### Logging
- `Microsoft.Extensions.Logging` (10.0.3) - Built-in logging
- `Microsoft.Extensions.Logging.Console` (10.0.3)
- `Microsoft.Extensions.Logging.Debug` (10.0.3)

### Plugin Management
- `NuGet.Protocol` (7.3.0)
- `NuGet.Packaging` (7.3.0)
- `NuGet.Configuration` (7.3.0)

### Deployment
- `SSH.NET` (2025.1.0) - SSH/SFTP deployment

### Build Tools
- `Microsoft.SourceLink.GitHub` (10.0.102) - Source link for debugging

### Testing
- `MSTest` (4.1.0) - Modern test framework with Microsoft.Testing.Platform
- `MSTest.Analyzers` (4.1.0)
- `NSubstitute` (5.3.0) - Mocking framework (preferred over Moq due to security concerns)
- `coverlet.collector` (6.0.4) - Code coverage

### Benchmarking
- `BenchmarkDotNet` (0.15.8) - Performance benchmarks

**Note:** FluentAssertions was removed - use MSTest v4 built-in assertions instead!

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
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Core.Tests
dotnet test tests/Commands.Tests
dotnet test tests/IntegrationTests
dotnet test tests/Plugin.Compress.Tests
dotnet test tests/Plugin.Serve.Tests
dotnet test tests/Plugin.Source.OneDrive.Tests
dotnet test tests/Plugin.Statistics.Tests
```

### Run CLI
```bash
dotnet run --project src/Cli -- --help
cd samples/filter-demo && dotnet run --project ../../src/Cli -- generate all
cd samples/filter-demo && dotnet run --project ../../src/Cli -- source onedrive download
```

### Package as Tool
```bash
dotnet pack src/Cli -c Release
dotnet tool install -g --add-source ./artifacts/packages Revela
```

### Code Formatting (IMPORTANT!)

**Run `dotnet format` before committing to ensure consistent code style!**

```bash
# Check for formatting issues (CI/pre-commit)
dotnet format --verify-no-changes

# Auto-fix all formatting issues
dotnet format
```

**Common issues fixed by `dotnet format`:**
- **IMPORTS:** Using directives not sorted alphabetically
- **IDE0001:** Name can be simplified (redundant namespace qualifiers)
- **IDE0022:** Use expression body for method
- **IDE0053:** Use expression body for lambda expression
- **IDE0055:** Formatting issues (indentation, spacing)

**When to run:**
- ✅ Before every commit
- ✅ After large refactoring
- ✅ When CI fails with format errors
- ✅ At session start to check codebase health

**Pragma Guidelines:**
- ❌ Don't suppress warnings for unused code - remove the code instead
- ❌ Don't suppress IDE0005 (unused usings) - remove the usings
- ✅ Legitimate suppressions: CA1054 (URI strings for user input), CA2000 (complex ownership)
- ✅ Always include `#pragma warning restore` after `#pragma warning disable`

---

## Context for AI Assistants

### When Starting New Conversation
1. Read `DEVELOPMENT.md` for current status
2. Read `docs/architecture.md` for design decisions
3. Check open files in IDE for current work
4. **CHECK FOR DEPENDENCY UPDATES** - Run `dotnet outdated` proactively
5. **CHECK CODE FORMATTING** - Run `dotnet format --verify-no-changes`

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
  
  namespace Spectara.Revela.Commands.Init;
  
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

**Last Updated:** 2026-02-12 (Session: Comprehensive copilot-instructions review)

**Key Learnings from Latest Sessions:**
- ✅ Plugin ConfigureServices pattern (4-phase lifecycle)
- ✅ Typed HttpClient for plugins
- ✅ Parent Command declaration in CommandDescriptor (not metadata)
- ✅ ConfigurationBuilder + Data Annotations validation
- ✅ Two-phase progress display (Scan + Download)
- ✅ Parallel.ForEachAsync for I/O-bound operations
- ✅ LoggingConfig with defaults (no appsettings.json needed)
- ✅ InternalsVisibleTo for testing internal classes
- ✅ CultureInfo.InvariantCulture for consistent formatting
- ✅ MockHttpMessageHandler for HTTP testing
- ✅ MSTest v4 built-in assertions (HasCount, IsEmpty, Contains, etc.)
- ✅ FluentAssertions removed - use MSTest v4 assertions only
- ✅ **Template Context:** `image_formats` is global, `image.sizes` is per-image
- ✅ **Manifest optimization:** Formats removed from ImageContent (redundant)
- ✅ **Template simplification:** No local variables needed, direct property access
- ✅ **Parallel image processing:** 5× speedup with Parallel.ForEachAsync
- ✅ **LibVips thread-safety:** Safe for independent images, no global lock needed
- ✅ **NetVips Cache.Max = 0:** Disable cache for batch processing (saves memory)
- ✅ **Format-specific quality:** AVIF:80, WebP:85, JPG:90 (22% smaller files)
- ✅ **AVIF support:** AV1 compression via Heifsave with ForeignHeifCompression.Av1
- ✅ **Interactive CLI:** Menu-driven interface when running without arguments
- ✅ **`generate all` command:** Explicit pipeline execution (scan → statistics → pages → images)
- ✅ **`clean` subcommands:** all, output, cache, statistics (no more flags)
- ✅ **CommandOrderRegistry:** Controls menu order in interactive mode
- ✅ **Debug builds:** Plugins as project references, no DLL copying needed
- ✅ **IPathResolver:** Dynamic path resolution for source/output directories
- ✅ **PathsConfig:** Configurable paths via project.json `paths` section
- ✅ **ProjectPaths cleanup:** Source/Output constants removed - use IPathResolver
- ✅ **TTY detection:** Graceful fallback when interactive mode lacks terminal
- ✅ **LQIP Placeholders:** CSS-only low-quality image placeholders via CssHash
- ✅ **Change Detection:** Manifest tracks LastModified + FileSize + SHA256 hash
- ✅ **FormatQualities:** Quality settings tracked in manifest, auto-regenerate on change
- ✅ **Setup Wizard:** RevelaWizard (first-time) + ProjectWizard (project-level)
- ✅ **ConfigPathResolver:** Portable vs. AppData config resolution
- ✅ **ProjectResolver:** Standalone mode project detection before host.Build()
- ✅ **ProjectEnvironment:** Runtime project info (Path, IsInitialized)
- ✅ **site.json:** NOT loaded via IConfiguration — loaded dynamically by RenderService
- ✅ **Plugin.Compress:** Gzip/Brotli pre-compression (NOT part of generate all pipeline)

**Template Context Variables:**
- `site` - Site settings (title, author, description, copyright)
- `basepath` - Relative path to root ("", "../", "/photos/")
- `image_basepath` - Path/URL to images (can be CDN URL)
- `image_formats` - Global: ["avif", "webp", "jpg"] (same for all images)
- `nav_items` - Navigation tree with active state
- `gallery` - Current gallery (title, body)
- `images` - Array of Image objects
- `image.sizes` - Per-image: available widths (filtered by original)
- `image.placeholder` - Per-image: CSS-only LQIP hash string (if PlaceholderStrategy = CssHash)

**Image Configuration (project.json):**
```json
{
  "paths": {
    "source": "D:\\OneDrive\\Photos",
    "output": "dist"
  },
  "theme": {
    "images": {
      "sizes": [160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560]
    }
  },
  "generate": {
    "images": {
      "avif": 80,
      "webp": 85,
      "jpg": 90
    }
  }
}
```

**For detailed architecture, see:** `docs/architecture.md`  
**For development status, see:** `DEVELOPMENT.md`  
**For dependency management, see:** `.github/DEPENDENCY_MANAGEMENT.md`  
**For HttpClient patterns, see:** `docs/httpclient-pattern.md`
