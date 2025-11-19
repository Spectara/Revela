# Setup & Build Instructions

## 🔄 Migrating from Original Expose?

If you're coming from the **original Bash-based Expose** (https://github.com/kirkone/Expose):

1. ✅ **This is a complete rewrite** - Not a drop-in replacement
2. ✅ **Content stays the same** - `content/` folder structure unchanged
3. ✅ **Config changes** - `config.sh` → `expose.json` (see migration guide)
4. ✅ **Templates change** - Mustache → Scriban (see template guide)
5. ✅ **Output identical** - Generated sites should look the same

**See:** `docs/architecture.md` (Migration section) for detailed migration guide.

---

## Prerequisites

- **.NET 10 SDK** (or later)
- **Git**
- **Visual Studio 2022** / **VS Code** / **Rider** (optional, but recommended)

---

## Initial Setup

### 1. Clone Repository

```bash
git clone https://github.com/yourname/expose.git
cd expose
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
dotnet run --project src/Expose.Cli -- --help

# Generate site (when implemented)
dotnet run --project src/Expose.Cli -- generate -p ./samples/example-site

# Plugin commands (when implemented)
dotnet run --project src/Expose.Cli -- plugin list
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

1. Open `Expose.sln`
2. Set `Expose.Cli` as startup project
3. Configure command-line arguments:
   - Right-click `Expose.Cli` → Properties → Debug
   - Add arguments: `generate -p samples/example-site`

### Visual Studio Code

1. Open folder in VS Code
2. Install extensions:
   - C# Dev Kit
   - .NET Install Tool
3. Use tasks (`.vscode/tasks.json` if created)

### Rider

1. Open `Expose.sln`
2. Set `Expose.Cli` as startup project
3. Edit run configuration to add arguments

---

## Project Structure

```
Expose/
├── src/                          # Source code
│   ├── Expose.Core/              # Core models & abstractions
│   ├── Expose.Infrastructure/    # External services (NetVips, Scriban)
│   ├── Expose.Features/          # Feature implementations
│   ├── Expose.Cli/               # CLI entry point
│   └── Expose.Plugins/           # Optional plugins
│
├── tests/                        # Test projects
│   ├── Expose.Core.Tests/
│   └── Expose.IntegrationTests/
│
├── docs/                         # Documentation
│   ├── architecture.md
│   └── setup.md (this file)
│
├── samples/                      # Example projects
│   └── example-site/
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
└── Expose.sln                    # Solution file
```

---

## Publishing

### Package as .NET Tool

```bash
# Pack
dotnet pack src/Expose.Cli -c Release

# Output: artifacts/packages/Expose.1.0.0.nupkg
```

### Install Locally

```bash
# Install from local package
dotnet tool install -g --add-source ./artifacts/packages Expose

# Or uninstall first
dotnet tool uninstall -g Expose
dotnet tool install -g --add-source ./artifacts/packages Expose
```

### Test Installed Tool

```bash
expose --help
expose generate -p path/to/site
```

### Publish to NuGet.org

```bash
# Requires NuGet API key
dotnet nuget push artifacts/packages/Expose.1.0.0.nupkg \
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
- `NetVips` (3.0.0)
- `NetVips.Native` (8.15.3)

### Code Style Errors (CA1848, IDE0055)

**Solution:** Either fix the warnings or temporarily disable

```xml
<!-- In Directory.Build.props -->
<PropertyGroup>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
</PropertyGroup>
```

### System.CommandLine Beta Version

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
    "version": "10.0.100"
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
# Set log level
export EXPOSE_LOG_LEVEL=Debug

# Set plugin directory (override default)
export EXPOSE_PLUGIN_DIR=/custom/path
```

---

## Common Commands

```bash
# Full clean rebuild
dotnet clean && dotnet restore && dotnet build

# Run all tests with coverage
dotnet test /p:CollectCoverage=true

# Pack all projects
dotnet pack -c Release

# List outdated packages
dotnet list package --outdated

# Update all packages (careful!)
# Edit Directory.Packages.props manually
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
