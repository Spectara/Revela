# Plugin System v2 — Architecture Design

> **Status:** Approved design, not yet implemented  
> **Date:** 2026-03-17  
> **Scope:** Restructure Revela so all features are plugins with a minimal host

---

## Motivation

1. **MCP Server & GUI** need access to core services (Scan, Generate, Render) — currently locked behind `internal` in the `Commands` project
2. **CLI, MCP, GUI** should be equal frontends on the same service layer
3. **Distribution** should use one mechanism (NuGet packages) instead of compiled-in vs. external plugins
4. **Extensibility** — third-party plugins should be able to create new command categories without host changes

## Core Principle: Everything is a Plugin

The `Commands` project (currently a special, non-packable project) is dissolved. All functionality moves into plugins. The host becomes a minimal shell.

```
CURRENT                                 TARGET
───────                                 ──────
Cli (Host)                              Cli (Host) — minimal
├── Commands/ (special project)         ├── Plugin Loader
│   ├── Generate/                       ├── Plugin Manager (install/remove)
│   ├── Config/                         ├── DI Container
│   ├── Clean/                          ├── CLI Parser
│   ├── Theme/                          └── Nothing else
│   ├── Plugins/
│   └── Projects/                       Plugins (ALL equal, ALL NuGet):
├── Plugins/ (different treatment)      ├── Generate
│   ├── Serve                           ├── Theme
│   ├── Compress                        ├── Projects
│   ├── Statistics                      ├── Serve
│   └── ...                             ├── Compress
└── Core (internal infra)               ├── Statistics
                                        ├── MCP
                                        └── ...
```

---

## Architecture Layers

### Layer 1: SDK (NuGet: `Spectara.Revela.Sdk`)

Public API for ALL plugins — stable contract.

```
src/Sdk/
├── Abstractions/               ← Existing (unchanged)
│   ├── IPlugin.cs
│   ├── IGenerateStep.cs
│   ├── IPageTemplate.cs
│   ├── IManifestRepository.cs
│   ├── IConfigService.cs
│   ├── IFileHashService.cs
│   ├── IPluginContext.cs
│   └── IWizardStep.cs
│
├── Abstractions/Engine/        ← NEW: High-level operations
│   └── IRevelaEngine.cs
│
├── Services/                   ← Existing + MOVED from Core
│   ├── IPathResolver.cs
│   ├── IThemeResolver.cs       ← MOVED from Core (plugins need it)
│   ├── IAssetResolver.cs       ← MOVED from Core
│   ├── ITemplateResolver.cs    ← MOVED from Core
│   ├── IStaticFileService.cs   ← MOVED from Core
│   └── IGlobalConfigManager.cs ← MOVED from Core
│
├── Models/                     ← Existing (Manifest models stay here)
│   ├── Manifest/
│   │   ├── ManifestEntry.cs
│   │   ├── ImageContent.cs
│   │   ├── GalleryContent.cs
│   │   ├── MarkdownContent.cs
│   │   └── ManifestMeta.cs
│   ├── ExifData.cs
│   └── Engine/                 ← NEW: IRevelaEngine result types
│       ├── ScanResult.cs
│       ├── PagesResult.cs
│       ├── ImagesResult.cs
│       ├── GenerateResult.cs
│       ├── ScanProgress.cs
│       ├── PagesProgress.cs
│       └── ImagesProgress.cs
│
└── Configuration/              ← Existing
    ├── ProjectConfig.cs
    ├── ThemeConfig.cs
    ├── GenerateConfig.cs
    └── PathsConfig.cs
```

#### Core Interfaces → SDK Migration

