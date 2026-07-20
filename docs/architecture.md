# Architecture Overview

## Project Background

**Revela** is a complete rewrite of the original Bash-based revela project.

- **Original Project:** https://github.com/kirkone/Expose
- **Original Implementation:** Bash/Shell scripts + various CLI tools
- **New Implementation:** .NET 10 with modern architecture

### Why Rewrite?

1. **Performance** - NetVips library is 3-5× faster than VIPS CLI
2. **Cross-platform** - Native Windows support, better Linux/macOS experience
3. **Extensibility** - Plugin system for community contributions
4. **Maintainability** - Modern codebase, IDE support, static typing
5. **Developer Experience** - Better error messages, progress reporting

### Compatibility Goal

**Generate identical output** - The generated HTML and images should be functionally identical to the Bash version, but internal implementation can differ.

---

## System Architecture

Revela follows **Vertical Slice Architecture** combined with a **Plugin System** for extensibility.

### Core Principles

1. **Features are self-contained** - Each feature (Generate, Plugin Management) is independent
2. **Plugin-based extensibility** - Optional features via NuGet plugins
3. **Performance first** - NetVips for fast image processing
4. **Developer-friendly** - Modern .NET patterns, strongly-typed configuration

---

## Project Structure

```
Revela/
├── src/
│   ├── Sdk/                     # Public abstractions for plugin/theme authors
│   │   ├── Abstractions/        # IPlugin, IPackageSource, IPathResolver, …
│   │   ├── Configuration/       # Bound options (ProjectConfig, SiteCoreConfig, …)
│   │   └── Validation/          # UrlSafety (SSRF guardrails), …
│   │
│   ├── Sdk.Generators/          # Roslyn source generators ([RevelaTemplateModel], …)
│   │
│   ├── Core/                    # Shared kernel — services, package loading, config wiring
│   │
│   ├── Commands/                # Host-owned CLI commands (Config, Info, Packages, …)
│   │
│   ├── Features/                # Always built-in features (NOT plugins)
│   │   ├── Generate/            # Site generation (scan, render, NetVipsImageProcessor)
│   │   ├── Packages/            # DiskPackageSource + package management
│   │   └── Theme/               # Theme management
│   │
│   ├── Plugins/                 # External plugins (NuGet-loaded)
│   │   ├── Calendar/
│   │   ├── Compress/
│   │   ├── Serve/
│   │   ├── Source/
│   │   │   ├── OneDrive/
│   │   │   └── Calendar/
│   │   └── Statistics/
│   │
│   ├── Themes/                  # Lumina (base) + Lumina.Calendar, Lumina.Statistics
│   │
│   ├── Cli/                     # Entry point — dynamic plugin loading (DiskPackageSource)
│   │   └── Hosting/             # HostBootstrap (shared), HostBuilderExtensions, menus
│   │
│   └── Cli.Embedded/            # Entry point — static plugin refs (EmbeddedPackageSource)
│
└── tests/                       # Mirrors src/ + Shared (fixtures)
```

**Two entry points, one bootstrap.** `Cli` and `Cli.Embedded` differ only in their
`IPackageSource` implementation — all shared setup lives in
[`src/Cli/Hosting/HostBootstrap.cs`](../src/Cli/Hosting/HostBootstrap.cs)
(`ConfigureRevela`). `Cli` uses `DiskPackageSource` (runtime discovery); `Cli.Embedded`
uses `EmbeddedPackageSource` (statically referenced plugins, AOT-friendly, F5 debug).

---

## Technology Stack

### Core Framework
- **.NET 10** - Target framework
- **C# 14** - Language version
- **System.CommandLine 2.0.1** - CLI framework

### Image Processing
- **NetVips 3.1.0** - High-performance image processing (libvips wrapper)
- **NetVips.Native 8.17.3** - Native libvips binaries

### Templating
- **Scriban 6.5.2** - Liquid-like template engine
- **Markdig 0.44.0** - Markdown parser

### Configuration
- **Microsoft.Extensions.Configuration** - Configuration system
- **Microsoft.Extensions.Options** - Options pattern

