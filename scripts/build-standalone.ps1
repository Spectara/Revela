<#
.SYNOPSIS
    Build a standalone Revela release for testing

.DESCRIPTION
    Creates a complete standalone release structure in playground/ for testing.
    Each run creates a timestamped folder.

    Supports two variants:
    - Core: CLI executable only (default)
    - Full: CLI executable + all NuGet packages in packages/ folder

    Output structure (Full):
        playground/standalone-{timestamp}/
        ├── revela.exe
        ├── getting-started/
        │   ├── README.md
        │   ├── en.md
        │   ├── de.md
        │   └── cli-reference.md
        └── packages/           (local NuGet feed with all packages)
            ├── Spectara.Revela.Sdk.{version}.nupkg
            ├── Spectara.Revela.Theme.Lumina.{version}.nupkg
            ├── Spectara.Revela.Theme.Lumina.Statistics.{version}.nupkg
            ├── Spectara.Revela.Plugin.Statistics.{version}.nupkg
            ├── Spectara.Revela.Plugin.Source.OneDrive.{version}.nupkg
            └── Spectara.Revela.Plugin.Serve.{version}.nupkg

.PARAMETER Version
    Version number for the build (default: 0.0.0-test)

.PARAMETER RuntimeIdentifier
    Target runtime (default: win-x64)

.PARAMETER Full
    Include all NuGet packages in the packages/ folder (default: false = Core build)

.PARAMETER Open
    Open the output folder in Explorer after build

.EXAMPLE
    .\scripts\build-standalone.ps1
    # Build Core variant with defaults

.EXAMPLE
    .\scripts\build-standalone.ps1 -Full -Open
    # Build Full variant and open folder in Explorer

.EXAMPLE
    .\scripts\build-standalone.ps1 -Version "1.0.0-beta.1" -Full
    # Build Full variant with specific version
#>

