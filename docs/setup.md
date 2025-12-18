# Setup & Build Instructions

## Prerequisites

- **.NET 10 SDK** (or later)
- **Git**
- **Visual Studio 2022** / **VS Code** / **Rider** (optional, but recommended)

---

## Initial Setup

### 1. Clone Repository

```bash
git clone https://github.com/spectara/revela.git
cd revela
```

### 2. Restore Packages

```bash
dotnet restore
```

This will:
- Download all NuGet packages (managed centrally via `Directory.Packages.props`)
- Restore project dependencies
- Download NetVips native binaries

### 3. Build Solution

```bash
dotnet build
```

**Expected output:** Build should succeed (or fail with known warnings/errors - see DEVELOPMENT.md)

---

## Development Workflow

### Build & Test

```bash
# Clean build
dotnet clean
dotnet build

# Run tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal
```

### Run CLI Locally

```bash
# Show help
dotnet run --project src/Cli -- --help

# Generate site
dotnet run --project src/Cli -- generate -p ./samples/subdirectory

# Plugin commands
dotnet run --project src/Cli -- plugins list
```

### Code Formatting

```bash
# Format all files
dotnet format

# Check formatting without changes
dotnet format --verify-no-changes
```

---

## IDE Setup

### Visual Studio 2022

1. Open `Spectara.Revela.slnx`
2. Set `Cli` as startup project
3. Configure command-line arguments:
   - Right-click `Cli` → Properties → Debug
   - Add arguments: `generate -p samples/subdirectory`

### Visual Studio Code

1. Open folder in VS Code
2. Install extensions:
   - C# Dev Kit
   - .NET Install Tool
3. Use tasks (`.vscode/tasks.json` if created)

### Rider

1. Open `Spectara.Revela.slnx`
2. Set `Cli` as startup project
3. Edit run configuration to add arguments

---

## Project Structure

```
Revela/
├── src/                          # Source code
│   ├── Core/                     # Shared kernel (models, abstractions, plugin system)
│   ├── Commands/                 # CLI commands (Generate, Init, Plugins, Restore, Theme)
│   ├── Cli/                      # CLI entry point
│   ├── Plugins/                  # Optional plugins
│   │   ├── Plugin.Deploy.SSH/
│   │   └── Plugin.Source.OneDrive/
│   └── Themes/                   # Built-in themes
│       ├── Theme.Lumina/
│       └── Theme.Lumina.Statistics/
│
├── tests/                        # Test projects
│   ├── Core.Tests/
│   ├── Commands.Tests/
│   └── IntegrationTests/
│
├── docs/                         # Documentation
│   ├── architecture.md
│   └── setup.md (this file)
│
├── samples/                      # Example projects
│   ├── subdirectory/
│   ├── cdn/
│   └── onedrive/
│
├── artifacts/                    # Build output (gitignored)
│   ├── bin/
│   ├── obj/
│   └── packages/
│
├── Directory.Build.props         # Central build configuration
├── Directory.Packages.props      # Central package management
├── global.json                   # .NET SDK version
├── .editorconfig                 # Code style rules
├── DEVELOPMENT.md                # Development status & TODOs
└── Spectara.Revela.slnx          # Solution file
```

---

## Publishing

### Package as .NET Tool

```bash
# Pack
dotnet pack src/Cli -c Release

# Output: artifacts/packages/Spectara.Revela.1.0.0.nupkg
```

### Install Locally

```bash
# Install from local package
dotnet tool install -g --add-source ./artifacts/packages Spectara.Revela

# Or uninstall first
dotnet tool uninstall -g Spectara.Revela
dotnet tool install -g --add-source ./artifacts/packages Spectara.Revela
```

### Test Installed Tool

```bash
revela --help
revela generate -p path/to/site
```

### Publish to NuGet.org

```bash
# Requires NuGet API key
dotnet nuget push artifacts/packages/Spectara.Revela.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

---

## Troubleshooting

### Build Fails with "Package not found"

**Solution:** Run `dotnet restore` again

```bash
dotnet clean
dotnet restore
dotnet build
```

### NetVips Not Found

**Solution:** Ensure `NetVips.Native` is installed

```bash
dotnet list package | findstr NetVips
```

Should show:
- `NetVips` (3.1.0)
- `NetVips.Native` (8.17.3)

### Code Style Errors (CA1848, IDE0055)

**Solution:** Either fix the warnings or temporarily disable

```xml
<!-- In Directory.Build.props -->
<PropertyGroup>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
</PropertyGroup>
```

### System.CommandLine Version

**Solution:** We use 2.0.0 (final). Check `Directory.Packages.props`:

```xml
<PackageVersion Include="System.CommandLine" Version="2.0.0" />
```

---

## Configuration Files

### Directory.Build.props

Central MSBuild properties for all projects:
- Target framework (net10.0)
- Nullable enabled
- TreatWarningsAsErrors
- Package metadata

### Directory.Packages.props

Central Package Management (CPM):
- All package versions in one place
- Ensures consistency across projects
- Update version once, applies everywhere

### global.json

Pins .NET SDK version:
```json
{
  "sdk": {
    "version": "10.0.100-preview.7.25364.1"
  }
}
```

### .editorconfig

Code style rules:
- C# 14 best practices
- Formatting rules
- Naming conventions
- Analyzer settings

---

## Environment Variables

### Optional Configuration

```bash
# Set log level (via logging.json or environment)
REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug

# Plugin directory is auto-discovered from project folder
```

---

## Common Commands

```bash
# Full clean rebuild
dotnet clean && dotnet restore && dotnet build

# Run all tests
dotnet test

# Pack all projects
dotnet pack -c Release

# List outdated packages
dotnet list package --outdated

# Check for updates (recommended: use dotnet-outdated tool)
dotnet outdated
```

---

## Next Steps

1. Read `DEVELOPMENT.md` for current status
2. Check "Next Steps" section for what to implement
3. Read `docs/architecture.md` for design decisions
4. Start coding! 🚀

---

**Questions?** Check:
- `DEVELOPMENT.md` - What's done, what's next
- `docs/architecture.md` - Design decisions
- `.github/copilot-instructions.md` - AI assistant context