### Logging
- **Microsoft.Extensions.Logging 10.0.1** - Built-in logging abstraction
- **Console & Debug providers** - Standard output

### Plugin Management
- **NuGet.Protocol 7.0.1** - NuGet package installation
- **NuGet.Packaging 7.0.1** - Package extraction

---

## Key Design Decisions

### 1. Why Vertical Slice Architecture?

**Problem:** Traditional layered architecture couples unrelated features.

**Solution:** Each feature is self-contained with its own:
- Commands
- Services  
- Models (if needed)

**Benefits:**
- Easy to add/remove features
- Clear boundaries
- Testable in isolation

### 2. Why Plugin System?

**Problem:** Not all users need deployment or sync features.

**Solution:** Optional features as NuGet packages.

**Benefits:**
- Smaller core installation
- Community can create plugins
- Easy to maintain

**Implementation:**
- Plugins loaded via `IPackageSource` abstraction
- `DiskPackageSource`: runtime discovery from disk (dotnet tool / standalone)
- `EmbeddedPackageSource`: statically linked (debugging / AOT build)
- Commands automatically registered

### 3. Why NetVips over ImageSharp?

| Aspect | NetVips | ImageSharp |
|--------|---------|------------|
| Performance | **3-5× faster** | Good |
| Memory | **Very efficient** | High |
| Formats | 40+ | Standard |
| Large images | ✅ Excellent | ⚠️ RAM limited |

**Decision:** NetVips for photography use case (large images).

### 4. Why Scriban over Razor?

| Aspect | Scriban | Razor |
|--------|---------|-------|
| Standalone | ✅ Yes | ❌ Needs ASP.NET |
| Syntax | Liquid-like (familiar) | C# |
| Performance | Very fast | Fast |
| Security | Sandboxed | Full C# access |

**Decision:** Scriban for static site generation.

### 5. Configuration Strategy

**Pattern:** Options Pattern + layered JSON

Configuration is merged from multiple JSON files plus environment variables and CLI
arguments. Each config section binds to a strongly-typed options class in
[`src/Sdk/Configuration/`](../src/Sdk/Configuration/) (e.g. `ProjectConfig`,
`SiteCoreConfig`, `GenerateConfig`) via
`services.AddOptions<T>().BindConfiguration(T.Section)`.

**Benefits:**
- Strongly-typed
- Validated (`IValidateOptions<T>` validators)
- Testable
- Hot-reload via `IOptionsMonitor<T>`

**Override hierarchy** (later sources win — see
[`HostBuilderExtensions.AddRevelaConfiguration`](../src/Cli/Hosting/HostBuilderExtensions.cs)
and `ConfigurePlugins` in
[`PackageServiceCollectionExtensions`](../src/Core/Extensions/PackageServiceCollectionExtensions.cs)):

1. C# property defaults on the config classes
2. `revela.json` — global user-wide defaults (`%APPDATA%/Revela/`)
3. `project.json` — project-local settings
4. `site.json` — added via `AddSiteJson` (re-keyed under the `site` section)
5. `logging.json` — optional, project-local logging overrides
6. Environment variables (prefix `SPECTARA__REVELA__`)
7. Command-line arguments (highest priority)

**The `site.json` split.** Only the *identity core* of `site.json` (`SiteCoreConfig`:
title, description, language, …) is bound via `IOptions`. The remaining
theme-specific properties are **not** modelled as options — `RenderService` loads them
dynamically as a `JsonElement`, so themes can define custom properties without a fixed
schema. Configurable filesystem paths (`source`, `output`) resolve through
`IPathResolver`; fixed paths (`Cache`, `Themes`, `Plugins`, …) come from `ProjectPaths`.

### 6. Package & Plugin Discovery

Plugins and themes are loaded through the `IPackageSource` abstraction, chosen by the
entry point:

- `DiskPackageSource` — discovers packages from disk (application directory + the user
  plugin directory `%APPDATA%/Revela/plugins`). Used by `Cli`.
- `EmbeddedPackageSource` — returns statically referenced plugin/theme assemblies. Used
  by `Cli.Embedded` (AOT-friendly, F5 debugging).

