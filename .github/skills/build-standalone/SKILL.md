---
name: build-standalone
description: Builds a local standalone Revela release for testing using scripts/build-standalone.ps1. Creates a self-contained executable with all plugins and themes. Use when the user wants to build locally, test a release, create a standalone build, or try the full release package.
---

# Build Standalone Release — Revela Project

Build a local standalone release for testing. This does NOT create a git tag or GitHub release — it's for local testing only.

## Script

```powershell
.\scripts\build-standalone.ps1 -Full -Open
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Full` | (off) | Include all NuGet packages (themes + plugins). Without this, only the CLI executable is built. |
| `-Version` | `0.0.0-test` | Version number for the build |
| `-RuntimeIdentifier` | `win-x64` | Target platform (`win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`) |
| `-Open` | (off) | Open the output folder in Explorer after build |

## What It Creates

Output in `playground/standalone-full-{timestamp}/`:

```
playground/standalone-full-20260212-143000/
├── revela.exe                              # Self-contained CLI
├── getting-started/                        # Documentation
│   ├── README.md
│   ├── en.md
│   ├── de.md
│   └── cli-reference.md
└── packages/                               # Local NuGet feed (-Full only)
    ├── Spectara.Revela.Sdk.{version}.nupkg
    ├── Spectara.Revela.Theme.Lumina.{version}.nupkg
    ├── Spectara.Revela.Theme.Lumina.Statistics.{version}.nupkg
    ├── Spectara.Revela.Plugin.Statistics.{version}.nupkg
    ├── Spectara.Revela.Plugin.Source.OneDrive.{version}.nupkg
    ├── Spectara.Revela.Plugin.Serve.{version}.nupkg
    └── Spectara.Revela.Plugin.Compress.{version}.nupkg
```

## Common Usage

```powershell
# Full build with all packages, open in Explorer
.\scripts\build-standalone.ps1 -Full -Open

# Quick core build (CLI only, no plugins)
.\scripts\build-standalone.ps1

# Specific version for testing
.\scripts\build-standalone.ps1 -Full -Version "0.0.1-beta.15"

# Cross-platform build
.\scripts\build-standalone.ps1 -Full -RuntimeIdentifier linux-x64
```

## Testing the Build

After the script completes:
```powershell
cd playground/standalone-full-{timestamp}
.\revela.exe
```

This starts the interactive setup wizard. The bundled packages in `packages/` are used as a local NuGet feed — no internet needed for plugin installation.
