# Project Structure & Build Configuration

> Based on [David Fowler's .NET project structure](https://gist.github.com/davidfowl/ed7564297c61fe9ab814)

## Repository Layout

```
Spectara.Revela/
├── Directory.Build.props           # Central build config (all projects)
├── Directory.Build.targets         # Conditional SDK targets import
├── Directory.Packages.props        # Central package version management
├── Spectara.Revela.slnx            # Solution file (XML format)
├── global.json                     # SDK version pin
├── NuGet.Config                    # NuGet source configuration
├── .editorconfig                   # Code style rules
├── coverage.config                 # Test coverage settings
│
├── src/                            # Production code
│   ├── Sdk/                        # Plugin/theme development SDK
│   ├── Sdk.Generators/             # Roslyn source generators
│   ├── Core/                       # Shared kernel (services, package loading)
│   ├── Commands/                   # Host-owned CLI commands (Config, Info, Packages, …)
│   ├── Features/                   # Always built-in features (Generate, Packages, Theme)
│   ├── Cli/                        # Entry point, hosting (dynamic plugin loading)
│   ├── Cli.Embedded/               # Entry point (static plugin references)
│   ├── Plugins/
│   │   ├── Directory.Build.props   # Inserts "Plugins" namespace segment
│   │   ├── Calendar/
│   │   ├── Compress/
│   │   ├── Serve/
│   │   ├── Source/
│   │   │   ├── OneDrive/
│   │   │   └── Calendar/
│   │   └── Statistics/
│   └── Themes/
│       ├── Directory.Build.props   # Inserts "Themes" namespace segment
│       ├── Lumina/
│       ├── Lumina.Calendar/
│       └── Lumina.Statistics/
│
├── tests/                          # Test code
│   ├── Directory.Build.props       # Inserts "Tests" namespace segment
│   ├── Shared/                     # Shared test utilities & fixtures
│   ├── Core/
│   ├── Commands/
│   ├── Integration/
│   ├── Sdk.Generators/             # Roslyn driver tests for the source generators
│   └── Plugins/
│       ├── Directory.Build.props   # Inserts "Tests.Plugins" namespace segment
│       ├── Compress/
│       ├── Serve/
│       ├── Source/
│       │   └── OneDrive/
│       └── Statistics/
│
├── benchmarks/                     # Performance benchmarks
│   ├── Directory.Build.props       # Inserts "Benchmarks" segment, relaxes analysis
│   └── ImageProcessing/
│
├── samples/                        # Example projects (not in solution)
│   ├── showcase/                   # Feature demo (Git-tracked test images)
│   ├── onedrive/                   # Real-world OneDrive source
│   ├── calendar/                   # Calendar source + theme extension demo
│   └── revela-website/             # Project homepage (theme customization example)
│
├── scripts/                        # Build & test automation
├── docs/                           # Documentation
└── artifacts/                      # Build output (gitignored)
    ├── bin/                        # Compiled binaries
    ├── obj/                        # Intermediate files
    └── packages/                   # NuGet packages
```

### A Revela Site Project

Each site project (for example `samples/showcase/`) has this layout; the fixed folder
names are defined once in [`ProjectPaths`](../src/Sdk/ProjectPaths.cs):

```
my-site/
├── project.json, site.json        # Configuration
├── source/                        # Photos and pages (paths.source)
├── output/                        # Generated site, published as is (paths.output)
├── themes/, plugins/              # Optional local theme files and plugin settings
└── .revela/                       # Revela's own data, never published (gitignored)
    ├── core/                      # manifest.json (cache), images.json (with the output)
    ├── statistics/, calendar/     # Plugin data per page (cache)
    └── compress/                  # ownership.json (with the output)
```

Each folder belongs to one owner (Revela core or a plugin). How long a file lives is
declared per artifact: `clean cache` removes cache artifacts, `clean output` removes the
output with everything that describes it, `clean all` both; durable plugin data is only
removed by its owner's own clean command. See
[Architecture](architecture.md#artifact-ownership).

## Namespace Convention

A single property `RevelaNamespacePrefix` (defined in root `Directory.Build.props`) drives all naming.
Each subfolder's `Directory.Build.props` inserts its segment between the prefix and the project name.

```
{RevelaNamespacePrefix}.{Segment}.{MSBuildProjectName}
```

| Location | Segment | Example |
|----------|---------|---------|
| `src/` | *(none)* | `Spectara.Revela.Core` |
| `src/Plugins/` | `Plugins` | `Spectara.Revela.Plugins.Compress` |
| `src/Themes/` | `Themes` | `Spectara.Revela.Themes.Lumina` |
| `tests/` | `Tests` | `Spectara.Revela.Tests.Core` |
| `tests/Plugins/` | `Tests.Plugins` | `Spectara.Revela.Tests.Plugins.Compress` |
| `benchmarks/` | `Benchmarks` | `Spectara.Revela.Benchmarks.ImageProcessing` |

**Exceptions:**
- `src/Cli/` overrides `AssemblyName` to `revela` (executable name)
- `src/Sdk/` sets explicit `PackageId` = `Spectara.Revela.Sdk`

## Build Configuration Files

### `Directory.Build.props` (Root)

Central configuration inherited by **all** projects:

- **`RevelaNamespacePrefix`** — `Spectara.Revela` (single source of truth)
- **`AssemblyName`** / **`RootNamespace`** — auto-generated from prefix + project name
- **Target framework** — `net10.0`, C# 14, nullable enabled
- **Code analysis** — `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel=latest-all`
- **Package metadata** — Authors, License, Repository URL, etc.
- **Versioning** — `VersionPrefix` with `-dev` suffix in Debug
- **Build output** — Redirected to `artifacts/bin/` and `artifacts/obj/`
- **InternalsVisibleTo** — Auto-registers `{Prefix}.Tests.{ProjectName}` for each project
- **Global usings** — Common namespaces (System, Linq, Logging, etc.)

### `Directory.Build.targets` (Root)

Conditionally imports `Spectara.Revela.Sdk.targets` for plugin/theme projects during local development. When consumed as a NuGet package, the SDK targets are auto-imported by NuGet.

### `Directory.Packages.props` (Root)

Central package version management (`ManagePackageVersionsCentrally`). All NuGet package versions are defined here — individual csproj files reference packages without version numbers.

### Subfolder `Directory.Build.props`

Each subfolder's props file follows the same pattern:

```xml
<Project>
  <!-- Chain to parent (do NOT skip levels) -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <!-- Insert segment into namespace -->
  <PropertyGroup>
    <AssemblyName>$(RevelaNamespacePrefix).{Segment}.$(MSBuildProjectName)</AssemblyName>
    <RootNamespace>$(AssemblyName)</RootNamespace>
  </PropertyGroup>

  <!-- Optional: folder-specific overrides -->
</Project>
```

**Important:** The `<Import>` must reference the **parent** directory (`../`), not the root. MSBuild walks up the tree automatically — each level imports the next higher one.

#### Folder-specific overrides

| Folder | Additional Properties |
|--------|----------------------|
| `src/Plugins/` | `PackageId = $(AssemblyName)` |
| `src/Themes/` | `PackageId = $(AssemblyName)` |
| `tests/` | `IsPackable = false`, custom output paths (`Tests.{Name}`) |
| `tests/Plugins/` | *(inherits from tests/)* |
| `benchmarks/` | `IsPackable = false`, relaxed analysis (`TreatWarningsAsErrors = false`) |

### Output Path Collision Avoidance

Tests and source projects can share the same `MSBuildProjectName` (e.g., both `src/Core/` and `tests/Core/` have project name `Core`). To prevent output directory collisions, `tests/Directory.Build.props` prefixes output paths:

```xml
<BaseOutputPath>artifacts/bin/Tests.$(MSBuildProjectName)/</BaseOutputPath>
<BaseIntermediateOutputPath>artifacts/obj/Tests.$(MSBuildProjectName)/</BaseIntermediateOutputPath>
```

## Adding a New Project

### New source project (`src/{Name}/`)

1. Create folder and `{Name}.csproj` — namespace is auto-generated
2. Add to `Spectara.Revela.slnx`
3. Add test project in `tests/{Name}/`

### New plugin (`src/Plugins/{Name}/`)

1. Create folder and `{Name}.csproj` with `<PackageType>RevelaPlugin</PackageType>`
2. Namespace becomes `Spectara.Revela.Plugins.{Name}` automatically
3. Add test project in `tests/Plugins/{Name}/`

### New test project (`tests/{Name}/`)

1. Create folder and `{Name}.csproj` — namespace becomes `Spectara.Revela.Tests.{Name}`
2. `IsPackable`, `InternalsVisibleTo`, and output paths are handled by Directory.Build.props
3. Add to `Spectara.Revela.slnx`

### New benchmark (`benchmarks/{Name}/`)

1. Create folder and `{Name}.csproj` with `<OutputType>Exe</OutputType>`
2. Namespace becomes `Spectara.Revela.Benchmarks.{Name}` automatically
3. Code analysis is relaxed (no warnings-as-errors)
