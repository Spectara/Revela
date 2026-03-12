---
name: test-release
description: Runs the end-to-end release pipeline test using scripts/test-release.ps1. Tests build, pack, plugin install, generate all, compress, clean, idempotency, and dotnet tool install. Use when the user wants to test a release, verify the pipeline, check packaging, or validate before publishing.
---

# Test Release Pipeline — Revela Project

Run a full end-to-end release pipeline test locally. Validates that the entire release workflow works: build, pack, publish, install plugins, generate a site, and install as dotnet tool.

## Script

```powershell
.\scripts\test-release.ps1
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Version` | `0.0.0-test` | Version number for the test build |
| `-SkipTests` | (off) | Skip unit tests for faster iteration |
| `-IncludeOneDrive` | (off) | Also test OneDrive sync (requires network + valid share URL) |
| `-KeepArtifacts` | (off) | Keep test artifacts after completion |
| `-RuntimeIdentifier` | auto-detected | Target platform (`win-x64`, `linux-x64`, `osx-x64`, etc.) |

## What It Tests

The script runs 13 sequential steps:

| Step | What | Validates |
|------|------|-----------|
| 1 | Clean & Prepare | Test directory setup |
| 2 | Restore & Build | Solution compiles in Release |
| 3 | Run Tests | All unit + integration tests pass (skippable) |
| 4 | Publish CLI | Self-contained executable works |
| 5 | Build NuGet Packages | All plugins, themes, SDK pack correctly |
| 6 | Integration Setup | Showcase sample copied to temp dir |
| 7 | Install Plugins | Plugin install from local NuGet feed |
| 7b | Verify Plugins | Plugin list, uninstall, re-install, serve --help |
| 7c | Theme List | Built-in theme + online search |
| 7d | OneDrive Sync | Optional, requires `-IncludeOneDrive` |
| 7e | CLI Commands | create page, config statistics, config locations |
| 8 | **generate all** | Full pipeline: scan → statistics → pages → images |
| 9 | Validate Output | index.html, images, galleries, _assets exist |
| 10 | **Compress** | Install compress plugin, generate compress, clean compress |
| 11 | **Idempotency** | clean all → generate all → generate all (incremental) |
| 12 | **dotnet tool** | Pack, install globally, verify version, uninstall |
| 13 | Summary | Timing report |

## Common Usage

```powershell
# Full test (recommended before release)
.\scripts\test-release.ps1

# Quick iteration (skip unit tests)
.\scripts\test-release.ps1 -SkipTests

# Test specific version
.\scripts\test-release.ps1 -Version "0.1.0-beta.1" -KeepArtifacts

# Full test including OneDrive download
.\scripts\test-release.ps1 -IncludeOneDrive
```

## Test Data

Uses `samples/showcase/` as test project:
- **14 JPEGs** with real EXIF data (Canon, Sony, Nikon)
- **1.1 MB total** — Git-tracked, no network needed
- Multiple galleries, shared `_images/`, statistics page

## Interpreting Failures

| Failure | Likely Cause | Fix |
|---------|-------------|-----|
| Step 2 Build failed | Compile error | Fix the build error, check `dotnet build -c Release` |
| Step 5 Pack failed (NU5026) | Missing runtimeconfig.json | Ensure `dotnet build -c Release` ran for that project |
| Step 5 Pack failed (NU5039) | Missing README.md | Add `IsPackable=false` or create README for the project |
| Step 7 Plugin install failed | Package not found in local feed | Check pack step output, verify .nupkg exists in plugins/ dir |
| Step 7b Plugin count mismatch | Plugin interface changed | Check `IPlugin` implementation, verify `GetCommands()` returns correctly |
| Step 8 generate all failed | Runtime error in pipeline | Run `revela generate all` manually in sample dir for full error output |
| Step 11 Idempotency failed | Clean doesn't fully reset state | Check `clean all` implementation, verify cache is cleared |
| Step 12 Version mismatch | `-p:Version` not passed to pack | Ensure both `-p:Version` and `-p:PackageVersion` are set |

## Artifacts

Output in `artifacts/release-test-{timestamp}/`:

```
artifacts/release-test-20260311-151058/
├── cli/                    # Self-contained executable + bundled Lumina
│   ├── revela.exe
│   ├── Spectara.Revela.Themes.Lumina.dll
│   └── plugins/            # Installed plugins
├── nuget/                  # SDK package
├── plugins/                # Plugin/theme .nupkg files
├── tool/                   # dotnet tool .nupkg
└── sample/                 # Test project with generated output
    ├── project.json
    ├── source/
    ├── output/
    └── .cache/
```

## Duration

Typical times (Windows, with `-SkipTests`):
- **~2.5 minutes** total
- Build + Pack: ~1.5 min
- Generate + Validate: ~30s
- dotnet tool test: ~20s