```
%APPDATA%/Revela/plugins/
└── Spectara.Revela.Plugins.Source.OneDrive/
    ├── Spectara.Revela.Plugins.Source.OneDrive.dll
    └── dependencies...
```

**Discovery process:**
1. `IPackageSource.LoadPlugins()` returns the available `LoadedPluginInfo` set
2. Each `IPlugin` runs `ConfigureConfiguration` (optional) then `ConfigureServices`
3. After the host is built, `GetCommands(IServiceProvider)` yields `CommandDescriptor`s
4. Commands are registered into the System.CommandLine tree

---

## Data Flow

### Site Generation Flow

```
User runs: revela generate all

1. Load Configuration
   revela.json → project.json → site.json → env → args (typed options)

2. Discover Content
   content/ → Images + Markdown

3. Process Images (NetVips)
   *.jpg → Multiple formats/sizes

4. Extract EXIF (NetVips)
   Images → ExifData

5. Parse Markdown (Markdig)
   *.md → HTML

6. Build Navigation
   Directories → NavigationTree

7. Render Templates (Scriban)
   layouts/ + data → HTML

8. Output Site
   → output/ directory
```

### Plugin Installation Flow

```
User runs: revela plugin install Source.OneDrive

1. Name Transformation
   Source.OneDrive → Spectara.Revela.Plugins.Source.OneDrive

2. Multi-Source Discovery
   Try configured NuGet sources in order:
   - nuget.org (built-in)
   - GitHub Packages (if configured)
   - Custom feeds (if configured)

3. Download Package
   Download .nupkg to temp file

4. Extract lib/net10.0/*.dll
   Extract plugin DLL + dependencies to:
   - Local: ./plugins/
   - Global: %APPDATA%/Revela/plugins/

5. Create plugin.meta.json
   Parse .nuspec metadata:
   - Name, Version, Authors
   - Description, Dependencies
   - Source (nuget/nupkg/url)
   - Installation timestamp

6. Update project.json
   Add plugin to "plugins" dictionary:
   "Spectara.Revela.Plugins.Source.OneDrive": "1.0.0"

7. Success
   Plugin available immediately (no restart needed)
```

### Plugin Restore Flow

```
User runs: revela restore

1. Read project.json
   Get "plugins" dictionary

2. Check Installed
   Compare with ./plugins/ directory

3. Install Missing (Parallel)
   - Use Parallel.ForEachAsync (4 concurrent)
   - Show progress bar (Spectre.Console)
   - Try all configured NuGet sources

4. Report Results
   - Success count
   - Failed plugins with errors
   - Exit code 1 if any failures
```

### NuGet Source Management

```
# List all configured sources
revela plugin source list

# Add custom source (GitHub Packages)
revela plugin source add --name github --url https://nuget.pkg.github.com/kirkone/index.json

# Add custom source (Private feed)
revela plugin source add --name myfeed --url https://my-nuget-feed.com/v3/index.json

# Remove custom source
revela plugin source remove github

# Install from specific source
revela plugin install OneDrive --source github
```

**Source Resolution:**
- Name → URL lookup in `%APPDATA%/Revela/nuget-sources.json`
- Direct URL also supported: `--source https://...`
- Multi-source fallback if no --source specified

---

## Extension Points

### 1. Custom Plugins

Implement `IPlugin` interface:

```csharp
public class MyPlugin : IPlugin
{
    public PluginMetadata Metadata => new()
    {
        Name = "My Plugin",
        Version = "1.0.0",
        Description = "Plugin description",
        Author = "Your Name"
    };
    
    // Register services BEFORE ServiceProvider is built
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<MyHttpService>();
        services.AddOptions<MyConfig>()
            .BindConfiguration(MyConfig.SectionName)
            .ValidateDataAnnotations();
    }
    
    // Return CommandDescriptors (command + optional parent)
    // IServiceProvider passed as parameter — no stored field needed
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var cmd = services.GetRequiredService<MyCmdCommand>();
        // ParentCommand: null = root level, "init" = under init, etc.
        yield return new CommandDescriptor(
            cmd.Create(),
            ParentCommand: null);  // revela mycmd
    }
}
```

