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
│   ├── Spectara.Revela.Core/              # Shared Kernel
│   │   ├── Models/               # Domain models
│   │   ├── Configuration/        # Config models
│   │   ├── Abstractions/         # Interfaces
│   │   ├── PluginLoader.cs       # Plugin discovery
│   │   └── PluginManager.cs      # Plugin management
│   │
│   ├── Spectara.Revela.Commands/          # CLI Commands (Vertical Slices)
│   │   ├── Generate/             # Site generation command
│   │   ├── Init/                 # Project initialization
│   │   ├── Plugins/              # Plugin management
│   │   ├── Restore/              # Dependency restore
│   │   └── Theme/                # Theme management
│   │
│   ├── Spectara.Revela.Cli/               # CLI Entry Point
│   │   └── Program.cs            # Host + Commands
│   │
│   └── Spectara.Revela.Plugins/           # Optional Plugins
│       ├── Plugin.Serve/
│       ├── Plugin.Source.OneDrive/
│       └── Plugin.Statistics/
│
└── tests/
```

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
- Plugins stored in `%APPDATA%/Revela/plugins/`
- Discovered via reflection
- Loaded at startup
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

**Pattern:** Options Pattern + JSON

```json
Spectara.Revela.json → ExposeConfig → IOptions<ExposeConfig>
```

**Benefits:**
- Strongly-typed
- Validated
- Testable
- IDE support

**Override hierarchy:**
1. `Spectara.Revela.json` (base)
2. `Spectara.Revela.{Environment}.json` (override)
3. Environment variables
4. Command-line arguments (highest priority)

### 6. Plugin Discovery

```
%APPDATA%/Revela/plugins/
└── Spectara.Revela.Plugin.Deploy/
    ├── Spectara.Revela.Plugin.Deploy.dll
    └── dependencies...
```

**Discovery process:**
1. Scan plugin directory for `Spectara.Revela.Plugin.*.dll`
2. Load assemblies
3. Find types implementing `IPlugin`
4. Instantiate and register commands

---

## Data Flow

### Site Generation Flow

```
User runs: revela generate -p mysite

1. Load Configuration
   project.json + site.json → RevelaConfig

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
   Source.OneDrive → Spectara.Revela.Plugin.Source.OneDrive

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
   "Spectara.Revela.Plugin.Source.OneDrive": "1.0.0"

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
    private IServiceProvider? services;
    
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "My Plugin",
        Version = "1.0.0",
        Description = "Plugin description",
        Author = "Your Name"
    };
    
    // 1. ConfigureConfiguration - usually empty (framework auto-loads plugins/*.json)
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Nothing to do - framework handles JSON + ENV loading
    }
    
    // 2. ConfigureServices - register services BEFORE ServiceProvider is built
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<MyHttpService>();
        services.AddOptions<MyConfig>()
            .BindConfiguration(MyConfig.SectionName)
            .ValidateDataAnnotations();
    }
    
    // 3. Initialize - called AFTER ServiceProvider is built
    public void Initialize(IServiceProvider services)
    {
        this.services = services;
    }
    
    // 4. GetCommands - returns CommandDescriptor (command + optional parent)
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        // ParentCommand: null = root level, "init" = under init, etc.
        yield return new CommandDescriptor(
            new Command("mycmd", "My command"),
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

- Sanitized before rendering
- No script injection possible
- GPS coordinates optional

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
expose
├── Spectara.Revela.json            # Structured JSON config
├── themes/
│   └── *.html             # Scriban templates (full-featured)
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
| **Config** | `config.sh` (Bash vars) | `Spectara.Revela.json` (typed) |
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
├── theme.json           # Theme manifest (name, version, variables)
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
  <img src="{{ image.url }}" alt="{{ image.title }}">
{{ end }}
```

### Migration Strategy for Users

1. **Keep original project** - Don't delete Bash version yet
2. **Copy content/** - Same folder structure works
3. **Migrate config** - Convert `config.sh` → `Spectara.Revela.json`
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
- **3-stage release** - GitHub Release → GitHub Packages (auto) → NuGet.org (approval)

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
| `image_basepath` | `string` | Path/URL to image folder (can be absolute CDN URL) |
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
| `url` | `string` | Path segment for variants (e.g., `"photo1"`) |
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
    srcset="{{~ for size in image.sizes ~}}{{ image_basepath }}{{ image.url }}/{{ size }}.{{ format }} {{ size }}w{{~ if !for.last ~}}, {{~ end ~}}{{~ end ~}}">
  {{~ end ~}}
  <img src="{{ image_basepath }}{{ image.url }}/{{ image.sizes[0] }}.jpg">
</picture>
```

---

**For questions or clarifications, refer to DEVELOPMENT.md**

