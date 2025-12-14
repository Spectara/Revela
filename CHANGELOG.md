# Changelog

Alle wichtigen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.

Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
und dieses Projekt folgt [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

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

[Unreleased]: https://github.com/spectara/revela/compare/v0.0.1-beta.3...HEAD
[0.0.1-beta.3]: https://github.com/spectara/revela/compare/v0.0.1-beta.2...v0.0.1-beta.3
[0.0.1-beta.2]: https://github.com/spectara/revela/compare/v0.0.1-beta.1...v0.0.1-beta.2
[0.0.1-beta.1]: https://github.com/spectara/revela/releases/tag/v0.0.1-beta.1
