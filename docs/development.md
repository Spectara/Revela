# Development Guide

Guide for contributors who want to build, test, and develop Revela.

## Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** or later
- **Git**
- **IDE** (recommended):
  - [Visual Studio 2022](https://visualstudio.microsoft.com/) (Windows)
  - [VS Code](https://code.visualstudio.com/) with C# Dev Kit
  - [JetBrains Rider](https://www.jetbrains.com/rider/) (cross-platform)

## Getting Started

### Clone Repository

```bash
git clone https://github.com/spectara/revela.git
cd revela
```

### Restore Packages

```bash
dotnet restore
```

This downloads all NuGet packages (managed centrally via `Directory.Packages.props`).

### Build

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test project
dotnet test tests/Core.Tests
```

### Run CLI Locally

```bash
# Show help
dotnet run --project src/Cli -- --help

# Interactive mode
dotnet run --project src/Cli

# Generate a sample site
dotnet run --project src/Cli -- generate all -p ./samples/subdirectory
```

---

## Project Structure

```
Revela/
├── src/                          # Source code
│   ├── Core/                     # Shared kernel (models, services, plugin system)
│   ├── Commands/                 # CLI commands (Generate, Clean, Config, etc.)
│   ├── Cli/                      # CLI entry point
│   ├── Sdk/                      # SDK for plugin development
│   ├── Plugins/                  # Official plugins
│   │   ├── Plugin.Serve/         # Local development server
│   │   ├── Plugin.Source.OneDrive/
│   │   └── Plugin.Statistics/
│   └── Themes/                   # Built-in themes
│       ├── Theme.Lumina/
│       └── Theme.Lumina.Statistics/
│
├── tests/                        # Test projects
│   ├── Core.Tests/
│   ├── Commands.Tests/
│   ├── IntegrationTests/
│   └── Shared/                   # Shared test utilities
│
├── docs/                         # Documentation
├── samples/                      # Example projects
├── scripts/                      # Build scripts
│
├── Directory.Build.props         # Central build configuration
├── Directory.Packages.props      # Central package management
├── global.json                   # .NET SDK version pinning
├── .editorconfig                 # Code style rules
└── Spectara.Revela.slnx          # Solution file
```

---

## Code Conventions

### Naming

| Element | Convention | Example |
|---------|------------|---------|
| Private fields | camelCase (no underscore) | `logger`, `configService` |
| Public members | PascalCase | `ProjectName`, `GenerateAsync` |
| Async methods | Async suffix | `LoadAsync`, `SaveAsync` |
| Interfaces | I prefix | `IPluginLoader`, `IConfigService` |
| Constants | PascalCase | `DefaultTimeout`, `MaxRetries` |

### Code Style

- **Namespaces:** File-scoped (`namespace Spectara.Revela.Core;`)
- **Nullable:** Enabled globally
- **Primary constructors:** Preferred for DI
- **Collection expressions:** Use `[]` instead of `new List<>()`

### Example

```csharp
namespace Spectara.Revela.Core.Services;

public sealed partial class MyService(
    ILogger<MyService> logger,
    IOptions<MyConfig> options) : IMyService
{
    public async Task DoWorkAsync(CancellationToken cancellationToken = default)
    {
        LogStarting(logger);
        
        var config = options.Value;
        // ... implementation
        
        await Task.CompletedTask;
    }
    
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting work")]
    private static partial void LogStarting(ILogger logger);
}
```

### Code Formatting

**Always run before committing:**

```bash
# Check for issues
dotnet format --verify-no-changes

# Auto-fix issues
dotnet format
```

---

## Testing

### Framework

- **MSTest v4** - Test framework
- **NSubstitute** - Mocking
- **Built-in assertions** - No FluentAssertions

### Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Core.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Writing Tests

```csharp
[TestClass]
public sealed class MyServiceTests
{
    [TestMethod]
    public async Task DoWorkAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var logger = Substitute.For<ILogger<MyService>>();
        var options = Options.Create(new MyConfig { Value = "test" });
        var service = new MyService(logger, options);
        
        // Act
        var result = await service.DoWorkAsync();
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("expected", result.Value);
    }
}
```

### Test Patterns

```csharp
// Collection assertions (MSTest v4)
Assert.IsEmpty(list);
Assert.HasCount(3, list);
Assert.Contains("item", collection);

// String assertions
Assert.Contains("substring", text);
Assert.DoesNotContain("bad", text);
```

---

## Dependency Management

### Check for Updates

```bash
# Install tool (once)
dotnet tool install --global dotnet-outdated-tool

# Check for updates
dotnet outdated

# Interactive update
dotnet outdated -u:prompt
```

### Update Strategy

| Update Type | Action |
|-------------|--------|
| Patch (x.x.**X**) | Safe, update immediately |
| Minor (x.**X**.x) | Usually safe, test first |
| Major (**X**.x.x) | Review changelog, may have breaking changes |

### Central Package Management

All package versions are defined in `Directory.Packages.props`:

```xml
<ItemGroup>
  <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.1" />
  <PackageVersion Include="Scriban" Version="6.5.2" />
</ItemGroup>
```

---

## IDE Setup

### Visual Studio 2022

1. Open `Spectara.Revela.slnx`
2. Set `Cli` as startup project
3. Configure arguments:
   - Right-click `Cli` → Properties → Debug → Command line arguments
   - Example: `generate all -p ../samples/subdirectory`

### VS Code

1. Open folder in VS Code
2. Install **C# Dev Kit** extension
3. Use the task runner (Ctrl+Shift+B) for build

### Rider

1. Open `Spectara.Revela.slnx`
2. Set `Cli` as startup project
3. Edit run configuration for arguments

---

## Building Releases

### Create NuGet Package

```bash
dotnet pack src/Cli -c Release
# Output: artifacts/packages/Spectara.Revela.*.nupkg
```

### Create Standalone Executable

```bash
# Windows
dotnet publish src/Cli -c Release -r win-x64 --self-contained

# Linux
dotnet publish src/Cli -c Release -r linux-x64 --self-contained

# macOS
dotnet publish src/Cli -c Release -r osx-arm64 --self-contained
```

### Test Release Build

```bash
./scripts/test-release.ps1
```

---

## Plugin Development

See [Plugin Development Guide](plugin-development.md) for creating custom plugins.

Quick start:

```bash
# Create plugin from template
dotnet new classlib -n MyPlugin
cd MyPlugin
dotnet add package Spectara.Revela.Sdk
```

---

## Useful Commands

```bash
# Clean everything
dotnet clean

# Restore packages
dotnet restore

# Build all
dotnet build

# Run tests
dotnet test

# Format code
dotnet format

# Check outdated packages
dotnet outdated

# Run CLI in development
dotnet run --project src/Cli -- [arguments]
```

---

## Getting Help

- **GitHub Issues:** [Report bugs or request features](https://github.com/spectara/revela/issues)
- **Discussions:** [Ask questions](https://github.com/spectara/revela/discussions)
- **Architecture:** [Architecture Overview](architecture.md)
