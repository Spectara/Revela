# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- **Plugin.Serve**: Local HTTP server for site preview
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
- Plugin.Statistics.Tests: Unit Tests für StatisticsAggregator
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
- Statistics-Plugin Styling für Theme.Lumina
- Data Sources und Custom Templates für Frontmatter
- Plugin.Statistics für erweiterte Galerie-Statistiken
- Theme.Lumina.Statistics als Theme-Erweiterung

### Changed
- Theme.Expose umbenannt zu Theme.Lumina
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
- Theme.Lumina (Standard-Theme)
- Plugin.Source.OneDrive (OneDrive Shared Folder Support)
- Commands: generate, init, clean, theme, plugins, restore

[Unreleased]: https://github.com/spectara/revela/compare/v0.0.1-beta.12...HEAD
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
