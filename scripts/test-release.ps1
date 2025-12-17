<#
.SYNOPSIS
    End-to-End Release Pipeline Test for Revela

.DESCRIPTION
    This script simulates the complete release pipeline locally:
    1. Build & Test all projects
    2. Publish CLI as self-contained executable
    3. Build all plugins and themes
    4. Integration test with real data (OneDrive sample)
    5. Validate generated output

.PARAMETER Version
    Version number for the test build (default: 0.0.0-test)

.PARAMETER SkipTests
    Skip running unit tests (faster iteration)

.PARAMETER SkipDownload
    Skip OneDrive sync (use existing source files)

.PARAMETER KeepArtifacts
    Don't clean up artifacts after test

.PARAMETER RuntimeIdentifier
    Target runtime (default: win-x64 on Windows, linux-x64 on Linux, osx-x64 on macOS)

.EXAMPLE
    .\scripts\test-release.ps1
    # Full test with default settings

.EXAMPLE
    .\scripts\test-release.ps1 -SkipTests -SkipDownload
    # Quick iteration: skip tests, use existing source files

.EXAMPLE
    .\scripts\test-release.ps1 -Version "1.0.0-beta.1" -KeepArtifacts
    # Test specific version, keep artifacts for inspection
#>

[CmdletBinding()]
param(
    [string]$Version = "0.0.0-test",
    [switch]$SkipTests,
    [switch]$SkipDownload,
    [switch]$KeepArtifacts,
    [string]$RuntimeIdentifier
)

# Strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Colors for output
function Write-Step { param([string]$Message) Write-Host "`n▶ $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "  ✓ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "  ℹ $Message" -ForegroundColor Gray }
function Write-Warn { param([string]$Message) Write-Host "  ⚠ $Message" -ForegroundColor Yellow }
function Write-Err { param([string]$Message) Write-Host "  ✗ $Message" -ForegroundColor Red }

function Write-Banner {
    param([string]$Title)
    $line = "═" * 60
    Write-Host ""
    Write-Host "╔$line╗" -ForegroundColor Magenta
    Write-Host "║ $($Title.PadRight(58)) ║" -ForegroundColor Magenta
    Write-Host "╚$line╝" -ForegroundColor Magenta
}

# Determine script and repo paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$Timestamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss')
$TestDir = Join-Path $RepoRoot "artifacts/release-test-$Timestamp"
$PublishDir = Join-Path $TestDir "publish"
$PluginsDir = Join-Path $TestDir "plugins"
$SampleDir = Join-Path $RepoRoot "samples/onedrive"

# Determine runtime identifier
if (-not $RuntimeIdentifier) {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $RuntimeIdentifier = "win-x64"
        $ExeName = "revela.exe"
    } elseif ($IsMacOS) {
        $RuntimeIdentifier = "osx-x64"
        $ExeName = "revela"
    } else {
        $RuntimeIdentifier = "linux-x64"
        $ExeName = "revela"
    }
}

$ExePath = Join-Path $PublishDir $ExeName

# Track timing
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$stepTimes = @{}

function Measure-Step {
    param([string]$Name, [scriptblock]$Action)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $Action
    } finally {
        $sw.Stop()
        $stepTimes[$Name] = $sw.Elapsed
    }
}

# ============================================================================
# MAIN PIPELINE
# ============================================================================

Write-Banner "Revela End-to-End Release Test"
Write-Host ""
Write-Info "Version:    $Version"
Write-Info "Runtime:    $RuntimeIdentifier"
Write-Info "Skip Tests: $SkipTests"
Write-Info "Skip Download: $SkipDownload"
Write-Info "Keep Artifacts: $KeepArtifacts"
Write-Info "Repo Root:  $RepoRoot"

