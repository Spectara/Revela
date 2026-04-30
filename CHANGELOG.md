# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

- **Path traversal hardening in dev server** — `revela serve` now uses `Path.GetRelativePath` to validate request paths, rejecting sibling directories that share a name prefix with the configured root (previously a naive `StartsWith` check could be bypassed). 7 new unit tests cover the vector. ([#53](https://github.com/Spectara/Revela/issues/53))
- **SSRF guardrails for source plugins** — New `Spectara.Revela.Sdk.Validation.UrlSafety` helper validates outbound URLs before HTTP requests. Rejects loopback, RFC 1918 private, RFC 6598 CGN, link-local (incl. cloud metadata IP `169.254.169.254`), IPv6 link-local/site-local/ULA, multicast, and IPv4-mapped loopback. Now used by Source.Calendar (iCal feeds) and Source.OneDrive (share URLs). 40 new unit tests. Plugin authors building source plugins should adopt this — see [`docs/httpclient-pattern.md`](docs/httpclient-pattern.md#url-validation-ssrf-prevention). ([#58](https://github.com/Spectara/Revela/issues/58))
- **Sensitive URLs no longer logged at Information level** — OneDrive share URLs (which contain account-scoped resource IDs) and iCal feed URLs (which often carry auth tokens in query strings) are now logged at Debug. iCal logs the host at Information for diagnostics. ([#57](https://github.com/Spectara/Revela/issues/57))
- **Markup escaping for user input** — All `Spectre.Console` `MarkupLine` calls now wrap user-controlled strings (package IDs, theme names, project paths, file paths, exception messages) in `Markup.Escape` to prevent display corruption. ([#61](https://github.com/Spectara/Revela/issues/61))

### Documentation

- **New `docs/security-model.md`** — comprehensive threat model: trust assumptions, what Revela protects against, what it explicitly does NOT (raw HTML in markdown, EXIF GPS, third-party theme review), plugin trust model, and upgrade paths.
- **`MarkdownService` trust model documented** — Inline XML doc explains why `.DisableHtml()` is deliberately not called (Markdig itself states it's not a sanitizer; same trust model as Jekyll, Eleventy, MkDocs, Astro, Zola). ([#60](https://github.com/Spectara/Revela/issues/60))
- **Plugin trust model documented** — Same model as `dotnet tool install`: trust at install time, no hash-pinning between install and load. Explains why author-signing isn't pursued (NuGet certificate lock-in via NU3038/NU3018). Documents existing free verification paths (`dotnet nuget verify` for nuget.org, `gh attestation verify` for GitHub releases). ([#55](https://github.com/Spectara/Revela/issues/55))
- **`docs/plugin-development.md`, `docs/httpclient-pattern.md`, `.github/copilot-instructions.md`** updated with `TryAddTransient` (was `AddTransient`) and `UrlSafety` guidance for plugin authors.

### Changed

- **Genuinely-async file IO** — `NavigationBuilder.BuildAsync` is now actually async (was wrapping sync code in `Task.FromResult`); `RenderService.LoadConfigurationAsync`/`LoadSiteJsonAsync`, `ThemeService.UpdateThemeNameAsync`, and `ThemeExtractCommand.UpdateThemeNameAsync`/`PromptForThemeSelectionAsync` use async file IO with `CancellationToken` plumbing. ([#56](https://github.com/Spectara/Revela/issues/56), [#59](https://github.com/Spectara/Revela/issues/59))
- **Plugin command registration uses `TryAddTransient`** — Serve, Source.OneDrive, and Source.Calendar plugins now register commands idempotently. ([#54](https://github.com/Spectara/Revela/issues/54))
- **OneDrive URL validation tightened** — Host equality check (`uri.Host == "1drv.ms"`) replaces substring match (`url.Contains("1drv.ms")`), which previously accepted `https://attacker.com/?fake=1drv.ms`.

### Fixed

- **Plugin test projects build again** — `InternalsVisibleTo` mismatches in Compress and Serve `AssemblyInfo.cs` files were silently breaking ~163 tests; stale `Microsoft.Extensions.Telemetry.Abstractions` reference (NU1010) in Compress and Statistics test csprojs; 16 `StatisticsAggregator` constructor calls updated for new `TimeProvider` parameter; obsolete `CleanCompressCommand.Order` test removed.

### Removed

- **Dead `TestDataHelper` infrastructure** — referenced a non-existent `test-data/` directory and was unused. Real test infrastructure lives in `tests/Shared/Fixtures/`.

### Build

- Bumped MSTest from 4.2.1 to 4.2.2 (patch).

## [0.0.1-beta.17] - 2026-03-15

### Added
- **Cover Image**: New `cover` frontmatter field for gallery/page cover images
  - Path resolution identical to Markdown content images (gallery-local → `_images/` → exact match)
  - Available in templates as `{{ gallery.cover_image }}` (full Image object with url, sizes, width, height)
  - Example: `cover = "panorama/panorama.jpg"`
- **`find_image` Template Function**: Resolve any image from the project in templates
  - Same 3-step lookup as Markdown content images
  - Returns full Image object: `{{ find_image "logo.jpg" }}` → url, sizes, width, height
  - Returns `null` if image not found
- **Content Image Template**: Markdown body images (`![alt](path)`) now rendered via
  Scriban template `Partials/ContentImage.revela` instead of hardcoded C#
  - Every theme must include this template (no fallback)
  - Themes can customize for lightbox, styling, etc.
  - Template variables: `image`, `alt`, `classes`, `image_basepath`, `image_formats`
- **Sitemap Generation**: Automatic `sitemap.xml` during `generate pages`
  - Requires `baseUrl` in project.json (`"project": { "baseUrl": "https://..." }`)
  - Includes all pages with `<lastmod>` (gallery date or build date)
  - Skipped with info log when `baseUrl` is not configured

### Changed
- **Custom Templates**: Now receive `{{ images }}` array (previously empty for custom templates)
- **Content Image Rendering**: Removed ~80 lines of C# HTML generation code, replaced by
  theme-owned Scriban template

### Fixed
- **Body Duplication**: Fixed duplicate body text on pages with custom templates
  (e.g., Calendar plugin). Removed redundant pre-rendering step that caused
  `{{ gallery.body }}` and `{{ page_content }}` to both contain the same content
- **Website**: Showcase screenshot too small due to overly broad CSS selector
  targeting all images in hero section

## [0.0.1-beta.16] - 2026-03-13

### Added
- **CLI: `--project/-p` path support**: Use `-p path/to/project` to specify a project directory
  without `cd`. Works in both Tool Mode (paths) and Standalone Mode (names or paths)
- **Statistics: Photo Activity Heatmap**: Calendar-style visualization showing when photos
  were taken, with color-coded intensity per day
- **SDK: `AddPluginConfig<T>()`**: Simplified one-line plugin configuration registration
  with validation and hot-reload support
- **Navigation: Container nodes**: New `container = true` frontmatter property for
  navigation-only nodes that group child pages without their own content
- **Website**: FAQ page, Docs overview page, Showcase page, glassmorphism redesign with
  neon logo, demo video on homepage

### Changed
- **Statistics Plugin**: Complete overhaul with pure-CSS dashboard (no JavaScript),
  restructured charts and layout
- **Plugin System**: Simplified `IPlugin` interface using default interface methods —
  `ConfigureConfiguration` and `GetCommands` are now optional with sensible defaults
- **Plugin Architecture**: Extracted `PluginManager` into focused service classes,
  extracted `IGlobalConfigManager` interface
- **Theme System**: Simplified with shared abstractions, modernized Lumina CSS with
  nesting and custom properties
- **Namespaces**: Unified conventions via `Directory.Build.props` — automatic
  `Spectara.Revela.*` prefix for all projects
- **Project Layout**: Renamed plugin/theme folders and namespaces for consistency
- **Code Quality**: Comprehensive code reviews across CLI, Serve, OneDrive, Lumina,
  and Statistics — restricted type visibility, extracted helpers, reduced duplication
- **Single-file bundle**: Enabled Brotli compression for smaller executable
- **Code Coverage**: Migrated from coverlet.collector to Microsoft Code Coverage

### Fixed
- **Linux Compatibility**: Forward slashes in NuGet `PackagePath`, case-sensitive
  `Build/` → `build/` rename, lowercase slugs in test assertions
- **Statistics**: Bar charts not rendering data, scoped CSS selectors to main content
- **Theme**: Nav scrollbar hidden behind sticky header
- **Build**: Plugins/themes now built in Release mode for pack step
- **Website**: Duplicate FAQ/Docs navigation entries, broken links, container labels
  clickable in sidebar

### Dependencies
- Microsoft.Extensions.* 10.0.3 → 10.0.5
- Microsoft.Extensions.Http.Resilience 10.3.0 → 10.4.0
- System.CommandLine 2.0.3 → 2.0.5
- Scriban 6.5.2 → 6.5.7
- Markdig 0.45.0 → 1.1.1

### Testing
- Full E2E pipeline integration tests (scan → render → images)
- Nested galleries and incremental build tests
- NetVips-based `TestImageGenerator` for real JPEG creation in tests
- `ConfigService` and `ManifestService` integration tests
- Improved SDK test coverage toward 100%
- Restructured test projects with shared infrastructure

## [0.0.1-beta.15] - 2026-02-12

### Added
- **Content Images**: Markdig extension for responsive images in Markdown body content
  - Transform `![alt](path)` into `<picture>` elements with AVIF/WebP/JPG srcset
  - 3-step image resolution: gallery-local → `_images/` shared → exact match
  - LQIP placeholder support via `--lqip` CSS custom property
  - GenericAttributes support: `{.class}` syntax passes CSS classes to `<picture>`
- **Shared Images**: `_images/` folder included as hidden node in manifest tree
  - Images available for content references across all galleries
  - Subdirectories supported (e.g., `_images/screenshots/`)
- **Browser Mockup CSS**: Simulated browser chrome for screenshots on website
  - Titlebar with traffic light dots (red/yellow/green)
  - Uses CSS variables for dark/light mode compatibility
- **Showcase Section**: "See it in action" section on revela.website homepage

### Changed
- **Website CSS**: Convert website.css to native CSS nesting
  - Nested `@media`, `&::before`, `&:hover`, `&.active` selectors
  - Reduced repetition, improved readability
- **Lumina Theme**: Add `.content-image` and `.breakout` CSS classes

### Fixed
- **Documentation**: Correct false "any name" claim for `_images/` folder
- **Website**: Remove unnecessary local main.css theme override

## [0.0.1-beta.14] - 2026-02-12

### Added
- **LQIP Placeholders**: CSS-only low-quality image placeholders
  - 20-bit integer encoding (~7 bytes per image)
  - CSS-only decoding using `calc()`, `mod()`, `pow()` - no JavaScript needed
  - 3×2 brightness grid with grayscale cells
  - Dual-layer blend modes (hard-light + overlay) for smooth appearance
  - Average color calculation in Oklab color space
  - Configurable via `generate.images.placeholder` in project.json
- **BenchmarkDotNet Benchmarks**: Image processing performance benchmarks
  - ResizeStrategyBenchmark: StarFromOriginal vs ThumbnailPerSize vs ThumbnailThenResize
  - FormatSequentialBenchmark: all-formats-per-image vs format-sequential processing
  - New `benchmarks/` folder with dedicated configuration
- **Documentation**: Complete plugin documentation section on website
  - Plugin overview with install/manage/uninstall commands
  - Statistics plugin: EXIF analysis, configuration, CLI reference
  - Serve plugin: port configuration, verbose mode, workflow
  - Source.OneDrive plugin: setup, sync commands, environment variables
- **Documentation**: Comprehensive User Journey guide (17 phases)
- **Tests**: ScanCachingTests for cache hit/miss conditions
- **Tests**: ManifestServiceTests for config hash computation

### Changed
- **Image Processing**: Switch from `Resize()` to `ThumbnailImage()` for correct alpha channel handling
  - Based on libvips maintainer recommendation (libvips/libvips#4588)
  - Properly handles alpha premultiplication for transparent PNGs
- **Image Processing**: "Star from Original" strategy - load once, resize all sizes
  - 13% faster than previous shrink-on-load per size approach
  - Remove unnecessary `CopyMemory()` call
- **AVIF Threading**: Optimal (CPU/2) × (CPU/2) threading strategy
  - Workers = ProcessorCount/2, NetVips.Concurrency = ProcessorCount/2
  - ~50% fewer threads with similar performance, prevents system freeze
- **Change Detection**: Replace hash-based detection with LastModified + FileSize
  - Much faster: no SHA256 computation for unchanged files
  - Scan metadata caching: skip NetVips reads for unchanged source files
  - ScanConfigHash invalidates cache when placeholder/minDimensions change
  - FormatQualities tracking: automatic regeneration when quality changes
- **Progress Display**: Dynamic legend with format-specific colors
  - JPG=green, WebP=blue, AVIF=magenta, PNG=cyan
  - Only shows configured formats
- **Image Sizes**: Updated defaults optimized for High-DPI displays
  - `[160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560]`
- **Manifest Schema**: Simplified change tracking
  - ImageContent: Replace Hash+ProcessedAt with LastModified
  - ManifestMeta: Add ScanConfigHash and FormatQualities
  - GalleryContent: Remove Hash (moved to MarkdownContent only)
- **Dependencies**: Major package updates
  - .NET SDK 10.0.102 → 10.0.103
  - Microsoft.Extensions.* 10.0.2 → 10.0.3
  - Microsoft.Extensions.Http.Resilience 10.2.0 → 10.3.0
  - System.CommandLine 2.0.2 → 2.0.3
  - Markdig 0.44.0 → 0.45.0
  - NuGet.* 7.0.1 → 7.3.0
  - MSTest 4.0.2 → 4.1.0
  - Remove unused Hosting.Abstractions, Configuration.CommandLine packages

### Fixed
- **CA1873**: Add `IsEnabled()` guards for 15 logging performance warnings
- **CA1508**: Fix false positives in ScanCachingTests

## [0.0.1-beta.13] - 2026-01-12

### Added
- **Clean Images Command**: Intelligently remove unused image files
  - Detects orphaned folders (deleted source images)
  - Detects unused sizes (removed from theme config)
  - Detects unused formats (disabled in project config)
  - `--dry-run` option to preview without deleting
  - Safety check: requires valid manifest to prevent accidental deletion
- **Incremental Image Generation**: Only generate missing variants
  - Adding a new format only generates that format (existing files kept)
  - Adding a new size only generates that size
  - Progress display shows green ■ (new) vs gray ■ (skipped)
  - Processing order: largest sizes first for smoother progress display
- **Compress**: Static compression plugin (Gzip/Brotli)
- **Setup Wizard**: Full/Custom installation modes
- **Documentation**: New "Image Processing" page on website
- **Documentation**: Number prefix sorting for galleries (e.g., `01 Weddings`, `02 Portraits`)

### Fixed
- **NavigationBuilder**: URL slugs now correctly strip number prefixes (was including `01-` in URLs)
- **Documentation**: Fixed incorrect JSON format examples (`formats` wrapper removed)
- **Documentation**: Plugin naming consistency (`Source.OneDrive` instead of `OneDrive`)
- **CI/CD**: Added missing Compress to all workflows and scripts

## [0.0.1-beta.12] - 2026-01-12

### Added
- **Static Files**: New `_static/` folder support for custom assets (CNAME, .nojekyll, favicon)
- **Favicon Partial**: Configurable favicon via `site.favicon` and theme partial
- **HeaderNavigation Partial**: Extracted from Layout for easier customization
- **NavigationItem.Current**: Property for active page detection in navigation
- **robots.txt**: Added to revela-website sample
- **CI/CD**: Automatic website deployment after release workflow

### Changed
- **Website Styling**: Complete color palette overhaul
  - Purple accent (263°) for links, buttons, active states
  - Pink accent-light (309°) for hover states
  - Separate light/dark mode gradients (logo-inspired)
  - Dark code blocks with Prism.js syntax highlighting
  - Consistent navigation hover colors across all sections
- **Documentation**: Updated CLI reference and plugin command names

### Fixed
- **Website**: Table styling in docs-content, duplicate H1 removal
- **Docs Navigation**: Proper active state and current page detection

## [0.0.1-beta.10] - 2026-01-07

### Fixed
- **Serve Plugin**: Prevent ObjectDisposedException when port is in use
  - Stop() now checks isRunning flag before calling HttpListener.Stop()
  - Dispose() catches ObjectDisposedException during Close()
- **Template Rendering**: Fix double `<section class="page-content">` wrapper on text pages
  - Pre-rendering now only applies to custom templates with data sources
  - Standard body templates (page, gallery) no longer get double-wrapped
- **Create Page**: Allow empty path input for source root pages
  - Pressing Enter without input creates page directly in source/

### Changed
- **Package Index**: packages.json now stored directly in ConfigDirectory
  - No separate cache/ folder in standalone root anymore
  - More consistent structure alongside revela.json
- **Path Constants**: Replace all hardcoded paths with ProjectPaths.* constants
  - source, output, .cache, themes centralized in Sdk/ProjectPaths.cs
  - Better maintainability and consistency
- **Config Menu**: Move sorting command to Project group
  - Now appears alongside project, theme, image, site commands

## [0.0.1-beta.8] - 2026-01-04

### Added
- **Create Page Command**: Extended page templates with interactive mode
  - `create page gallery`: New options --sort, --hidden, --slug
  - `create page text`: New template for text-only pages (About, Contact)
  - Interactive wizard when path argument is missing
  - DefaultBody property for starter content
- **Theme Customization**: Local theme variables via theme/theme.json
- **Documentation**: Theme customization guide (DE/EN), pages documentation (DE/EN)

### Changed
- **Standalone Mode**: Setup wizard now appears BEFORE project selection
- **Getting-Started Guides**: Focus on interactive menu mode
- **UX**: Base URL prompt with helpful description
- **Template System**: Unified theme/extension structure with implicit template prefixes
- **Generate Pipeline**: Unified IGenerateStep interface
- **Config System**: Fixed IConfiguration array-merge problem

### Fixed
- Statistics rendering with implicit template prefix system
- Path handling: Pages are created relative to source/
- Option constructor for properties without short alias

## [0.0.1-beta.7] - 2025-12-29

### Changed
- Documentation: Comprehensive revision and asset restructuring
- CI/CD: Fix duplicate SHA256SUMS uploads in release workflow

## [0.0.1-beta.6] - 2025-12-29

### Added
- **Setup Wizard**: Interactive assistant for new projects
- **Packages Command**: Package index with NuGet packageTypes support
- **Serve**: Local HTTP server for site preview
- **Spectara.Revela.Sdk**: Separate SDK package for plugin development
- Unified command registration with CommandDescriptor
- Context-aware interactive menu
- DevContainer debug configurations (Local/Container)

### Changed
- **CLI Restructuring**: New commands `create`, `init`, `config`
- **Config System**: Unified global config with `revela.json`
- **Theme-based Configuration**: ThemeConfig system
- **IOptions Pattern**: Hierarchical config merging
- Simplified plugin directory structure (removed CWD and global options)
- Consolidated plugin configuration in project.json
- Interactive mode UX improvements
- Revised release pipeline and standardized NuGet metadata

### Fixed
- Cross-platform path handling in NuGetSourceManagerTests
- Added missing Packages Command files to Git

## [0.0.1-beta.5] - 2025-12-21

### Added
- **NuGet Plugin System**: Full NuGet package support (instead of ZIP)
- **plugin.meta.json**: Automatic creation with package metadata
- **Restore Command**: NuGet-based plugin restore with parallelization
- **NuGet Source Management**: add/remove/list commands for NuGet sources
- **Multi-Source Discovery**: --source parameter for plugin installation
- **GitHub Workflows**: 3-stage NuGet release process
- **.NET Global Tool**: CLI as installable dotnet tool
- Dedicated README for each plugin and theme
- Provenance attestations and keyless signatures

### Changed
- Switched plugin installation from ZIP to NuGet
- Updated CI workflow for MSTest v4 Testing Platform
- Added HttpClient parameter to PluginManager (Typed HttpClient Pattern)
- Single-file publish compatibility for plugin system
- Comprehensive documentation updates

### Fixed
- Embedded resource loading for theme assets
- Skip plugin loading for plugin management commands
- Added --yes flag for plugin uninstall

## [0.0.1-beta.4] - 2025-12-15

### Added
- **Template/Asset Resolver System**: Theme files command
- **FileHashService**: Service for file hashing
- CancellationToken propagation through all CLI commands
- Interactive mode with menu-driven interface

### Changed
- Improved image caching (moved hash to processing phase)
- Optimized logging defaults

### Fixed
- Emojis durch ASCII ersetzt für Terminal-Kompatibilität
- Plugin Name Prefix korrigiert und Uninstall Cleanup verbessert
- Using Directives aufgeräumt und Data Source Loading verbessert
- Cancellation + Exit Codes für Generate Pipeline

## [0.0.1-beta.3] - 2025-12-14

### Added
- Test Infrastructure: SharedTestDataHelper und MockHttpMessageHandler
- Statistics.Tests: Unit Tests für StatisticsAggregator
- TestData Factory für konsistente Test-Daten
- IntegrationTests Placeholder-Struktur

### Changed
- Tests nutzen jetzt shared Shared-Projekt für gemeinsame Test-Utilities
- Alle Test-Projekte referenzieren nun das Shared-Projekt

### Fixed
- Test-Projekt Konfiguration für MSTest v4 vereinheitlicht

## [0.0.1-beta.2] - 2025-12-13

### Added
- Deutsche Anleitung für Fotografen ([docs/getting-started-de.md](docs/getting-started-de.md))
- Theme Extension Support mit CSS-Loading für Plugins
- Statistics-Plugin Styling für Lumina
- Data Sources und Custom Templates für Frontmatter
- Statistics für erweiterte Galerie-Statistiken
- Lumina.Statistics als Theme-Erweiterung

### Changed
- Theme.Expose umbenannt zu Lumina
- Migration von .sln zu .slnx Format (Visual Studio 2022 17.10+)
- Code Quality Verbesserungen und Cleanup

### Fixed
- Statistics: Verbessertes Sorting und Template-Anzeige

## [0.0.1-beta.1] - 2025-12-10

### Added
- Initiales Release
- CLI mit System.CommandLine 2.0
- Image Processing mit NetVips (AVIF, WebP, JPG)
- Scriban Template Engine
- Plugin System (NuGet-basiert)
- Lumina (Standard-Theme)
- Source.OneDrive (OneDrive Shared Folder Support)
- Commands: generate, init, clean, theme, plugins, restore

[Unreleased]: https://github.com/spectara/revela/compare/v0.0.1-beta.17...HEAD
[0.0.1-beta.17]: https://github.com/spectara/revela/compare/v0.0.1-beta.16...v0.0.1-beta.17
[0.0.1-beta.16]: https://github.com/spectara/revela/compare/v0.0.1-beta.15...v0.0.1-beta.16
[0.0.1-beta.15]: https://github.com/spectara/revela/compare/v0.0.1-beta.14...v0.0.1-beta.15
[0.0.1-beta.14]: https://github.com/spectara/revela/compare/v0.0.1-beta.13...v0.0.1-beta.14
[0.0.1-beta.13]: https://github.com/spectara/revela/compare/v0.0.1-beta.12...v0.0.1-beta.13
[0.0.1-beta.12]: https://github.com/spectara/revela/compare/v0.0.1-beta.10...v0.0.1-beta.12
[0.0.1-beta.10]: https://github.com/spectara/revela/compare/v0.0.1-beta.8...v0.0.1-beta.10
[0.0.1-beta.8]: https://github.com/spectara/revela/compare/v0.0.1-beta.7...v0.0.1-beta.8
[0.0.1-beta.7]: https://github.com/spectara/revela/compare/v0.0.1-beta.6...v0.0.1-beta.7
[0.0.1-beta.6]: https://github.com/spectara/revela/compare/v0.0.1-beta.5...v0.0.1-beta.6
[0.0.1-beta.5]: https://github.com/spectara/revela/compare/v0.0.1-beta.4...v0.0.1-beta.5
[0.0.1-beta.4]: https://github.com/spectara/revela/compare/v0.0.1-beta.3...v0.0.1-beta.4
[0.0.1-beta.3]: https://github.com/spectara/revela/compare/v0.0.1-beta.2...v0.0.1-beta.3
[0.0.1-beta.2]: https://github.com/spectara/revela/compare/v0.0.1-beta.1...v0.0.1-beta.2
[0.0.1-beta.1]: https://github.com/spectara/revela/releases/tag/v0.0.1-beta.1