[CmdletBinding()]
param(
    [string]$Version = "0.0.0-test",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$Full,
    [switch]$Open
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Colors
function Write-Step { param([string]$Message) Write-Host "`n▶ $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "  ✓ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "  ℹ $Message" -ForegroundColor Gray }

# Paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$Timestamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss')
$Variant = if ($Full) { "full" } else { "core" }
$OutputDir = Join-Path $RepoRoot "playground/standalone-$Variant-$Timestamp"
$PackagesDir = Join-Path $OutputDir "packages"
$GettingStartedDir = Join-Path $OutputDir "getting-started"

$ExeName = if ($RuntimeIdentifier -like "win-*") { "revela.exe" } else { "revela" }
$ExePath = Join-Path $OutputDir $ExeName

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║ Revela Standalone Build                                    ║" -ForegroundColor Magenta
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""
Write-Info "Version:  $Version"
Write-Info "Runtime:  $RuntimeIdentifier"
Write-Info "Variant:  $Variant"
Write-Info "Output:   $OutputDir"

Push-Location $RepoRoot
try {
    # ========================================================================
    # Step 1: Create directories
    # ========================================================================
    Write-Step "Creating directories"

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    New-Item -ItemType Directory -Path $GettingStartedDir -Force | Out-Null
    if ($Full) {
        New-Item -ItemType Directory -Path $PackagesDir -Force | Out-Null
    }

    Write-Success "Created output structure"

    # ========================================================================
    # Step 2: Build solution
    # ========================================================================
    Write-Step "Building solution"

    dotnet build -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    Write-Success "Build completed"

    # ========================================================================
    # Step 3: Publish CLI
    # ========================================================================
    Write-Step "Publishing CLI (self-contained)"

    dotnet publish src/Cli/Cli.csproj `
        -c Release `
        -r $RuntimeIdentifier `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        -o $OutputDir `
        --verbosity quiet

    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

    # Clean up XML docs
    Get-ChildItem $OutputDir -Filter "*.xml" | Remove-Item -Force -ErrorAction SilentlyContinue

    $exeSize = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
    Write-Success "CLI published: $ExeName ($exeSize MB)"

    # ========================================================================
    # Step 4: Copy launcher scripts (macOS/Linux)
    # ========================================================================
    if ($RuntimeIdentifier -like "osx-*" -or $RuntimeIdentifier -like "linux-*") {
        Write-Step "Copying launcher scripts"

        $launchersDir = Join-Path $ScriptDir "launchers"

        if ($RuntimeIdentifier -like "osx-*") {
            # macOS: .command file opens Terminal on double-click
            $sourcePath = Join-Path $launchersDir "start-revela.command"
            $destPath = Join-Path $OutputDir "Start Revela.command"
            Copy-Item -Path $sourcePath -Destination $destPath
            Write-Success "Copied 'Start Revela.command' (macOS double-click launcher)"
        }
        else {
            # Linux: .sh file
            $sourcePath = Join-Path $launchersDir "start-revela.sh"
            $destPath = Join-Path $OutputDir "start-revela.sh"
            Copy-Item -Path $sourcePath -Destination $destPath
            Write-Success "Copied 'start-revela.sh' (Linux double-click launcher)"
        }

        Write-Info "Launcher ensures correct working directory on double-click"
    }

    # ========================================================================
    # Step 5: Copy documentation
    # ========================================================================
    Write-Step "Copying documentation"

    Copy-Item -Path "docs/getting-started/*.md" -Destination $GettingStartedDir

    $docCount = (Get-ChildItem $GettingStartedDir -Filter "*.md").Count
    Write-Success "Copied $docCount documentation files"

    # ========================================================================
    # Step 6: Pack all packages (Full variant only)
    # ========================================================================
    if ($Full) {
        Write-Step "Packing NuGet packages"

        $packages = @(
            "src/Sdk/Sdk.csproj",
            "src/Themes/Theme.Lumina/Theme.Lumina.csproj",
            "src/Themes/Theme.Lumina.Statistics/Theme.Lumina.Statistics.csproj",
            "src/Plugins/Plugin.Statistics/Plugin.Statistics.csproj",
            "src/Plugins/Plugin.Source.OneDrive/Plugin.Source.OneDrive.csproj",
            "src/Plugins/Plugin.Serve/Plugin.Serve.csproj"
        )

        foreach ($proj in $packages) {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
            Write-Info "Packing $name..."

            dotnet pack $proj -c Release -o $PackagesDir -p:PackageVersion=$Version --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Pack failed for $name" }
        }

        $packageCount = (Get-ChildItem $PackagesDir -Filter "*.nupkg").Count
        Write-Success "Packed $packageCount packages"
    }

    # ========================================================================
    # Summary
    # ========================================================================
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║ Build Complete!                                            ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Output: " -NoNewline; Write-Host $OutputDir -ForegroundColor Yellow
    Write-Host "  Variant: " -NoNewline; Write-Host $Variant.ToUpper() -ForegroundColor $(if ($Full) { "Green" } else { "Cyan" })
    Write-Host ""
    Write-Host "  Contents:" -ForegroundColor Cyan
    Write-Host "    $ExeName" -ForegroundColor White
    if ($RuntimeIdentifier -like "osx-*") {
        Write-Host "    Start Revela.command  (double-click launcher)" -ForegroundColor White
    }
    elseif ($RuntimeIdentifier -like "linux-*") {
        Write-Host "    start-revela.sh       (double-click launcher)" -ForegroundColor White
    }
    Write-Host "    getting-started/" -ForegroundColor White
    if ($Full) {
        $packageCount = (Get-ChildItem $PackagesDir -Filter "*.nupkg").Count
        Write-Host "    packages/             ($packageCount packages)" -ForegroundColor White
        Write-Host ""
        Write-Host "  Bundled packages:" -ForegroundColor Cyan
        Get-ChildItem $PackagesDir -Filter "*.nupkg" | ForEach-Object {
            Write-Host "    • $($_.BaseName)" -ForegroundColor DarkGray
        }
    }
    Write-Host ""
    Write-Host "  Quick start:" -ForegroundColor Cyan
    if ($RuntimeIdentifier -like "win-*") {
        Write-Host "    cd `"$OutputDir`"" -ForegroundColor Yellow
        Write-Host "    .\$ExeName" -ForegroundColor Yellow
    }
    elseif ($RuntimeIdentifier -like "osx-*") {
        Write-Host "    Double-click 'Start Revela.command'" -ForegroundColor Yellow
        Write-Host "    Or in Terminal: cd `"$OutputDir`" && ./revela" -ForegroundColor Yellow
    }
    else {
        Write-Host "    Double-click 'start-revela.sh'" -ForegroundColor Yellow
        Write-Host "    Or in Terminal: cd `"$OutputDir`" && ./revela" -ForegroundColor Yellow
    }
    Write-Host ""

    if ($Open) {
        Start-Process "explorer.exe" -ArgumentList $OutputDir
    }
}
finally {
    Pop-Location
}