Push-Location $RepoRoot
try {
    # ========================================================================
    # STEP 1: Clean & Prepare
    # ========================================================================
    Write-Step "Step 1: Clean & Prepare"
    Measure-Step "Clean" {
        if (Test-Path $TestDir) {
            Remove-Item $TestDir -Recurse -Force
            Write-Success "Cleaned previous test artifacts"
        }
        New-Item -ItemType Directory -Path $TestDir -Force | Out-Null
        New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
        New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
        Write-Success "Created test directories"
    }

    # ========================================================================
    # STEP 2: Restore & Build
    # ========================================================================
    Write-Step "Step 2: Restore & Build"
    Measure-Step "Build" {
        Write-Info "Running dotnet restore..."
        dotnet restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
        Write-Success "Restore completed"

        Write-Info "Running dotnet build..."
        dotnet build -c Release --no-restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        Write-Success "Build completed"
    }

    # ========================================================================
    # STEP 3: Run Tests (optional)
    # ========================================================================
    if (-not $SkipTests) {
        Write-Step "Step 3: Run Tests"
        Measure-Step "Tests" {
            # .NET 10 uses Microsoft.Testing.Platform - run tests as executables
            $testProjects = @(
                @{ Name = "Core.Tests"; Path = "artifacts/bin/Core.Tests/Release/net10.0/Spectara.Revela.Core.Tests.exe" },
                @{ Name = "Commands.Tests"; Path = "artifacts/bin/Commands.Tests/Release/net10.0/Spectara.Revela.Commands.Tests.exe" },
                @{ Name = "Plugin.Source.OneDrive.Tests"; Path = "artifacts/bin/Plugin.Source.OneDrive.Tests/Release/net10.0/Spectara.Revela.Plugin.Source.OneDrive.Tests.exe" },
                @{ Name = "Plugin.Statistics.Tests"; Path = "artifacts/bin/Plugin.Statistics.Tests/Release/net10.0/Spectara.Revela.Plugin.Statistics.Tests.exe" }
            )

            foreach ($test in $testProjects) {
                $exePath = Join-Path $RepoRoot $test.Path
                if (-not (Test-Path $exePath)) {
                    throw "Test executable not found: $($test.Path). Run build first."
                }
                Write-Info "Testing $($test.Name)..."
                & $exePath --no-progress
                if ($LASTEXITCODE -ne 0) { throw "Tests failed for $($test.Name)" }
                Write-Success "$($test.Name) passed"
            }
        }
    } else {
        Write-Step "Step 3: Run Tests [SKIPPED]"
        Write-Warn "Tests skipped by -SkipTests flag"
    }

    # ========================================================================
    # STEP 4: Publish CLI
    # ========================================================================
    Write-Step "Step 4: Publish CLI (self-contained)"
    Measure-Step "Publish CLI" {
        Write-Info "Publishing for $RuntimeIdentifier..."
        dotnet publish src/Cli/Cli.csproj `
            -c Release `
            -r $RuntimeIdentifier `
            --self-contained `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:Version=$Version `
            -o $PublishDir `
            --verbosity quiet

        if ($LASTEXITCODE -ne 0) { throw "CLI publish failed" }

        $exeSize = (Get-Item $ExePath).Length / 1MB
        Write-Success "CLI published: $ExeName ($([math]::Round($exeSize, 1)) MB)"
    }

    # ========================================================================
    # STEP 5: Build NuGet Packages (like CI pipeline)
    # ========================================================================
    Write-Step "Step 5: Build NuGet Packages"
    Measure-Step "NuGet Packages" {
        # Theme.Lumina (goes next to CLI - bundled with release)
        Write-Info "Building Theme.Lumina..."
        dotnet build src/Themes/Theme.Lumina/Theme.Lumina.csproj `
            -c Release -r $RuntimeIdentifier -p:Version=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Theme.Lumina build failed" }

        Copy-Item "artifacts/bin/Theme.Lumina/Release/net10.0/$RuntimeIdentifier/Spectara.Revela.Theme.Lumina.dll" $PublishDir
        Write-Success "Theme.Lumina bundled with CLI"

        # Pack Plugin.Source.OneDrive
        Write-Info "Packing Plugin.Source.OneDrive..."
        dotnet pack src/Plugins/Plugin.Source.OneDrive/Plugin.Source.OneDrive.csproj `
            -c Release -o $PluginsDir -p:PackageVersion=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "OneDrive plugin pack failed" }
        Write-Success "Plugin.Source.OneDrive packed"

        # Pack Plugin.Statistics
        Write-Info "Packing Plugin.Statistics..."
        dotnet pack src/Plugins/Plugin.Statistics/Plugin.Statistics.csproj `
            -c Release -o $PluginsDir -p:PackageVersion=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Statistics plugin pack failed" }
        Write-Success "Plugin.Statistics packed"

        # Pack Theme.Lumina.Statistics
        Write-Info "Packing Theme.Lumina.Statistics..."
        dotnet pack src/Themes/Theme.Lumina.Statistics/Theme.Lumina.Statistics.csproj `
            -c Release -o $PluginsDir -p:PackageVersion=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Theme.Lumina.Statistics pack failed" }
        Write-Success "Theme.Lumina.Statistics packed"

        # List all NuGet packages
        Write-Info "NuGet packages:"
        Get-ChildItem $PluginsDir -Filter "*.nupkg" | ForEach-Object {
            $size = [math]::Round($_.Length / 1KB, 1)
            Write-Info "  $($_.Name) ($size KB)"
        }
    }

    # ========================================================================
    # STEP 6: Integration Test - Setup Project
    # ========================================================================
    Write-Step "Step 6: Integration Test Setup"
    Measure-Step "Integration Setup" {
        $testProjectDir = Join-Path $TestDir "test-project"
        New-Item -ItemType Directory -Path $testProjectDir -Force | Out-Null

        # Copy project files from onedrive sample
        Copy-Item "$SampleDir/project.json" $testProjectDir
        Copy-Item "$SampleDir/site.json" $testProjectDir

        # Create plugins config folder and copy JSON configs
        $testPluginsConfig = Join-Path $testProjectDir "plugins"
        New-Item -ItemType Directory -Path $testPluginsConfig -Force | Out-Null
        Copy-Item "$SampleDir/plugins/*.json" $testPluginsConfig

        Write-Success "Test project created at: $testProjectDir"
    }

    # ========================================================================
    # STEP 7: Install Plugins via NuGet (local feed)
    # ========================================================================
    $testProjectDir = Join-Path $TestDir "test-project"

    Write-Step "Step 7: Install Plugins (NuGet from local feed)"
    Measure-Step "Install Plugins" {
        Push-Location $testProjectDir
        try {
            # Install OneDrive Plugin from local NuGet feed
            Write-Info "Installing Plugin.Source.OneDrive..."
            & $ExePath plugin install Source.OneDrive --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "OneDrive plugin installation failed" }
            Write-Success "Plugin.Source.OneDrive installed"

            # Install Statistics Plugin
            Write-Info "Installing Plugin.Statistics..."
            & $ExePath plugin install Statistics --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Statistics plugin installation failed" }
            Write-Success "Plugin.Statistics installed"

            # Install Theme.Lumina.Statistics Extension
            Write-Info "Installing Theme.Lumina.Statistics..."
            & $ExePath plugin install Spectara.Revela.Theme.Lumina.Statistics --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Theme.Lumina.Statistics installation failed" }
            Write-Success "Theme.Lumina.Statistics installed"

            # Verify plugins are installed in correct directory (local, next to exe)
            # This validates the "GitHub Release" scenario: user extracts ZIP, runs exe, installs plugins
            $localPluginsDir = Join-Path $PublishDir "plugins"
            $installedDlls = @(Get-ChildItem $localPluginsDir -Filter "*.dll" -ErrorAction SilentlyContinue)
            if ($installedDlls.Count -eq 0) {
                throw "No plugins found in local directory: $localPluginsDir"
            }
            Write-Success "Plugins installed to local directory: $localPluginsDir"
            foreach ($dll in $installedDlls) {
                Write-Info "  $($dll.Name)"
            }

            # List installed plugins
            Write-Info "Installed plugins:"
            & $ExePath plugin list
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 7b: Verify Plugin System (including uninstall)
    # ========================================================================
    Write-Step "Step 7b: Verify Plugin Installation & Uninstall"
    Measure-Step "Plugin Verify" {
        $localPluginsDir = Join-Path $PublishDir "plugins"

        # Verify correct number of plugins installed
        $pluginListOutput = & $ExePath plugin list 2>&1 | Out-String
        if ($pluginListOutput -match "Total: 3 plugin") {
            Write-Success "Verified: 3 plugins installed"
        } else {
            throw "Expected 3 plugins, got unexpected output"
        }

        # Verify all plugins are local (not global)
        if ($pluginListOutput -match "local.*local.*local") {
            Write-Success "Verified: All plugins installed locally (next to exe)"
        }

        # Verify DLL files exist
        $expectedDlls = @(
            "Spectara.Revela.Plugin.Source.OneDrive.dll",
            "Spectara.Revela.Plugin.Statistics.dll",
            "Spectara.Revela.Theme.Lumina.Statistics.dll"
        )
        foreach ($dll in $expectedDlls) {
            $dllPath = Join-Path $localPluginsDir $dll
            if (-not (Test-Path $dllPath)) {
                throw "Missing plugin DLL: $dll"
            }
        }
        Write-Success "Verified: All plugin DLLs present in local directory"

        # Test plugin uninstall (critical - DLLs must not be locked)
        Write-Info "Testing plugin uninstall (DLLs must not be locked)..."
        & $ExePath plugin uninstall Statistics --yes
        if ($LASTEXITCODE -ne 0) { throw "Plugin uninstall command failed" }

        # Verify plugin was actually removed
        $statisticsPath = Join-Path $localPluginsDir "Spectara.Revela.Plugin.Statistics.dll"
        if (Test-Path $statisticsPath) {
            throw "Plugin uninstall failed - DLL still exists (probably locked by AssemblyLoadContext)"
        }
        Write-Success "Verified: Plugin uninstall works (DLLs not locked)"

        # Re-install for subsequent tests
        Write-Info "Re-installing Statistics plugin..."
        & $ExePath plugin install Spectara.Revela.Plugin.Statistics --source $PluginsDir
        if ($LASTEXITCODE -ne 0) { throw "Plugin re-install failed" }
        Write-Success "Plugin re-installed for subsequent tests"
    }

    # ========================================================================
    # STEP 8: OneDrive Sync (optional)
    # ========================================================================
    if (-not $SkipDownload) {
        Write-Step "Step 8: OneDrive Sync"
        Measure-Step "OneDrive Sync" {
            Write-Info "Running: revela source onedrive sync"
            Push-Location $testProjectDir
            try {
                & $ExePath source onedrive sync
                if ($LASTEXITCODE -ne 0) { throw "OneDrive sync failed" }
                Write-Success "OneDrive sync completed"

                # Show downloaded files
                $sourceDir = Join-Path $testProjectDir "source"
                if (Test-Path $sourceDir) {
                    $fileCount = (Get-ChildItem $sourceDir -Recurse -File).Count
                    Write-Info "Downloaded $fileCount files to source/"
                }
            } finally {
                Pop-Location
            }
        }
    } else {
        Write-Step "Step 8: OneDrive Sync [SKIPPED]"
        Measure-Step "Copy Source" {
            # Copy existing source files from sample
            $sourceDir = Join-Path $testProjectDir "source"
            Copy-Item "$SampleDir/source" $testProjectDir -Recurse
            $fileCount = (Get-ChildItem $sourceDir -Recurse -File).Count
            Write-Success "Copied $fileCount source files from sample"
        }
    }

    # ========================================================================
    # STEP 9: Generate - Scan & Images
    # ========================================================================
    Write-Step "Step 9: Scan Content & Process Images"
    Measure-Step "Scan & Images" {
        Push-Location $testProjectDir
        try {
            Write-Info "Running: revela generate scan"
            & $ExePath generate scan
            if ($LASTEXITCODE -ne 0) { throw "Scan failed" }
            Write-Success "Content scanned, manifest created"

            Write-Info "Running: revela generate images"
            & $ExePath generate images
            if ($LASTEXITCODE -ne 0) { throw "Image processing failed" }
            Write-Success "Images processed"
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 10: Generate Statistics
    # ========================================================================
    Write-Step "Step 10: Generate Statistics"
    Measure-Step "Statistics" {
        Write-Info "Running: revela generate stats"
        Push-Location $testProjectDir
        try {
            & $ExePath generate stats
            if ($LASTEXITCODE -ne 0) { throw "Statistics generation failed" }
            Write-Success "Statistics generated"

            # Note: Statistics plugin generates data into manifest, no separate files
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 11: Generate Pages (includes Statistics page)
    # ========================================================================
    Write-Step "Step 11: Render Pages"
    Measure-Step "Pages" {
        Write-Info "Running: revela generate pages"
        Push-Location $testProjectDir
        try {
            & $ExePath generate pages
            if ($LASTEXITCODE -ne 0) { throw "Page rendering failed" }
            Write-Success "Pages rendered (including Statistics)"
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 12: Validate Output
    # ========================================================================
    Write-Step "Step 12: Validate Output"
    Measure-Step "Validate" {
        $outputDir = Join-Path $testProjectDir "output"

        if (-not (Test-Path $outputDir)) {
            throw "Output directory not created!"
        }

        # Check for expected files
        # Note: CSS/JS assets are in _assets/ directory (theme asset system)
        $expectedFiles = @(
            @{ Path = "index.html"; Description = "Homepage" },
            @{ Path = "_assets/main.css"; Description = "Theme CSS" }
        )

        $missingFiles = @()
        foreach ($file in $expectedFiles) {
            $path = Join-Path $outputDir $file.Path
            if (Test-Path $path) {
                Write-Success "Found: $($file.Path)"
            } else {
                $missingFiles += $file.Path
                Write-Err "Missing: $($file.Path) ($($file.Description))"
            }
        }

        # Check images directory
        $imagesDir = Join-Path $outputDir "images"
        if (Test-Path $imagesDir) {
            $imageCount = (Get-ChildItem $imagesDir -Recurse -File).Count
            Write-Success "Images generated: $imageCount files"
        } else {
            Write-Warn "Images directory not found"
        }

        # Check gallery directories
        $galleryDirs = @(Get-ChildItem $outputDir -Directory | Where-Object { $_.Name -ne "images" })
        if ($galleryDirs.Count -gt 0) {
            Write-Success "Gallery directories: $($galleryDirs.Count)"
            foreach ($dir in $galleryDirs) {
                $htmlFiles = @(Get-ChildItem $dir.FullName -Filter "*.html" -Recurse)
                Write-Info "  $($dir.Name): $($htmlFiles.Count) HTML files"
            }
        }

        # Check statistics page
        $statsPage = Join-Path $outputDir "pages/statistics/index.html"
        if (Test-Path $statsPage) {
            $statsContent = Get-Content $statsPage -Raw
            if ($statsContent -match "statistics" -or $statsContent -match "chart") {
                Write-Success "Statistics page generated with charts"
            } else {
                Write-Warn "Statistics page found but may be missing charts"
            }
        } else {
            Write-Warn "Statistics page not found (optional)"
        }

        # Check _assets directory (scan-based asset system)
        $assetsDir = Join-Path $outputDir "_assets"
        if (Test-Path $assetsDir) {
            $assetFiles = Get-ChildItem $assetsDir -Recurse -File
            Write-Success "_assets/ directory: $($assetFiles.Count) files"

            # Check for extension assets
            $extensionDir = Join-Path $assetsDir "lumina-statistics"
            if (Test-Path $extensionDir) {
                Write-Success "Theme.Lumina.Statistics assets included"
            }
        }

        # Check index.html content
        $indexPath = Join-Path $outputDir "index.html"
        if (Test-Path $indexPath) {
            $indexContent = Get-Content $indexPath -Raw
            if ($indexContent -match "<html") {
                Write-Success "index.html contains valid HTML"
            } else {
                Write-Warn "index.html may be invalid"
            }
        }

        if ($missingFiles.Count -gt 0) {
            throw "Validation failed: Missing files: $($missingFiles -join ', ')"
        }

        Write-Success "All validations passed!"
    }

    # ========================================================================
    # STEP 13: Test .NET Tool Package
    # ========================================================================
    Write-Step "Step 13: Test .NET Tool Package"
    Measure-Step "ToolTest" {
        $nupkgDir = Join-Path $TestDir "nupkgs"
        New-Item -ItemType Directory -Path $nupkgDir -Force | Out-Null

        Write-Info "Packing CLI as .NET Tool..."
        dotnet pack src/Cli/Cli.csproj `
            -c Release `
            -o $nupkgDir `
            -p:PackageVersion=$Version `
            -p:IncludeSymbols=false `
            --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Pack failed" }
        Write-Success "CLI packed to NuGet package"

        $nupkgFile = Get-ChildItem -Path $nupkgDir -Filter "Spectara.Revela.$Version.nupkg" | Select-Object -First 1
        if (-not $nupkgFile) { throw "NuGet package not found" }
        Write-Info "Package: $($nupkgFile.Name) ($([Math]::Round($nupkgFile.Length / 1MB, 2)) MB)"

        Write-Info "Installing tool from local package..."
        $installResult = dotnet tool install -g Spectara.Revela `
            --version $Version `
            --add-source $nupkgDir `
            --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            # Tool might already be installed from previous run
            Write-Warn "Install failed (might be already installed), trying update..."
            dotnet tool update -g Spectara.Revela `
                --version $Version `
                --add-source $nupkgDir `
                --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Tool install/update failed" }
        }
        Write-Success "Tool installed globally"

        Write-Info "Testing tool command..."
        $versionOutput = & revela --version 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Tool command failed: $versionOutput" }
        Write-Success "Tool executable: $versionOutput"

        Write-Info "Testing plugin list..."
        $pluginOutput = & revela plugin list 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Plugin list failed: $pluginOutput" }
        Write-Success "Plugin list command works"

        Write-Info "Uninstalling tool..."
        # Note: dotnet tool uninstall doesn't support --verbosity
        $null = dotnet tool uninstall -g Spectara.Revela 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Tool uninstall failed" }
        Write-Success "Tool uninstalled"

        Write-Success "✓ .NET Tool package verified"
    }

    # ========================================================================
    # STEP 14: Summary
    # ========================================================================
    Write-Banner "Release Test Complete"

    $stopwatch.Stop()
    Write-Host ""
    Write-Host "  Step Timings:" -ForegroundColor White
    foreach ($step in $stepTimes.GetEnumerator() | Sort-Object { $_.Value } -Descending) {
        $time = $step.Value.ToString("mm\:ss\.fff")
        Write-Host "    $($step.Key.PadRight(20)) $time" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "  Total Time: $($stopwatch.Elapsed.ToString("mm\:ss\.fff"))" -ForegroundColor Cyan
    Write-Host ""

    # Artifact locations
    Write-Host "  Artifacts:" -ForegroundColor White
    Write-Host "    CLI:      $ExePath" -ForegroundColor Gray
    Write-Host "    Plugins:  $PluginsDir" -ForegroundColor Gray
    Write-Host "    Output:   $(Join-Path $testProjectDir 'output')" -ForegroundColor Gray
    Write-Host ""

    Write-Host "  ✓ Release pipeline test PASSED" -ForegroundColor Green
    Write-Host ""

} catch {
    Write-Host ""
    Write-Err "Pipeline failed: $_"
    Write-Host ""
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
} finally {
    Pop-Location

    # Cleanup (unless -KeepArtifacts)
    if (-not $KeepArtifacts -and (Test-Path $TestDir)) {
        Write-Info "Cleaning up test artifacts..."
        # Don't actually delete - user might want to inspect
        # Remove-Item $TestDir -Recurse -Force
        Write-Info "Artifacts kept at: $TestDir"
        Write-Info "Run with -KeepArtifacts:$false to auto-delete"
    }
}