Plugins currently cannot reference Core (it's not a NuGet package). These interfaces
must move to SDK so plugins can use them:

| Interface | Current Location | Used By |
|-----------|-----------------|--------|
| `IThemeResolver` | Core/Services | Generate Plugin, Theme Plugin |
| `IAssetResolver` | Core/Services | Generate Plugin |
| `ITemplateResolver` | Core/Services | Generate Plugin |
| `IStaticFileService` | Core/Services | Generate Plugin |
| `IGlobalConfigManager` | Core/Services | Host (Wizards), Config commands |

Implementations stay in Core — only interfaces move to SDK.

#### Model Boundary: SDK vs Generate Plugin

SDK models are the **persisted/public** data. Generate-internal models are **pipeline/render** data.

```
SDK Models (public):                Generate Plugin Models (internal):
─────────────────────               ─────────────────────────────────
ManifestEntry (tree node)           Gallery (working model with body/template)
ImageContent (persisted metadata)   Image (template model with variants/placeholder)
ExifData (camera metadata)          NavigationItem (nav tree for templates)
GalleryContent (polymorphic base)   SourceImage, SourceMarkdown (raw discovery)
                                    ContentTree, DirectoryMetadata
ScanResult, PagesResult (engine)    RenderContext, SiteModel (render pipeline)
ImagesResult, GenerateResult        ImageProcessingOptions
*Progress types (engine)            WorkerState, VariantResult
```

**Gallery and Image stay INTERNAL** in the Generate Plugin — they are template/render-specific.
`IRevelaEngine` returns `ScanResult` (counts and status), not the internal `Gallery` objects.

#### IRevelaEngine — New Facade

```csharp
namespace Spectara.Revela.Sdk.Abstractions.Engine;

public interface IRevelaEngine
{
    Task<ScanResult> ScanAsync(
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<PagesResult> GeneratePagesAsync(
        IProgress<PagesProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ImagesResult> GenerateImagesAsync(
        bool force = false,
        IProgress<ImagesProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GenerateResult> GenerateAllAsync(
        IProgress<GenerateProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

**Why in SDK (not a separate package)?**
- It's a high-level facade ("generate my site"), not low-level internals
- Third-party plugins MAY use it (e.g., a plugin that auto-regenerates after upload)
- Internal implementation (IContentService, IImageProcessor) stays hidden
- One less package to maintain

### Layer 2: Core (internal, not packable)

Plugin hosting infrastructure. NOT a NuGet package. Interfaces moved to SDK,
implementations stay here.

```
src/Core/
├── PluginLoader.cs             ← Load plugin DLLs from plugins/ directory
├── PluginManager.cs            ← Install/remove/update via NuGet
├── Extensions/                 ← DI registration
├── Services/
│   ├── GlobalConfigManager.cs  ← impl of IGlobalConfigManager (interface → SDK)
│   ├── TemplateResolver.cs     ← impl of ITemplateResolver (interface → SDK)
│   ├── AssetResolver.cs        ← impl of IAssetResolver (interface → SDK)
│   ├── StaticFileService.cs    ← impl of IStaticFileService (interface → SDK)
│   ├── IPackageIndexService.cs ← Package index (stays here, host-only)
│   └── INuGetSourceManager.cs  ← NuGet feeds (stays here, host-only)
└── Configuration/              ← Config loading
```

### Layer 3: Plugins (ALL NuGet packages)

Every plugin follows the same pattern. No exceptions.

```
src/Plugins/
│
├── Generate/                   ← Core generation pipeline (~50 files)
│   ├── GeneratePlugin.cs
│   ├── Services/
│   │   ├── ContentService.cs       (internal IContentService)
│   │   ├── ImageService.cs         (internal IImageService)
│   │   ├── RenderService.cs        (internal IRenderService)
│   │   ├── RevelaEngine.cs         (implements SDK IRevelaEngine)
│   │   ├── MarkdownService.cs      (Markdig wrapper)
│   │   ├── ManifestService.cs      (IManifestRepository impl)
│   │   ├── SitemapGenerator.cs
│   │   └── ContentImageExtension.cs (Markdig extension)
│   ├── Commands/
│   │   ├── ScanCommand.cs          (CLI UI + IGenerateStep)
│   │   ├── PagesCommand.cs         (CLI UI + IGenerateStep)
│   │   ├── ImagesCommand.cs        (CLI UI + IGenerateStep)
│   │   ├── AllCommand.cs           (pipeline orchestrator)
│   │   ├── GenerateCommand.cs      (parent command)
│   │   ├── CreatePageCommand.cs    (revela create page)
│   │   ├── CleanOutputCommand.cs   (ParentCommand: "clean", IsSequentialStep)
│   │   ├── CleanCacheCommand.cs    (ParentCommand: "clean", IsSequentialStep)
│   │   ├── CleanImagesCommand.cs   (ParentCommand: "clean", IsSequentialStep)
│   │   ├── ConfigGenerateCommand.cs (ParentCommand: "config")
│   │   ├── ConfigImageCommand.cs   (ParentCommand: "config")
│   │   ├── ConfigSortingCommand.cs (ParentCommand: "config")
│   │   └── ConfigPathsCommand.cs   (ParentCommand: "config")
│   ├── Infrastructure/
│   │   ├── NetVipsImageProcessor.cs
│   │   ├── ScribanTemplateEngine.cs
│   │   ├── ContentScanner.cs
│   │   ├── NavigationBuilder.cs
│   │   ├── CameraModelMapper.cs
│   │   ├── GallerySorter.cs
│   │   ├── UrlBuilder.cs
│   │   └── RevelaParser.cs     (YAML frontmatter)
│   ├── Filtering/              (image filter DSL — 9 files)
│   │   ├── FilterLexer.cs
│   │   ├── FilterParser.cs
│   │   ├── FilterService.cs
│   │   ├── FilterExpressionBuilder.cs
│   │   └── Ast/ (FilterNode, BinaryNode, UnaryNode, etc.)
│   ├── Models/                 (internal pipeline models)
│   │   ├── Gallery.cs, Image.cs, NavigationItem.cs
│   │   ├── SourceImage.cs, SourceMarkdown.cs
│   │   ├── ContentTree.cs, DirectoryMetadata.cs
│   │   ├── SiteModel.cs, RenderContext.cs
│   │   └── ImageProcessingOptions.cs
│   └── Templates/
│       ├── GalleryPageTemplate.cs  (IPageTemplate)
│       └── TextPageTemplate.cs     (IPageTemplate)
│
├── Theme/                      ← Theme management
│   ├── ThemePlugin.cs
│   └── Commands/
│       ├── ThemeListCommand.cs
│       ├── ThemeFilesCommand.cs
│       ├── ThemeExtractCommand.cs
│       ├── ThemeInstallCommand.cs
│       ├── ThemeUninstallCommand.cs
│       └── ConfigThemeCommand.cs   (ParentCommand: "config")
│
├── Projects/                   ← Project management
│   ├── ProjectsPlugin.cs
│   └── Commands/
│       ├── ProjectsListCommand.cs
│       ├── ProjectsCreateCommand.cs
│       └── ProjectsDeleteCommand.cs
│
├── Serve/                      ← Dev server (existing, unchanged)
├── Compress/                   ← Gzip/Brotli (existing, unchanged)
├── Statistics/                 ← EXIF stats (existing, unchanged)
├── Calendar/                   ← Calendar (existing, unchanged)
├── Source/OneDrive/            ← OneDrive sync (existing, unchanged)
├── Source/Calendar/            ← iCal fetch (existing, unchanged)
├── Mcp/                        ← MCP Server (NEW, future)
└── Gui/                        ← Blazor Web UI (NEW, future)
```

#### NuGet Dependencies per Plugin

| Plugin | NuGet Dependencies (beyond SDK) |
|--------|---------------------------------|
| **Generate** | NetVips, NetVips.Native, Scriban, Markdig, Spectre.Console |
| **Theme** | Spectre.Console, Http |
| **Projects** | Spectre.Console |
| **Serve** | (HTTP server libs) |
| **Compress** | (none beyond .NET) |
| **Statistics** | Spectre.Console |
| **MCP** | ModelContextProtocol |

### Layer 4: Themes (NuGet packages)

Unchanged. Themes reference only SDK.

---

## What Stays Internal

These interfaces/classes are implementation details BEHIND `IRevelaEngine`:

| Interface/Class | Location | Purpose |
|-----------|----------|--------|
| `IContentService` | Generate Plugin | Filesystem scanning, manifest building |
| `IImageService` | Generate Plugin | Batch image processing |
| `IRenderService` | Generate Plugin | HTML page rendering |
| `IImageProcessor` | Generate Plugin | NetVips wrapper |
| `ITemplateEngine` | Generate Plugin | Scriban wrapper |
| `IMarkdownService` | Generate Plugin | Markdig wrapper |
| `ContentScanner` | Generate Plugin | Directory recursion |
| `NavigationBuilder` | Generate Plugin | Tree construction |
| `RevelaParser` | Generate Plugin | YAML frontmatter extraction |
| `GallerySorter` | Generate Plugin | Image sorting logic |
| `UrlBuilder` | Generate Plugin | URL generation |
| `CameraModelMapper` | Generate Plugin | EXIF camera ID mapping |
| `FilterLexer/Parser/Service` | Generate Plugin | Image filter DSL (9 files) |
| `IConfigService` | Generate Plugin | JSON property editing |
| `IDependencyScanner` | Host | Dependency detection |

Plugins that need to trigger generation use `IRevelaEngine` from the SDK.

### What Moves to SDK (currently in Core)

| Interface | Why Plugins Need It |
|-----------|--------------------|
| `IThemeResolver` | Generate Plugin resolves theme packages |
| `IAssetResolver` | Generate Plugin copies CSS/JS/assets |
| `ITemplateResolver` | Generate Plugin finds template files |
| `IStaticFileService` | Generate Plugin copies static files |
| `IGlobalConfigManager` | Wizards, config commands read/write revela.json |

---

## Host Responsibilities

The Cli project (host) contains ONLY:

```
src/Cli/
├── Program.cs                  ← Entry point
├── Hosting/
│   ├── HostBuilderExtensions.cs  ← Config loading, DI setup
│   ├── ProjectResolver.cs       ← --project arg parsing
│   ├── InteractiveMenuService.cs ← Interactive mode
│   ├── RevelaWizard.cs           ← First-run setup (global)
│   └── ProjectWizard.cs          ← Project creation wizard
└── (references Core for PluginLoader + PluginManager)
```

### What the host handles (without plugins):

```
revela                          → First-Run wizard / Interactive menu
revela plugins install <name>   → NuGet download to plugins/
revela plugins remove <name>    → Delete from plugins/
revela plugins list             → Show installed plugins
revela plugins update [name]    → Update from NuGet
revela packages search <query>  → Search NuGet for plugins/themes
revela packages refresh         → Refresh package index cache
revela restore                  → Install project dependencies
revela config feed list|add|rm  → Manage NuGet feed sources
revela config project           → Edit project.json (basic)
revela config site              → Edit site.json
revela config locations         → Show config file paths
revela --version                → Show version
revela --help                   → Dynamic help from loaded plugins
```

Plugin management, package search, feed management, and dependency restore
stay in the host (chicken-and-egg: need them before plugins are loaded).

### First-Run Behavior (no Wizard in Host)

Inspired by **Terraform** (`terraform init`) and **Azure CLI** (`az extension add`):
no interactive wizard in the host. Instead, clear CLI instructions.

```
$ revela

  No plugins installed. Get started:

  revela plugins install --recommended    Install recommended set
  revela plugins install --minimal        Install minimal set
  revela plugins install Generate Lumina  Install specific plugins

  See available: revela packages search
```

For CI/CD, plugins are declared in `project.json` and restored automatically:

```bash
$ revela restore    # Reads project.json → installs missing plugins
$ revela generate all
```

### Setup Plugin (optional interactive Wizard)

The full interactive setup experience (Spectre.Console prompts, theme preview,
image config) is an **optional plugin**:

```
revela plugins install Setup   # or: comes pre-installed in Standalone
revela setup                   # Full interactive wizard
```

| Distribution | Setup Plugin | First-Run Experience |
|-------------|-------------|---------------------|
| **Standalone** | Pre-installed | Interactive wizard available immediately |
| **Minimal** | Not installed | `revela plugins install --recommended` |
| **dotnet tool** | Not installed | `revela plugins install --recommended` |

The Setup plugin can use `IWizardStep` from loaded plugins to extend the wizard
(e.g., OneDrive plugin adds "Configure OneDrive URL" step).

### Project Templates (future)

Like `dotnet new` or `gatsby new`, Revela can offer starter templates:

```bash
revela new photography    # Copy pre-configured project template
revela new portfolio      # Different starter
revela new blank          # Empty project
```

Templates are NuGet packages with the `revela-template` tag, discovered the
same way as plugins and themes.

---

## NuGet Package Ecosystem

Three package types, one registry, one discovery mechanism:

```
NuGet Tags:
├── revela-plugin      → Plugins (DLLs → plugins/ → DI load)
├── revela-theme       → Themes (templates + assets → themes/ → IThemePlugin)
└── revela-template    → Project starters (files → copy once → revela restore)
```

### Template Packages (future)

```
Spectara.Revela.Templates.Photography.nupkg
└── content/
    ├── project.json          ← Pre-configured (plugins, theme, image sizes)
    ├── site.json             ← Example site config
    ├── source/
    │   ├── _index.md         ← Home page
    │   ├── landscapes/
    │   │   └── _index.md     ← Example gallery
    │   └── portraits/
    │       └── _index.md
    └── _static/              ← Optional custom CSS
```

```bash
$ revela new photography
# 1. Find Spectara.Revela.Templates.Photography on NuGet
# 2. Extract content/ to current directory
# 3. Run revela restore (install plugins from project.json)
# Done!
```

### Complete NuGet Package List (target state)

```
Packages:
  Spectara.Revela                               ← dotnet tool (host)
  Spectara.Revela.Sdk                           ← plugin developer SDK

Plugins (tag: revela-plugin):
  Spectara.Revela.Plugins.Generate              ← core generation
  Spectara.Revela.Plugins.Theme                 ← theme management
  Spectara.Revela.Plugins.Projects              ← project management
  Spectara.Revela.Plugins.Setup                 ← interactive wizard (optional)
  Spectara.Revela.Plugins.Serve                 ← dev server
  Spectara.Revela.Plugins.Compress              ← gzip/brotli
  Spectara.Revela.Plugins.Statistics            ← EXIF stats
  Spectara.Revela.Plugins.Calendar              ← calendar
  Spectara.Revela.Plugins.Source.OneDrive        ← OneDrive sync
  Spectara.Revela.Plugins.Source.Calendar        ← iCal fetch
  Spectara.Revela.Plugins.Mcp                   ← MCP server (future)
  Spectara.Revela.Plugins.Gui                   ← Blazor UI (future)

Themes (tag: revela-theme):
  Spectara.Revela.Themes.Lumina                 ← default theme
  Spectara.Revela.Themes.Lumina.Calendar        ← calendar extension
  Spectara.Revela.Themes.Lumina.Statistics      ← statistics extension

Templates (tag: revela-template, future):
  Spectara.Revela.Templates.Photography         ← photography portfolio
  Spectara.Revela.Templates.Portfolio            ← generic portfolio
  Spectara.Revela.Templates.Blank                ← empty project
```

---

## Parent Commands & "All" Steps

### Parent commands are auto-created

When a plugin registers `ParentCommand: "deploy"` and "deploy" doesn't exist, the host creates it automatically. This already works today.

### "All" command is opt-in via `IsSequentialStep`

```csharp
public sealed record CommandDescriptor(
    Command Command,
    string? ParentCommand = null,
    int Order = 50,
    string? Group = null,
    bool RequiresProject = true,
    bool HideWhenProjectExists = false,
    bool IsSequentialStep = false    // ← NEW
);
```

When ≥2 commands under the same parent have `IsSequentialStep: true`, the host auto-generates an "all" subcommand that runs them in `Order` sequence.

```
revela generate all      → ✅ (5 steps with IsSequentialStep)
revela clean all         → ✅ (3 steps with IsSequentialStep)
revela config            → ❌ no "all" (no sequential steps)
revela source            → ❌ no "all" (no sequential steps)
revela deploy all        → ✅ if a deploy plugin registers sequential steps
```

**New categories don't need host changes.** A third-party deploy plugin just registers commands with `ParentCommand: "deploy"` and `IsSequentialStep: true`.

---

## Plugin Dependencies

### PluginMetadata Extension

```csharp
public sealed record PluginMetadata
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = "Unknown";

    /// <summary>
    /// Plugin PackageIds that MUST be installed. Host validates before loading.
    /// </summary>
    public string[] RequiredPlugins { get; init; } = [];

    /// <summary>
    /// Plugin PackageIds this plugin optionally extends.
    /// Extension commands only registered if parent plugin is present.
    /// </summary>
    public string[] ExtendsPlugins { get; init; } = [];
}
```

### Validation at load time

```
RequiredPlugins missing  → Plugin NOT loaded, clear error message
ExtendsPlugins missing   → Plugin loads, extension commands skipped, info log
```

### Example

```csharp
// Statistics Plugin
public PluginMetadata Metadata => new()
{
    Name = "Statistics",
    RequiredPlugins = ["Spectara.Revela.Plugins.Generate"],
    ExtendsPlugins = ["Spectara.Revela.Plugins.Clean",
                      "Spectara.Revela.Plugins.Config"]
};
```

### Install-time validation

```
$ revela plugins install MCP

  Checking dependencies...
  ✗ MCP requires: Generate — NOT installed
  
  Install Generate first, or install both:
  → revela plugins install Generate MCP
