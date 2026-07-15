---
name: build-release
description: Builds a local Revela release for testing using scripts/build-release.ps1. Mirrors the GitHub release pipeline output — produces Standalone or Full variants. Use when the user wants to build a release locally, test packaging, create a self-contained executable, verify what end users would download, or validate the release before publishing.
argument-hint: "[Standalone|Full|All] [--Version 0.0.0-test]"
context: fork
---

# Build Release — Revela Project

Build one or both release variants locally to mirror what GitHub Releases publishes.
Does NOT create a git tag or GitHub release — for local testing only.

## Variants

| Variant | Source | Contents | Size |
|---------|--------|----------|------|
| **Standalone** | `Cli.Embedded` | **Native AOT** binary + `libvips` native dep (loaded via P/Invoke). All plugins/themes statically linked, no `plugin` command. Requires a platform-native toolchain at publish time (gcc/clang on Linux, MSVC on Windows, Xcode CLT on macOS). | ~36 MB (20 MB exe + 17 MB libvips) |
| **Full** | `Cli` + `packages/` | Single-file EXE + all `.nupkg` files as local NuGet feed. Modular: `revela plugin install <name>`. Pure managed (no native toolchain prereq). | ~130 MB |

> The GitHub release pipeline also produces a **Core** variant (`Cli` only, expects plugins from NuGet.org).
> It is omitted here because plugins are not published to NuGet.org yet — a Core build would have no theme and fail at `generate`.

## Script

```powershell
.\scripts\build-release.ps1                        # Both variants
.\scripts\build-release.ps1 -Variant Standalone    # Just one
.\scripts\build-release.ps1 -Variant Full -Open    # Open output folder afterwards
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Variant` | `All` | `Standalone`, `Full`, or `All` |
| `-Version` | `0.0.0-test` | Version baked into binaries and packages |
| `-RuntimeIdentifier` | auto-detect | `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` |
| `-SkipBuild` | (off) | Reuse existing build output (faster iteration) |
| `-Open` | (off) | Open the first output folder after build |

## Output Layout

`artifacts/releases/{variant}-{timestamp}/`

```
standalone-20260501-002132/         (Standalone — Cli.Embedded, Native AOT)
├── revela.exe                       (or `revela` on Linux/macOS)
└── libvips-42.dll                   (or libvips.so.42 / libvips.42.dylib)

full-20260501-002132/               (Full — Cli + packages/)
├── revela.exe
└── packages/
    ├── Spectara.Revela.{version}.nupkg                       (CLI tool)
    ├── Spectara.Revela.Sdk.{version}.nupkg
    ├── Spectara.Revela.Themes.Lumina.{version}.nupkg
    ├── Spectara.Revela.Themes.Lumina.Statistics.{version}.nupkg
    ├── Spectara.Revela.Themes.Lumina.Calendar.{version}.nupkg
    ├── Spectara.Revela.Plugins.Statistics.{version}.nupkg
    ├── Spectara.Revela.Plugins.Calendar.{version}.nupkg
    ├── Spectara.Revela.Plugins.Source.Calendar.{version}.nupkg
    ├── Spectara.Revela.Plugins.Source.OneDrive.{version}.nupkg
    ├── Spectara.Revela.Plugins.Serve.{version}.nupkg
    └── Spectara.Revela.Plugins.Compress.{version}.nupkg
```

On Linux/macOS each variant also gets a launcher script (`start-revela.sh` or `Start Revela.command`).

## Implementation Notes

1. **Per-project Release builds**: The script builds each plugin/theme/feature project explicitly with `dotnet build -c Release`, NOT via `dotnet build Solution.slnx -c Release`. Solution-level builds can produce Debug output for plugin projects when test projects are pulled in via dependency graph (MSBuild node reuse quirk). The GitHub workflow does the same.

2. **`DebugType=embedded`** (Full only): Both build and pack use `-p:DebugType=embedded` so symbol info lands inside each DLL instead of separate `.pdb` files. Avoids NU5026 ("PDB not found") when `dotnet pack --no-build` runs after `dotnet publish` (which strips PDBs from the shared `artifacts/bin/`). Stack traces still show file/line info because the symbols travel with the assembly.

3. **`DebugType=none` + `StripSymbols=true`** (Standalone/AOT only): AOT publish emits a native binary plus a separate debug companion (`.dbg` on Linux, `.dwarf` on macOS, `.pdb` on Windows). The script strips them post-publish to keep the release lean. Managed PDBs and XML docs are also swept.

4. **Native toolchain pre-flight** (Standalone only): The script aborts with an actionable install hint if `gcc`/`clang`/`link.exe` is missing on PATH — catches the missing-toolchain case before `dotnet publish` produces a cryptic NETSDK1144.

5. **Smoke test**: After publishing, the script runs `revela --version` to verify the binary actually executes (catches AOT/single-file-bundle issues early). Skipped automatically when cross-compiling.

## Quick Verification

```powershell
.\scripts\build-release.ps1 -Variant Standalone
$exe = (Get-Item artifacts\releases\standalone-* | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName + '\revela.exe'
& $exe -p samples\revela-website generate all
```

For a full pipeline test (install plugins, generate, verify .NET tool install), use `scripts/test-release.ps1` instead.