### 2. Custom Template Functions

Register with Scriban:

```csharp
var scriptObject = new ScriptObject();
scriptObject.Import("my_function", new Func<string, string>(MyFunction));
```

### 3. Custom Image Processors

Implement `IImageProcessor`:

```csharp
public class CustomProcessor : IImageProcessor
{
    public Task<Image> ProcessImageAsync(...)
    {
        // Custom processing
    }
}
```

---

## Security Considerations

### 1. Template Sandboxing

- Scriban runs in restricted mode
- No file system access from templates
- Limited function set

### 2. Plugin Trust

- Plugins run in same process (trusted)
- Users install explicitly
- Verified via NuGet signatures (future)

### 3. EXIF Data

- **Read** into the in-memory `ImageManifest` for display and statistics
- **Stripped** from every published variant — the writer saves with
  `keep: ForeignKeep.None` (JPEG/WebP/AVIF/PNG), removing EXIF, XMP, ICC, and GPS
- No embedded metadata (including home GPS coordinates) leaks into the output files
- See [`docs/security-model.md`](security-model.md) for the full rationale

---

## Performance Considerations

### 1. Image Processing

- **Parallel processing** - Uses `Parallel.ForEachAsync` (default `Environment.ProcessorCount - 2`), configurable via `generate.images.maxDegreeOfParallelism`
- **Thread-safe** - LibVips is thread-safe for independent images
- **No global lock** - Each image processed independently
- **Streaming** - NetVips streams large images (low memory)
- **Caching** - Hash-based manifest skips unchanged images
- **Performance:** ~5× speedup vs sequential (tested: 94s → 18s for 23 images)

### 2. Template Rendering

- **Compiled templates** - Scriban compiles once
- **Partial caching** - Reuse common partials
- **Parallel rendering** - Configurable via `generate.render.parallel` (default `false`) and `generate.render.maxDegreeOfParallelism`

### 3. Plugin Loading

- **Lazy loading** - Only when directory exists
- **Cached assemblies** - Load once per run

---

## Testing Strategy

### Unit Tests
- Core models
- Configuration validation
- Plugin discovery logic

### Integration Tests
- Image processing with NetVips
- Template rendering with Scriban
- Plugin installation

### End-to-End Tests
- Complete site generation
- CLI command execution

---

## Migration from Original Expose

### Original vs. New Architecture

#### Original (Bash)
```
Spectara.Revela.sh
├── config.sh              # Bash variables
├── build.sh               # Main build script
├── templates/
│   └── *.mustache         # Mustache-like templates (regex-based)
├── tools/
│   ├── vips (CLI)         # Image processing
│   ├── exiftool (CLI)     # EXIF extraction
│   └── markdown.pl        # Perl markdown parser
└── output/
```

#### New (.NET)
```
project root
├── revela.json            # Global user-wide defaults (%APPDATA%/Revela/)
├── project.json           # Project-local settings (typed options)
├── site.json              # Site identity + theme-specific properties
├── themes/
│   └── *.revela           # Scriban templates (full-featured)
├── NetVips (library)      # In-process image processing
├── Markdig (library)      # Native C# markdown
└── output/
```

### What Stays the Same

- **Content structure** - `content/` folder organization
- **Output structure** - Same HTML hierarchy
- **Image formats** - AVIF, WebP, JPG support (format-specific quality)
- **Responsive images** - Multiple sizes
- **EXIF display** - Camera settings shown
- **Navigation** - Hierarchical structure

### What Changes

