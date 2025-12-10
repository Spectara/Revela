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
│   ├── Spectara.Revela.Infrastructure/    # External Services
│   │   ├── ImageProcessing/      # NetVips wrapper
│   │   └── Templating/           # Scriban + Markdig
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
│       ├── Plugin.Deploy.SSH/
│       └── Plugin.Source.OneDrive/
│
└── tests/
```

---

## Technology Stack

### Core Framework
- **.NET 10** - Target framework
- **C# 14** - Language version
- **System.CommandLine 2.0.0** - CLI framework

### Image Processing
- **NetVips 3.1.0** - High-performance image processing (libvips wrapper)
- **NetVips.Native 8.17.3** - Native libvips binaries

### Templating
- **Scriban 5.10.0** - Liquid-like template engine
- **Markdig 0.37.0** - Markdown parser

### Configuration
- **Microsoft.Extensions.Configuration** - Configuration system
- **Microsoft.Extensions.Options** - Options pattern

### Logging
- **Microsoft.Extensions.Logging 10.0.0** - Built-in logging abstraction
- **Console & Debug providers** - Standard output

### Plugin Management
- **NuGet.Protocol 6.12.1** - NuGet package installation
- **NuGet.Packaging 6.12.1** - Package extraction

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
User runs: revela plugin install Spectara.Revela.Plugin.Deploy

1. Query NuGet.org
   Search for package

2. Resolve Version
   Get latest (or specified)

3. Download Package
   .nupkg file

4. Extract to Plugin Dir
   %APPDATA%/Revela/plugins/Spectara.Revela.Plugin.Deploy/

5. Verify Plugin
   Check IPlugin implementation

6. Success
   Plugin available on next run
```

---

## Extension Points

### 1. Custom Plugins

Implement `IPlugin` interface:

```csharp
public class MyPlugin : IPlugin
{
    public IPluginMetadata Metadata { get; }
    
    public void Initialize(IServiceProvider services)
    {
        // Register services
    }
    
    public IEnumerable<Command> GetCommands()
    {
        yield return new Command("mycmd") { /* ... */ };
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

- **Parallel processing** - Uses `Parallel.ForEachAsync` with `Environment.ProcessorCount`
- **Thread-safe** - LibVips is thread-safe for independent images
- **No global lock** - Each image processed independently
- **Streaming** - NetVips streams large images (low memory)
- **Caching** - Hash-based manifest skips unchanged images
- **Performance:** ~5× speedup vs sequential (tested: 94s → 18s for 23 images)

### 2. Template Rendering

- **Compiled templates** - Scriban compiles once
- **Partial caching** - Reuse common partials

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
- **Image formats** - WebP, JPG support
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
themes/default/
├── layouts/
│   ├── _default.html
│   ├── gallery.html
│   └── image.html
├── partials/
│   ├── head.html
│   ├── navigation.html
│   └── footer.html
└── static/
    ├── css/
    └── js/
```

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
**Date:** 2025-01-19  
**Decision:** Plugins as NuGet packages  
**Rationale:** Standard distribution, version management, easy updates

### ADR-004: System.CommandLine 2.0.0
**Status:** Accepted  
**Date:** 2025-01-19  
**Decision:** Use System.CommandLine (final release)  
**Rationale:** Official Microsoft CLI framework, modern API

### ADR-005: Global Image Formats (Not Per-Image)
**Status:** Accepted  
**Date:** 2025-12-10  
**Decision:** Store image formats globally, not in per-image manifest entries  
**Rationale:** Formats are identical for all images (webp, jpg). Per-image storage was redundant. Sizes remain per-image because small images may skip larger sizes.

---

## Template Context Variables

Templates receive the following context variables from `RenderService`:

### Global Variables (all pages)

| Variable | Type | Description |
|----------|------|-------------|
| `site` | `SiteSettings` | Site metadata (title, author, description, copyright) |
| `basepath` | `string` | Relative path to site root (e.g., `""`, `"../"`, `"/photos/"`) |
| `image_basepath` | `string` | Path/URL to image folder (can be absolute CDN URL) |
| `image_formats` | `string[]` | Global list of image formats `["webp", "jpg"]` |
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

