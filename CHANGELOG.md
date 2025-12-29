# Changelog

Alle wichtigen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
und dieses Projekt folgt [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

## [0.0.1-beta.7] - 2025-12-29

### Changed
- Dokumentation: Umfassende Überarbeitung und Asset-Neustrukturierung
- CI/CD: Fix für doppelte SHA256SUMS-Uploads im Release-Workflow

## [0.0.1-beta.6] - 2025-12-29

### Added
- **Setup Wizard**: Interaktiver Assistent für neue Projekte
- **Packages Command**: Package Index mit NuGet packageTypes Support
- **Plugin.Serve**: Lokaler HTTP-Server für Site-Preview
- **Spectara.Revela.Sdk**: Separates SDK-Paket für Plugin-Entwicklung
- Unified Command Registration mit CommandDescriptor
- Kontextabhängiges interaktives Menü
- DevContainer Debug-Konfigurationen (Local/Container)

### Changed
- **CLI Restructuring**: Neue Befehle `create`, `init`, `config`
- **Config System**: Unified global config mit `revela.json`
- **Theme-based Configuration**: ThemeConfig System
- **IOptions Pattern**: Hierarchisches Config-Merging
- Plugin-Verzeichnisstruktur vereinfacht (CWD und global options entfernt)
- Plugin-Konfiguration in project.json konsolidiert
- Interactive Mode UX-Verbesserungen
- Release Pipeline überarbeitet und NuGet Metadata standardisiert

### Fixed
- Cross-platform Pfad-Handling in NuGetSourceManagerTests
- Fehlende Packages Command Dateien zu Git hinzugefügt

## [0.0.1-beta.5] - 2025-12-21

### Added
- **NuGet Plugin System**: Vollständiges NuGet-Paket-Support (statt ZIP)
- **plugin.meta.json**: Automatische Erstellung mit Package-Metadaten
- **Restore Command**: NuGet-basiertes Plugin-Restore mit Parallelisierung
- **NuGet Source Management**: add/remove/list Befehle für NuGet-Quellen
- **Multi-Source Discovery**: --source Parameter für Plugin-Installation
- **GitHub Workflows**: 3-stufiger NuGet Release-Prozess
- **.NET Global Tool**: CLI als installierbares dotnet Tool
- Dedizierte README für jedes Plugin und Theme
- Provenance Attestations und Keyless Signatures

### Changed
- Plugin-Installation von ZIP auf NuGet umgestellt
- CI Workflow für MSTest v4 Testing Platform aktualisiert
- HttpClient Parameter zu PluginManager hinzugefügt (Typed HttpClient Pattern)
- Single-File Publish Kompatibilität für Plugin-System
- Dokumentation umfassend aktualisiert

### Fixed
- Embedded Resource Loading für Theme Assets
- Plugin loading für Plugin-Management Commands übersprungen
- --yes Flag für plugin uninstall hinzugefügt

## [0.0.1-beta.4] - 2025-12-15

### Added
- **Template/Asset Resolver System**: Theme Files Command
- **FileHashService**: Service für Datei-Hashing
- CancellationToken-Propagierung durch alle CLI Commands
- Interactive Mode mit menügesteuerter Oberfläche

### Changed
- Image Caching verbessert (Hash in Processing-Phase verschoben)
- Logging Defaults optimiert

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

[Unreleased]: https://github.com/spectara/revela/compare/v0.0.1-beta.7...HEAD
[0.0.1-beta.7]: https://github.com/spectara/revela/compare/v0.0.1-beta.6...v0.0.1-beta.7
[0.0.1-beta.6]: https://github.com/spectara/revela/compare/v0.0.1-beta.5...v0.0.1-beta.6
[0.0.1-beta.5]: https://github.com/spectara/revela/compare/v0.0.1-beta.4...v0.0.1-beta.5
[0.0.1-beta.4]: https://github.com/spectara/revela/compare/v0.0.1-beta.3...v0.0.1-beta.4
[0.0.1-beta.3]: https://github.com/spectara/revela/compare/v0.0.1-beta.2...v0.0.1-beta.3
[0.0.1-beta.2]: https://github.com/spectara/revela/compare/v0.0.1-beta.1...v0.0.1-beta.2
[0.0.1-beta.1]: https://github.com/spectara/revela/releases/tag/v0.0.1-beta.1
