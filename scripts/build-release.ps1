<#
.SYNOPSIS
    Build a Revela release locally — mirrors the GitHub release pipeline.

.DESCRIPTION
    Produces one or both release variants exactly as published on
    GitHub Releases (https://github.com/spectara/revela/releases):

      • Standalone — Cli.Embedded single-file binary with everything baked in
                     (no plugin management). Recommended for end users.
      • Full       — Cli single-file binary + every plugin/theme as .nupkg in
                     packages/. Modular: install/update individual packages
                     via `revela plugin install` against the local feed.

    Each variant is published into a timestamped folder under
    artifacts/releases/. Files mirror the GitHub release ZIP/TAR contents
    (no archiving — folders for fast iteration).

.PARAMETER Variant
    Which variant(s) to build:
      Standalone | Full | All  (default: All)

.PARAMETER Version
    Version number written into the binary and packages.
    Default: 0.0.0-test

.PARAMETER RuntimeIdentifier
    Target runtime (default: auto-detected from current OS).
    Examples: win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64

.PARAMETER SkipBuild
    Reuse existing build output. Faster iteration when only re-publishing.

.PARAMETER Open
    Open the output folder in Explorer/Finder/file manager after the build.

.EXAMPLE
    .\scripts\build-release.ps1
    # Build both variants for the current OS

.EXAMPLE
    .\scripts\build-release.ps1 -Variant Standalone -Open
    # Only the Standalone variant, then open the folder

.EXAMPLE
    .\scripts\build-release.ps1 -Variant Full -Version 1.0.0-rc.1
    # Full variant with a specific version
#>

[CmdletBinding()]
param(
    [ValidateSet('Standalone', 'Full', 'All')]
    [string]$Variant = 'All',

    [string]$Version = '0.0.0-test',

    [string]$RuntimeIdentifier,

    [switch]$SkipBuild,

    [switch]$Open
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --------------------------------------------------------------------------
# Output helpers
# --------------------------------------------------------------------------
function Write-Banner {
    param([string]$Title)
    $line = '═' * 60
    Write-Host ''
    Write-Host "╔$line╗" -ForegroundColor Magenta
    Write-Host "║ $($Title.PadRight(58)) ║" -ForegroundColor Magenta
    Write-Host "╚$line╝" -ForegroundColor Magenta
}
function Write-Step    { param([string]$Msg) Write-Host "`n▶ $Msg" -ForegroundColor Cyan }
function Write-Success { param([string]$Msg) Write-Host "  ✓ $Msg" -ForegroundColor Green }
function Write-Info    { param([string]$Msg) Write-Host "  ℹ $Msg" -ForegroundColor Gray }
function Write-Warn    { param([string]$Msg) Write-Host "  ⚠ $Msg" -ForegroundColor Yellow }

# --------------------------------------------------------------------------
# Resolve paths and runtime
# --------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$Timestamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss')

$isWindowsOS = $IsWindows -or $env:OS -eq 'Windows_NT'
if (-not $RuntimeIdentifier) {
    $RuntimeIdentifier = if ($isWindowsOS) { 'win-x64' }
        elseif ($IsMacOS) { 'osx-x64' }
        else { 'linux-x64' }
}

$ExeName = if ($RuntimeIdentifier -like 'win-*') { 'revela.exe' } else { 'revela' }

# --------------------------------------------------------------------------
# Build/Pack inventory is no longer hardcoded here.
# - `dotnet build Spectara.Revela.slnx` walks the whole solution.
# - `dotnet pack` emits packages for every csproj with <IsPackable>true</IsPackable>
#   (default false in Directory.Build.props; opted in by Sdk, Cli tool,
#   all Plugins, all Themes, and Features.{Generate,Theme,Projects}).
# Adding a new plugin = single <IsPackable>true</IsPackable> line in its csproj.
# --------------------------------------------------------------------------

$VariantsToBuild = if ($Variant -eq 'All') {
    @('Standalone', 'Full')
} else {
    @($Variant)
}

# --------------------------------------------------------------------------
# Header
# --------------------------------------------------------------------------
Write-Banner 'Revela Release Builder'
Write-Info "Variant(s): $($VariantsToBuild -join ', ')"
Write-Info "Version:    $Version"
Write-Info "Runtime:    $RuntimeIdentifier"
Write-Info "Output:     artifacts/releases/{variant}-$Timestamp/"

Push-Location $RepoRoot
try {
    # ----------------------------------------------------------------------
    # Restore + solution-wide Release build.
    #
    # The historical per-project loop was needed to work around an MSBuild
    # parallelism bug that produced Debug output for plugins/themes when
    # tests referenced them. Fixed in SDK 10.0.203 — solution build is fine.
    # ----------------------------------------------------------------------
    if (-not $SkipBuild) {
        Write-Step 'Restoring NuGet packages'
        dotnet restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
        Write-Success 'Restore completed'

        Write-Step 'Building solution (Release)'
        # `-p:DebugType=embedded` keeps debug info inside the DLL itself —
        # no separate .pdb files. This makes the build output stable across
        # later `dotnet publish` calls (which would otherwise strip PDBs and
        # break `dotnet pack --no-build` with NU5026).
        dotnet build Spectara.Revela.slnx -c Release --no-restore `
            -p:Version=$Version `
            -p:DebugType=embedded `
            --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
        Write-Success 'Solution built'
    } else {
        Write-Step 'Skipping build (-SkipBuild)'
    }

    $BuiltVariants = @()

    foreach ($v in $VariantsToBuild) {
        Write-Banner "Variant: $v"

        $outputDir = Join-Path $RepoRoot "artifacts/releases/$($v.ToLowerInvariant())-$Timestamp"
        $exePath   = Join-Path $outputDir $ExeName
        $docsDir   = Join-Path $outputDir 'getting-started'

        if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

        $project = if ($v -eq 'Standalone') {
            'src/Cli.Embedded/Cli.Embedded.csproj'
        } else {
            'src/Cli/Cli.csproj'
        }

        # ------------------------------------------------------------------
        # Publish self-contained single-file binary.
        #
        # `DebugType=embedded` keeps symbol info inside each DLL (no .pdb
        # files) so stack traces in the released binary still show file/line
        # numbers. Costs ~350 KB / 0.7% in the bundle vs. stripping them
        # entirely — a worthwhile tradeoff for a tool that may need user
        # bug reports.
        # ------------------------------------------------------------------
        Write-Step "Publishing $project"
        dotnet publish $project `
            -c Release `
            -r $RuntimeIdentifier `
            --self-contained `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=embedded `
            -p:Version=$Version `
            -o $outputDir `
            --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $project" }

        # Strip XML doc files (not needed at runtime)
        Get-ChildItem $outputDir -Filter '*.xml' -File `
            | Remove-Item -Force -ErrorAction SilentlyContinue

        if (-not (Test-Path $exePath)) {
            throw "Expected executable not found: $exePath"
        }
        $exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
        Write-Success "Published $ExeName ($exeSize MB)"

        # ------------------------------------------------------------------
        # Launcher script (macOS/Linux only)
        # ------------------------------------------------------------------
        if ($RuntimeIdentifier -like 'osx-*') {
            $src = Join-Path $ScriptDir 'launchers/start-revela.command'
            Copy-Item $src (Join-Path $outputDir 'Start Revela.command')
            Write-Success "Copied 'Start Revela.command' (macOS launcher)"
        } elseif ($RuntimeIdentifier -like 'linux-*') {
            $src = Join-Path $ScriptDir 'launchers/start-revela.sh'
            Copy-Item $src (Join-Path $outputDir 'start-revela.sh')
            Write-Success "Copied 'start-revela.sh' (Linux launcher)"
        }

        # ------------------------------------------------------------------
        # Documentation
        # ------------------------------------------------------------------
        Write-Step 'Copying getting-started/*.md'
        Copy-Item 'docs/getting-started/*.md' $docsDir
        $docCount = (Get-ChildItem $docsDir -Filter '*.md').Count
        Write-Success "Copied $docCount documentation files"

        # ------------------------------------------------------------------
        # Full variant — pack plugins/themes/SDK into packages/
        #
        # `IsPackable` is set per-project in csproj (default false in
        # Directory.Build.props, true on the 14 published projects), so a
        # solution-wide pack emits exactly the right packages.
        #
        # Safe with `--no-build` because the Release build above used
        # `DebugType=embedded` (no separate .pdb files — see NU5026).
        # ------------------------------------------------------------------
        if ($v -eq 'Full') {
            $packagesDir = Join-Path $outputDir 'packages'
            New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null

            Write-Step 'Packing NuGet packages (plugins, themes, SDK, CLI tool)'

            dotnet pack Spectara.Revela.slnx `
                -c Release -o $packagesDir `
                -p:PackageVersion=$Version `
                -p:Version=$Version `
                -p:IncludeSymbols=false `
                -p:DebugType=embedded `
                --no-build --no-restore --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw 'Pack failed' }

            $pkgFiles = Get-ChildItem $packagesDir -Filter '*.nupkg'
            Write-Success "Packed $($pkgFiles.Count) NuGet packages"
            foreach ($p in $pkgFiles) {
                $size = [math]::Round($p.Length / 1KB, 1)
                Write-Info "  • $($p.Name) ($size KB)"
            }
        }

        # ------------------------------------------------------------------
        # Smoke test — verify the binary actually runs
        # ------------------------------------------------------------------
        # Only test when the runtime matches the host (cross-compiled binaries
        # cannot be executed locally).
        $hostRid = if ($isWindowsOS) { 'win-x64' }
            elseif ($IsMacOS) { if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' } }
            else { 'linux-x64' }

        if ($RuntimeIdentifier -eq $hostRid) {
            Write-Step 'Smoke test: revela --version'
            $smokeOutput = & $exePath --version 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "Smoke test failed (exit $LASTEXITCODE): $smokeOutput"
            }
            Write-Success "Binary runs: $smokeOutput"
        } else {
            Write-Warn "Skipping smoke test (cross-compiled $RuntimeIdentifier on $hostRid)"
        }

        $totalSize = [math]::Round(
            (Get-ChildItem $outputDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB,
            1)
        Write-Success "Variant $v complete — $totalSize MB total at $outputDir"

        $BuiltVariants += [PSCustomObject]@{
            Variant = $v
            Path    = $outputDir
            Size    = $totalSize
        }
    }

    # ----------------------------------------------------------------------
    # Summary
    # ----------------------------------------------------------------------
    Write-Banner 'Build Complete'
    foreach ($b in $BuiltVariants) {
        Write-Host ('  {0,-12} {1,8} MB   {2}' -f $b.Variant, $b.Size, $b.Path) -ForegroundColor White
    }
    Write-Host ''

    if ($Open -and $BuiltVariants.Count -gt 0) {
        $firstPath = $BuiltVariants[0].Path
        if ($isWindowsOS) {
            Start-Process explorer.exe -ArgumentList $firstPath
        } elseif ($IsMacOS) {
            & open $firstPath
        } else {
            & xdg-open $firstPath
        }
    }
}
finally {
    Pop-Location
}