| Feature | Original | New |
|---------|----------|-----|
| **Config** | `config.sh` (Bash vars) | `revela.json` / `project.json` / `site.json` (typed) |
| **Templates** | Regex-based Mustache | Scriban (full Liquid) |
| **Images** | VIPS CLI (external) | NetVips (in-process) |
| **EXIF** | ExifTool CLI | NetVips native |
| **Markdown** | Perl script | Markdig (C#) |
| **Speed** | Good | **3-5× faster** |
| **Plugins** | None | NuGet-based |
| **GUI** | None | Planned |

### Theme Migration

#### Original Theme Structure
```bash
template/
├── header.mustache
├── footer.mustache
├── gallery.mustache
└── image.mustache
```

#### New Theme Structure
```
themes/my-theme/
├── theme.json           # Theme manifest (name, version, templates)
├── Layout.revela        # Main layout template
├── Assets/              # Static assets (auto-scanned)
│   ├── main.css
│   └── main.js
├── Body/                # Body templates
│   ├── Gallery.revela   # Image gallery (default)
│   └── Page.revela      # Text-only pages
└── Partials/            # Partial templates
    ├── Navigation.revela
    └── Image.revela
```

**Key conventions:**
- Templates use `.revela` extension (Scriban syntax)
- PascalCase folder/file names → lowercase keys (`Body/Gallery.revela` → `body/gallery`)
- Assets folder is auto-scanned (no manifest declaration needed)
- Local overrides: `project/themes/{ThemeName}/` (same structure)

#### Template Syntax Migration

**Original (Mustache-like):**
```html
{{TITLE}}
{{#IMAGES}}
  <img src="{{URL}}" alt="{{TITLE}}">
{{/IMAGES}}
```

**New (Scriban):**
```html
{{ site.title }}
{{ for image in images }}
  <img src="{{ variant_url(image, 640, 'jpg') }}" alt="{{ image.title }}">
{{ end }}
```

### Migration Strategy for Users

1. **Keep original project** - Don't delete Bash version yet
2. **Copy content/** - Same folder structure works
3. **Migrate config** - Convert `config.sh` → `project.json` / `site.json`
4. **Migrate theme** - Convert Mustache → Scriban templates
5. **Compare output** - Ensure HTML is equivalent
6. **Switch over** - Once satisfied, use .NET version

### Backward Compatibility

**Not a goal** - This is a rewrite, not a drop-in replacement.

**Migration path provided** - Tools and docs to help convert.

---

## Future Considerations

### Potential Enhancements

1. **Watch Mode**
   - File system watcher
   - Incremental rebuilds

2. **Dev Server**
   - Local HTTP server
   - Live reload

3. **Asset Pipeline**
   - CSS/JS minification
   - Image optimization

4. **Multi-Site Support**
   - Multiple configurations
   - Shared themes

5. **Cloud Integration**
   - Azure/AWS deployment
   - CDN invalidation

---

## ADR (Architecture Decision Records)

### ADR-001: Vertical Slice Architecture
**Status:** Accepted  
**Date:** 2025-01-19  
**Decision:** Use Vertical Slice over Layered Architecture  
**Rationale:** Better feature isolation, easier maintenance

### ADR-002: NetVips for Image Processing
**Status:** Accepted  
**Date:** 2025-01-19  
**Decision:** Use NetVips instead of ImageSharp  
**Rationale:** 3-5× faster, better memory usage, more formats

### ADR-003: NuGet-based Plugin System
**Status:** Accepted  
**Date:** 2025-12-17 (Updated from ZIP-based system)  
**Decision:** Plugins as NuGet packages (.nupkg) instead of ZIP files  
**Rationale:** 
- **Standard ecosystem** - NuGet.org, GitHub Packages support
- **Version management** - Semantic versioning, dependency resolution
- **Easy distribution** - `dotnet pack` + GitHub Actions workflows
- **Auto-restore** - `revela restore` installs missing plugins from project.json
- **Multi-source** - Support for private feeds, GitHub Packages
- **Metadata** - .nuspec provides authors, description, dependencies
- **2-stage release** - GitHub Release (auto) → NuGet.org (approval gate)

### ADR-004: System.CommandLine 2.0.0
**Status:** Accepted  
**Date:** 2025-01-19  
**Decision:** Use System.CommandLine (final release)  
**Rationale:** Official Microsoft CLI framework, modern API

### ADR-005: Global Image Formats with Per-Format Quality
**Status:** Accepted  
**Date:** 2025-12-10  
**Decision:** Store image formats as dictionary with format-specific quality settings  
**Rationale:** 
- Different formats have different quality characteristics (AVIF Q80 ≈ JPEG Q95)
- Allows optimization per format (AVIF: 80, WebP: 85, JPG: 90)
- Eliminates separate Quality property (single source of truth)
- Measured ~22% file size reduction vs uniform Q90

---

## Template Context Variables

Templates receive the following context variables from `RenderService`:

### Global Variables (all pages)

| Variable | Type | Description |
|----------|------|-------------|
| `site` | `SiteSettings` | Site metadata (title, author, description, copyright) |
| `basepath` | `string` | Relative path to site root (e.g., `""`, `"../"`, `"/photos/"`) |
| `assets_basepath` | `string` | Path/URL to image (asset) folder (can be absolute CDN URL) |
| `base_url` | `string?` | Absolute site host from `project.baseUrl` (null when unset); used by `absolute_url` |
| `image_formats` | `string[]` | Global list of image formats `["avif", "webp", "jpg"]` |
| `nav_items` | `NavigationItem[]` | Navigation tree with active state |

### Page-Specific Variables

| Variable | Type | Pages | Description |
|----------|------|-------|-------------|
| `gallery` | `Gallery` | all | Current gallery (title, body, description) |
| `galleries` | `Gallery[]` | index | All galleries for index listing |
| `images` | `Image[]` | all | Images for current page |

### Image Properties

Used in templates as `image.{property}`:

| Property | Type | Description |
|----------|------|-------------|
| `id` | `string` | HTML anchor ID (filename without extension) |
| `slug` | `string` | Path segment identifying the variants (e.g., `"photo1"`); build URLs with `variant_url` |
| `width` | `int` | Original width in pixels |
| `height` | `int` | Original height in pixels |
| `sizes` | `int[]` | Available widths for srcset (per-image, filtered by actual width) |
| `title` | `string?` | Optional image title |
| `exif` | `ExifData?` | EXIF metadata (f_number, exposure_time, iso, etc.) |

### Template Example

```scriban
{{~ # image_formats is GLOBAL (identical for all images) ~}}
{{~ # image.sizes is PER-IMAGE (varies based on original dimensions) ~}}
<picture>
  {{~ for format in image_formats ~}}
  <source
    type="image/{{ format }}"
    srcset="{{~ for size in image.sizes ~}}{{ variant_url(image, size, format) }} {{ size }}w{{~ if !for.last ~}}, {{~ end ~}}{{~ end ~}}">
  {{~ end ~}}
  <img src="{{ variant_url(image, image.sizes[0], 'jpg') }}">
</picture>
```

### Template Authoring Cheat Sheet

**Rule of thumb (#74):** *Properties = identity* (context-free, serialisable — e.g. `image.slug`,
`gallery.slug`). *Helpers = rendering/linking* — they own all `basepath` / `base_url` /
asset-prefix knowledge. **Never concatenate paths in templates; always go through a helper.**

| Helper | Purpose | Example |
|--------|---------|---------|
| `page_url(target)` | Site-relative page URL. Polymorphic: `Image`, `Gallery`, `NavigationItem`, or slug string. Returns `null` for a pageless nav item (branch on it). | `{{ page_url(gallery) }}` → `/events/fireworks/` |
| `absolute_url(target)` | Absolute URL (host from `baseUrl`) for OG/RSS/sitemap/JSON-LD. Falls back to a root-relative path when `baseUrl` is unset. | `{{ absolute_url(gallery) }}` |
| `variant_url(image, size, format)` | Asset URL for one image variant (size + format). | `{{ variant_url(image, 640, 'jpg') }}` |
| `asset_url(path)` | URL for a static asset (CSS/JS). | `{{ asset_url('css/site.css') }}` |
| `find_image(path)` | Resolve an `Image` by source path (gallery-local → `_images/` → exact). | `{{ find_image('cover.jpg') }}` |
| `format_date` / `format_filesize` / `format_exif_*` | Value formatting. | `{{ format_date(image.date_taken, 'yyyy-MM-dd') }}` |
| `markdown(text)` | Render Markdown to HTML. | `{{ markdown(gallery.body) }}` |

`basepath` remains available for raw links such as `{{ basepath }}_assets/…` and `{{ basepath }}_static/…`.

---

**For questions or clarifications, refer to DEVELOPMENT.md**

