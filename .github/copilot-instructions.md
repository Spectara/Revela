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
├── Core/                     # Shared kernel (models, abstractions, plugin system)
├── Commands/                 # CLI commands (Generate, Init, Plugins, Restore, Theme)
├── Cli/                      # Entry point (.NET Tool)
├── Plugins/
│   ├── Plugin.Deploy.SSH/    # SSH/SFTP deployment (planned)
│   ├── Plugin.Source.OneDrive/  # OneDrive shared folder source
│   └── Plugin.Statistics/    # Statistics functionality
└── Themes/
    ├── Theme.Lumina/         # Default photography portfolio theme
    └── Theme.Lumina.Statistics/  # Statistics extension for Lumina theme
tests/
├── Core.Tests/               # Unit tests for Core
├── Commands.Tests/           # Unit tests for Commands
├── IntegrationTests/         # Integration tests
├── Plugin.Source.OneDrive.Tests/  # OneDrive plugin tests
├── Plugin.Statistics.Tests/  # Statistics plugin tests
└── Shared/                   # Shared test utilities
```

### Key Files
- **Models:** `src/Core/Models/` - Domain entities
- **Config:** `src/Core/Configuration/` - Configuration models
- **Plugins:** `src/Core/PluginLoader.cs` + `PluginManager.cs`

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
  builder.Services.AddPlugins(builder.Configuration);
  ```

**Reason:** All commands resolved from DI, unified registration via extension method

### 4. Plugin System with Host.CreateApplicationBuilder

**MODERN PATTERN:** Use `Host.CreateApplicationBuilder` for full .NET hosting features!

#### Complete Program.cs Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli;
using Spectara.Revela.Commands;

// ✅ Use Host.CreateApplicationBuilder
var builder = Host.CreateApplicationBuilder(args);

// ✅ Pre-build: Load configuration and register services
builder.AddRevelaConfiguration();
builder.Services.AddRevelaCommands();
builder.Services.AddPlugins(builder.Configuration);

// ✅ Build host
var host = builder.Build();

// ✅ Post-build: Create CLI and execute
return host.UseRevelaCommands().Parse(args).Invoke();
```

#### Plugin Lifecycle - 4 Phases

**IMPORTANT:** Plugins have a specific initialization order!

```csharp
public interface IPlugin
{
    IPluginMetadata Metadata { get; }
    
    // 1. ConfigureConfiguration - Called BEFORE BuildServiceProvider
    //    Usually empty - framework handles JSON + ENV loading
    //    NOTE: JSON files auto-loaded from plugins/*.json
    //    NOTE: ENV vars auto-loaded with SPECTARA__REVELA__ prefix
    void ConfigureConfiguration(IConfigurationBuilder configuration);
    
    // 2. ConfigureServices - Called BEFORE BuildServiceProvider
    //    Plugin registers services (HttpClient, Commands, IOptions)
    void ConfigureServices(IServiceCollection services);
    
    // 3. Initialize - Called AFTER host.Build()
    //    Plugin receives built ServiceProvider for initialization
    void Initialize(IServiceProvider services);
    