```

---

## Distribution Model

### One mechanism: NuGet packages in `plugins/` directory

```
Host (revela.exe) = ALWAYS identical build

Standalone.zip = Host + plugins/ filled with NuGet packages
Minimal.zip    = Host + empty plugins/
dotnet tool     = Host + empty plugins/
```

No compiled-in plugins, no MSBuild conditionals, no `RevelaProfile` flags. The only difference between Standalone and Minimal is which plugins are in the folder.

### All plugins published to NuGet

See [NuGet Package Ecosystem](#nuget-package-ecosystem) above for the complete package list.

### Plugin tiers via NuGet tags

```xml
<!-- Tier definitions in .csproj PackageTags -->
<PackageTags>revela-plugin;tier-bundled;category-core</PackageTags>
<PackageTags>revela-plugin;tier-recommended;category-development</PackageTags>
<PackageTags>revela-plugin;tier-optional;category-content</PackageTags>
<PackageTags>revela-theme;tier-recommended</PackageTags>
```

**No custom metadata system.** NuGet tags are the source of truth:
- **Online:** NuGet Search API reads tags (no package download needed)
- **Offline:** `.nuspec` in `.nupkg` can be read without full extraction
- **packages.json** remains as optional cache, not a dependency

### First-Run (Minimal / dotnet tool)

```
$ revela

  Welcome to Revela! No plugins found.

  ● Recommended (Generate, Theme, Serve, Compress, Lumina)
  ○ Minimal (Generate, Theme)
  ○ Full (everything)
  ○ Custom...

  → Plugin sets derived from NuGet tags
  → New plugin with tier-recommended? Automatically in the set!