    // 4. GetCommands - Returns CLI commands for registration
    IEnumerable<Command> GetCommands();
}
```

#### AddPlugins() Extension Method

**Automatic Plugin Lifecycle Management:**

```csharp
// Extension method handles all plugin lifecycle phases
public static IPluginContext AddPlugins(
    this IServiceCollection services,
    IConfigurationBuilder configuration,
    Action<PluginOptions>? configure = null)
{
    // 1. Load plugin assemblies
    var pluginLoader = new PluginLoader(logger);
    pluginLoader.LoadPlugins();
    var plugins = pluginLoader.GetLoadedPlugins();
    
    // 2. Auto-load all plugins/*.json config files
    // JSON structure: { "Spectara.Revela.Plugin.X": { ... } }
    var pluginsDir = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
    if (Directory.Exists(pluginsDir))
    {
        foreach (var jsonFile in Directory.GetFiles(pluginsDir, "*.json"))
        {
            configuration.AddJsonFile(jsonFile, optional: true, reloadOnChange: true);
        }
    }
    
    // 3. Auto-load environment variables with global prefix
    // Allows: SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL=https://...
    configuration.AddEnvironmentVariables(prefix: "SPECTARA__REVELA__");
    
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
- ✅ **Configuration:** plugins/*.json, environment variables (SPECTARA__REVELA__*)
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
    Name = "OneDrive Source",
    Version = "1.0.0",
    Description = "OneDrive shared folder source",
    Author = "Spectara"
};
```

**Plugin returns CommandDescriptor with optional parent:**
```csharp
public IEnumerable<CommandDescriptor> GetCommands()
{
    // ✅ ParentCommand specified here, not in metadata!
    // "source" = registered under source (revela source onedrive)
    yield return new CommandDescriptor(
        new Command("onedrive", "OneDrive plugin"),
        ParentCommand: "source");
    
    // null = registered at root level (revela mycommand)
    yield return new CommandDescriptor(
        new Command("sync", "Sync command"),
        ParentCommand: null);
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
// Load optional logging.json from working directory
builder.Configuration
    .AddJsonFile("logging.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "REVELA__")
    .AddCommandLine(args);

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
2. `logging.json` (optional, working directory)
3. Environment variables (`REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug`)

**Benefits:**
- ✅ No appsettings.json needed (defaults in code)
- ✅ Optional user override (logging.json)
- ✅ Environment variables support
- ✅ Perfect for Global Tools (ContentRoot = Working Directory)

### 7. Plugin Configuration System

**Framework auto-loads all `plugins/*.json` files before plugin initialization.**
**Plugin filename convention:** Default = Package-ID (e.g., `Spectara.Revela.Plugin.Source.OneDrive.json`)

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
    // - JSON files: auto-loaded from plugins/*.json
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

**Plugin config file: `plugins/Spectara.Revela.Plugin.MyPlugin.json`**
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
2. `plugins/*.json` (auto-loaded by framework, filename = Package-ID)
3. Environment variables: `SPECTARA__REVELA__PLUGIN__MYPLUGIN__*`
4. CLI arguments (override in command)

**Config Filename Convention:**
- Default: `plugins/Spectara.Revela.Plugin.MyPlugin.json`
- Custom: `revela source myplugin init --name custom-name.json`

**Benefits:**
- ✅ No filename conflicts (Package-ID is unique)
- ✅ Framework auto-loads (plugins don't register JSON files)
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
Location: `src/Revela.Features/{FeatureName}/` or `src/Plugins/Plugin.*/Commands/`

**MODERN PATTERN (with DI):**
```csharp
namespace Revela.Features.MyFeature;

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
- `Microsoft.Extensions.Hosting` (10.0.1)
- `Microsoft.Extensions.Configuration.Json` (10.0.1)
- `Microsoft.Extensions.Logging` (10.0.1)

### CLI
- `System.CommandLine` (2.0.1) - **FINAL release, not beta!**
- `Spectre.Console` (0.54.0) - Rich console output (progress bars, tables, panels)

### Image Processing
- `NetVips` (3.1.0)
- `NetVips.Native` (8.17.3)

### Templating
- `Scriban` (6.5.2)
- `Markdig` (0.44.0)

### Logging
- `Microsoft.Extensions.Logging` (10.0.1) - Built-in logging
- `Microsoft.Extensions.Logging.Console` (10.0.1)
- `Microsoft.Extensions.Logging.Debug` (10.0.1)

### Plugin Management
- `NuGet.Protocol` (7.0.1)
- `NuGet.Packaging` (7.0.1)
- `NuGet.Configuration` (7.0.1)

### Testing
- `MSTest` (4.0.2) - Modern test framework with Microsoft.Testing.Platform
- `MSTest.Analyzers` (4.0.2)
- `NSubstitute` (5.3.0) - Mocking framework (preferred over Moq due to security concerns)
- `coverlet.collector` (6.0.4) - Code coverage

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
# MSTest v4 with Microsoft.Testing.Platform
# Run as executables (recommended for .NET 10)
dotnet run --project tests/Core.Tests
dotnet run --project tests/IntegrationTests
dotnet run --project tests/Plugin.Source.OneDrive.Tests
```

### Run CLI
```bash
dotnet run --project src/Cli -- --help
dotnet run --project src/Cli -- generate -p samples/minimal
dotnet run --project src/Cli -- source onedrive download
```

### Package as Tool
```bash
dotnet pack src/Cli -c Release
dotnet tool install -g --add-source ./artifacts/packages Revela
```

---

## Context for AI Assistants

### When Starting New Conversation
1. Read `DEVELOPMENT.md` for current status
2. Read `docs/architecture.md` for design decisions
3. Check open files in IDE for current work
4. **CHECK FOR DEPENDENCY UPDATES** - Run `dotnet outdated` proactively

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

**Last Updated:** 2025-12-20 (Session: Interactive CLI + Clean Refactoring)

**Key Learnings from Latest Sessions:**
- ✅ Plugin ConfigureServices pattern (3-phase lifecycle)
- ✅ Typed HttpClient for plugins
- ✅ Parent Command declaration in metadata
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

**Template Context Variables:**
- `site` - Site settings (title, author, description, copyright)
- `basepath` - Relative path to root ("", "../", "/photos/")
- `image_basepath` - Path/URL to images (can be CDN URL)
- `image_formats` - Global: ["avif", "webp", "jpg"] (same for all images)
- `nav_items` - Navigation tree with active state
- `gallery` - Current gallery (title, body)
- `images` - Array of Image objects
- `image.sizes` - Per-image: available widths (filtered by original)

**Image Configuration (project.json):**
```json
{
  "generate": {
    "images": {
      "formats": {
        "avif": 80,
        "webp": 85,
        "jpg": 90
      },
      "sizes": [640, 1024, 1280, 1920, 2560]
    }
  }
}
```

**For detailed architecture, see:** `docs/architecture.md`  
**For development status, see:** `DEVELOPMENT.md`  
**For dependency management, see:** `.github/DEPENDENCY_MANAGEMENT.md`  
**For HttpClient patterns, see:** `docs/httpclient-pattern.md`