```

---

## Three Frontends, One Service Layer

```
┌─────────┐   ┌─────────────┐   ┌──────────┐
│   CLI   │   │  MCP Server │   │   GUI    │
│ Generate│   │  Mcp Plugin │   │ Gui      │
│ Plugin  │   │             │   │ Plugin   │
└────┬────┘   └──────┬──────┘   └────┬─────┘
     │               │              │
     └───────────────┼──────────────┘
                     │
            ┌────────▼────────┐
            │  IRevelaEngine   │  (SDK — public)
            └────────┬────────┘
                     │
            ┌────────▼────────┐
            │  RevelaEngine    │  (Generate Plugin — internal impl)
            │  ContentService  │
            │  ImageService    │
            │  RenderService   │
            └─────────────────┘
```

```csharp
// CLI (Generate Plugin — Spectre.Console output)
var result = await engine.ScanAsync(progress, ct);
AnsiConsole.Write(new Panel($"Galleries: {result.GalleryCount}"));

// MCP (Mcp Plugin — JSON response)
var result = await engine.ScanAsync(ct);
return JsonSerializer.Serialize(result);

// GUI (Gui Plugin — data binding)
var result = await engine.ScanAsync(progress, ct);
ViewModel.GalleryCount = result.GalleryCount;
```

---

## Migration Strategy

### Phase 1: SDK Foundation

| Step | What | Files | Risk |
|------|------|-------|------|
| 1.1 | Move Core interfaces to SDK (IThemeResolver, IAssetResolver, ITemplateResolver, IStaticFileService, IGlobalConfigManager) | ~10 files | Medium — namespace changes across codebase |
| 1.2 | Create Engine result/progress types in SDK | ~7 new files | Low — additive |
| 1.3 | Create `IRevelaEngine` interface in SDK | 1 new file | Low — additive |
| 1.4 | Add `IsSequentialStep` to CommandDescriptor | 1 file | Low — additive, default false |
| 1.5 | Add `RequiredPlugins` + `ExtendsPlugins` to PluginMetadata | 1 file | Low — additive, default empty |

### Phase 2: Generate Plugin (largest block)

| Step | What | Files | Risk |
|------|------|-------|------|
| 2.1 | Create Generate Plugin project (.csproj) | 1 new file | Low |
| 2.2 | Move Generate services (Content, Image, Render, Manifest, Markdown, Sitemap) | ~10 files | Medium |
| 2.3 | Move Generate infrastructure (NetVips, Scriban, Scanner, Parser, Builder) | ~8 files | Medium |
| 2.4 | Move Generate models (Gallery, Image, NavItem, etc.) | ~15 files | Medium |
| 2.5 | Move Filtering DSL (Lexer, Parser, AST, Service) | ~9 files | Low — self-contained |
| 2.6 | Move Generate commands (Scan, Pages, Images, All, Create) | ~7 files | Medium |
| 2.7 | Move Clean commands (Output, Cache, Images) into Generate | ~3 files | Low |
| 2.8 | Move Config commands (Images, Sorting, Paths) into Generate | ~3 files | Low |
| 2.9 | Create `RevelaEngine` implementation | 1 new file | Medium — facade over internal services |
| 2.10 | Create `GeneratePlugin.cs` (IPlugin) | 1 new file | Low |

### Phase 3: Remaining Plugins

| Step | What | Files | Risk |
|------|------|-------|------|
| 3.1 | Create Theme Plugin (move Theme commands) | ~7 files | Low |
| 3.2 | Create Projects Plugin (move Projects commands) | ~4 files | Low |
| 3.3 | Move Config commands into respective plugins | ~4 files | Low |
| 3.4 | Move Packages/Restore/Feed commands into Host | ~6 files | Medium |
| 3.5 | Move Wizards into Setup Plugin | ~2 files | Low — clean separation |

### Phase 4: Cleanup & Distribution

| Step | What | Files | Risk |
|------|------|-------|------|
| 4.1 | Remove Commands project | Delete project | Low (if all migrated) |
| 4.2 | Remove CoreCommandProvider (replaced by plugin loading) | 1 file | Low |
| 4.3 | Update host to auto-generate "all" commands | ~2 files | Medium |
| 4.4 | Update build-standalone.ps1 for new structure | 1 file | Medium |
| 4.5 | Update test-release.ps1 | 1 file | Medium |
| 4.6 | Update solution file | 1 file | Low |
| 4.7 | Update CI/CD workflows | ~2 files | Medium |

### Phase 5: New Capabilities

| Step | What | Risk |
|------|------|------|
| 5.1 | Build MCP Plugin | Low — new plugin |
| 5.2 | First-Run wizard for Minimal/dotnet-tool | Medium |

**Total: ~100 files moved/created, 0 external breaking changes.**

The CLI interface stays identical throughout the entire migration.

---

## Complete Feature → Plugin Mapping

| Current Feature | Current Location | Target | Owner |
|----------------|-----------------|--------|-------|
| generate (scan/pages/images/all) | Commands/Generate | **Generate Plugin** | Plugin |
| create page | Commands/Create | **Generate Plugin** | Plugin |
| clean output/cache/images | Commands/Clean | **Generate Plugin** | Plugin |
| config images/sorting/paths | Commands/Config | **Generate Plugin** | Plugin |
| theme list/files/extract/install | Commands/Theme | **Theme Plugin** | Plugin |
| config theme | Commands/Config/Theme | **Theme Plugin** | Plugin |
| projects list/create/delete | Commands/Projects | **Projects Plugin** | Plugin |
| config project | Commands/Config/Project | **Host** | Host |
| config site | Commands/Config/Site | **Host** | Host |
| config locations | Commands/Config/Revela | **Host** | Host |
| config feed list/add/remove | Commands/Config/Feed | **Host** | Host |
| plugins install/remove/list | Commands/Plugins | **Host** | Host |
| packages search/refresh | Commands/Packages | **Host** | Host |
| restore | Commands/Restore | **Host** | Host |
| RevelaWizard | Commands/Revela | **Setup Plugin** | Plugin |
| ProjectWizard | Commands/Project | **Setup Plugin** | Plugin |
| serve | Plugins/Serve | **Serve Plugin** | Plugin (unchanged) |
| compress | Plugins/Compress | **Compress Plugin** | Plugin (unchanged) |
| statistics | Plugins/Statistics | **Statistics Plugin** | Plugin (unchanged) |
| clean statistics | Plugins/Statistics | **Statistics Plugin** | Plugin (unchanged) |
| clean compress | Plugins/Compress | **Compress Plugin** | Plugin (unchanged) |
| config statistics | Plugins/Statistics | **Statistics Plugin** | Plugin (unchanged) |
| config onedrive | Plugins/Source/OneDrive | **OneDrive Plugin** | Plugin (unchanged) |

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Generate Plugin is huge (~50 files)** | Complex migration | Move in sub-steps (services → infra → models → commands) |
| **Core interface move breaks namespaces** | Many files touched | Find-and-replace, run `dotnet build` after each step |
| **Wizard cross-cutting dependencies** | Wizards call multiple plugins | Keep wizards in Host, inject via DI |
| **Plugin load order matters** | MCP needs Generate loaded first | RequiredPlugins validation at load time |
| **Test projects reference Commands** | Tests break | Update test project references incrementally |
| **Filtering DSL is self-contained but large** | 9 files to move | Move as a unit, no refactoring needed |
| **Build scripts assume current structure** | CI breaks | Update scripts in Phase 4, test locally first |

---

## Plugin Listing (Target State)

```
$ revela plugins list

 INSTALLED
  ✓ Generate        Core site generation pipeline
  ✓ Theme           Theme management
  ✓ Projects        Project management
  ✓ Serve           Local development server
  ✓ Compress        Gzip/Brotli pre-compression
  ✓ Statistics      EXIF statistics
  ✓ MCP             AI assistant integration
  ✗ Calendar        Calendar (disabled)

 AVAILABLE
  ○ OneDrive        OneDrive shared folder source
  ○ GUI             Web-based management UI
  ○ Deploy.SFTP     SFTP deployment (third-party)
```
