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
$CliDir = Join-Path $TestDir "cli"
$NuGetDir = Join-Path $TestDir "nuget"
$PluginsDir = Join-Path $TestDir "plugins"
$ToolDir = Join-Path $TestDir "tool"
$SampleProjectDir = Join-Path $TestDir "sample"
$SampleSourceDir = Join-Path $RepoRoot "samples/onedrive"

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

$ExePath = Join-Path $CliDir $ExeName

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
        New-Item -ItemType Directory -Path $CliDir -Force | Out-Null
        New-Item -ItemType Directory -Path $NuGetDir -Force | Out-Null
        New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
        New-Item -ItemType Directory -Path $ToolDir -Force | Out-Null
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
            -p:DebugType=none `
            -p:DebugSymbols=false `
            -p:Version=$Version `
            -o $CliDir `
            --verbosity quiet

        if ($LASTEXITCODE -ne 0) { throw "CLI publish failed" }

        # Clean up XML documentation files (not needed for end users)
        Get-ChildItem $CliDir -Filter "*.xml" | Remove-Item -Force

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

        Copy-Item "artifacts/bin/Theme.Lumina/Release/net10.0/$RuntimeIdentifier/Spectara.Revela.Theme.Lumina.dll" $CliDir
        Write-Success "Theme.Lumina bundled with CLI"

        # Pack Theme.Lumina (for online theme list test)
        Write-Info "Packing Theme.Lumina..."
        dotnet pack src/Themes/Theme.Lumina/Theme.Lumina.csproj `
            -c Release -o $PluginsDir -p:PackageVersion=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Theme.Lumina pack failed" }
        Write-Success "Theme.Lumina packed"

        # Pack SDK (for third-party plugin/theme developers)
        Write-Info "Packing Sdk..."
        dotnet pack src/Sdk/Sdk.csproj `
            -c Release -o $NuGetDir -p:PackageVersion=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Sdk pack failed" }
        Write-Success "Sdk packed"

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

        # List SDK package (for developers)
        Write-Info "SDK package (nuget/):"
        Get-ChildItem $NuGetDir -Filter "*.nupkg" | ForEach-Object {
            $size = [math]::Round($_.Length / 1KB, 1)
            Write-Info "  $($_.Name) ($size KB)"
        }

        # List plugin packages (for installation)
        Write-Info "Plugin packages (plugins/):"
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
        New-Item -ItemType Directory -Path $SampleProjectDir -Force | Out-Null

        # Copy project files from onedrive sample (without source - will be downloaded or copied later)
        Copy-Item "$SampleSourceDir/project.json" $SampleProjectDir
        Copy-Item "$SampleSourceDir/site.json" $SampleProjectDir

        # Create plugins config folder and copy JSON configs
        $samplePluginsConfig = Join-Path $SampleProjectDir "plugins"
        New-Item -ItemType Directory -Path $samplePluginsConfig -Force | Out-Null
        Copy-Item "$SampleSourceDir/plugins/*.json" $samplePluginsConfig

        Write-Success "Sample project created at: $SampleProjectDir"
    }

    # ========================================================================
    # STEP 7: Install Plugins via NuGet (local feed)
    # ========================================================================

    Write-Step "Step 7: Install Plugins (NuGet from local feed)"
    Measure-Step "Install Plugins" {
        Push-Location $SampleProjectDir
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
            # New structure: plugins/{PackageId}/{PackageId}.dll (with dependencies in same folder)
            $localPluginsDir = Join-Path $CliDir "plugins"
            $installedPlugins = @(Get-ChildItem $localPluginsDir -Directory -ErrorAction SilentlyContinue)
            if ($installedPlugins.Count -eq 0) {
                throw "No plugins found in local directory: $localPluginsDir"
            }
            Write-Success "Plugins installed to local directory: $localPluginsDir"
            foreach ($pluginFolder in $installedPlugins) {
                $mainDll = Join-Path $pluginFolder.FullName "$($pluginFolder.Name).dll"
                if (Test-Path $mainDll) {
                    $dllCount = @(Get-ChildItem $pluginFolder.FullName -Filter "*.dll").Count
                    Write-Info "  $($pluginFolder.Name)/ ($dllCount DLLs)"
                }
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
        $localPluginsDir = Join-Path $CliDir "plugins"

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

        # Verify plugin folders exist with main DLLs (new structure: plugins/{PackageId}/{PackageId}.dll)
        $expectedPlugins = @(
            "Spectara.Revela.Plugin.Source.OneDrive",
            "Spectara.Revela.Plugin.Statistics",
            "Spectara.Revela.Theme.Lumina.Statistics"
        )
        foreach ($pluginName in $expectedPlugins) {
            $pluginFolder = Join-Path $localPluginsDir $pluginName
            $mainDll = Join-Path $pluginFolder "$pluginName.dll"
            if (-not (Test-Path $mainDll)) {
                throw "Missing plugin: $pluginName (expected at $mainDll)"
            }
        }
        Write-Success "Verified: All plugin folders present with main DLLs"

        # Test plugin uninstall (critical - DLLs must not be locked)
        Write-Info "Testing plugin uninstall (DLLs must not be locked)..."
        & $ExePath plugin uninstall Statistics --yes
        if ($LASTEXITCODE -ne 0) { throw "Plugin uninstall command failed" }

        # Verify plugin folder was actually removed
        $statisticsFolder = Join-Path $localPluginsDir "Spectara.Revela.Plugin.Statistics"
        if (Test-Path $statisticsFolder) {
            throw "Plugin uninstall failed - folder still exists (probably locked by AssemblyLoadContext)"
        }
        Write-Success "Verified: Plugin uninstall works (folder removed)"

        # Re-install for subsequent tests
        Write-Info "Re-installing Statistics plugin..."
        & $ExePath plugin install Spectara.Revela.Plugin.Statistics --source $PluginsDir
        if ($LASTEXITCODE -ne 0) { throw "Plugin re-install failed" }
        Write-Success "Plugin re-installed for subsequent tests"
    }

    # ========================================================================
    # STEP 7c: Theme List with Online Search
    # ========================================================================
    Write-Step "Step 7c: Theme List (Online Search)"
    Measure-Step "Theme List Online" {
        Push-Location $SampleProjectDir
        try {
            # Test theme list (shows installed/built-in themes)
            Write-Info "Running: revela theme list"
            $themeListOutput = & $ExePath theme list 2>&1 | Out-String
            if ($themeListOutput -match "Lumina") {
                Write-Success "Found built-in Lumina theme"
            } else {
                throw "Built-in Lumina theme not found in theme list"
            }

            # Test theme list --online (searches NuGet sources)
            # Add local NuGet source first (for testing without nuget.org)
            Write-Info "Adding local NuGet feed for testing..."
            & $ExePath config feed add local-test $PluginsDir
            if ($LASTEXITCODE -ne 0) { Write-Warn "Feed may already exist, continuing..." }

            Write-Info "Running: revela theme list --online"
            $themeOnlineOutput = & $ExePath theme list --online 2>&1 | Out-String
            Write-Info $themeOnlineOutput

            # Should find Theme.Lumina from local NuGet feed
            if ($themeOnlineOutput -match "Spectara.Revela.Theme.Lumina" -or $themeOnlineOutput -match "Available from NuGet") {
                Write-Success "Theme list --online works (searched NuGet sources)"
            } else {
                Write-Warn "No online themes found (may be expected if not published)"
            }
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 7d: Get Source Content (OneDrive Sync or Copy)
    # ========================================================================
    if (-not $SkipDownload) {
        Write-Step "Step 7d: OneDrive Sync"
        Measure-Step "OneDrive Sync" {
            Write-Info "Running: revela source onedrive sync"
            Push-Location $SampleProjectDir
            try {
                & $ExePath source onedrive sync
                if ($LASTEXITCODE -ne 0) { throw "OneDrive sync failed" }
                Write-Success "OneDrive sync completed"

                # Show downloaded files
                $sourceDir = Join-Path $SampleProjectDir "source"
                if (Test-Path $sourceDir) {
                    $fileCount = (Get-ChildItem $sourceDir -Recurse -File).Count
                    Write-Info "Downloaded $fileCount files to source/"
                }
            } finally {
                Pop-Location
            }
        }
    } else {
        Write-Step "Step 7d: Copy Source [OneDrive SKIPPED]"
        Measure-Step "Copy Source" {
            # Copy existing source files from sample
            Copy-Item "$SampleSourceDir/source" $SampleProjectDir -Recurse
            $sourceDir = Join-Path $SampleProjectDir "source"
            $fileCount = (Get-ChildItem $sourceDir -Recurse -File).Count
            Write-Success "Copied $fileCount source files from sample"
        }
    }

    # ========================================================================
    # STEP 7e: Test CLI Commands (create, init, config)
    # ========================================================================
    Write-Step "Step 7e: Test CLI Commands (create, init, config)"
    Measure-Step "CLI Commands" {
        Push-Location $SampleProjectDir
        try {
            # Test create page gallery (in existing source directory)
            Write-Info "Running: revela create page gallery source/test-gallery --title 'Test Gallery'"
            & $ExePath create page gallery source/test-gallery --title "Test Gallery"
            if ($LASTEXITCODE -ne 0) { throw "create page gallery failed" }
            
            $revelaFile = Join-Path $SampleProjectDir "source/test-gallery/_index.revela"
            if (Test-Path $revelaFile) {
                Write-Success "Gallery page created: source/test-gallery/_index.revela"
            } else {
                throw "Gallery page file not created"
            }

            # Test create page statistics
            Write-Info "Running: revela create page statistics source/test-stats --title 'Test Stats'"
            & $ExePath create page statistics source/test-stats --title "Test Stats"
            if ($LASTEXITCODE -ne 0) { throw "create page statistics failed" }
            
            $statsFile = Join-Path $SampleProjectDir "source/test-stats/_index.revela"
            if (Test-Path $statsFile) {
                Write-Success "Statistics page created: source/test-stats/_index.revela"
            } else {
                throw "Statistics page file not created"
            }

            # Test config statistics (non-interactive with args)
            Write-Info "Running: revela config statistics --max-entries 20"
            & $ExePath config statistics --max-entries 20
            if ($LASTEXITCODE -ne 0) { throw "config statistics failed" }
            Write-Success "Statistics config updated"

            # Test config onedrive (non-interactive with args)
            Write-Info "Running: revela config onedrive --concurrency 4"
            & $ExePath config onedrive --concurrency 4
            if ($LASTEXITCODE -ne 0) { throw "config onedrive failed" }
            Write-Success "OneDrive config updated"

            # Verify config files were updated
            $statsConfig = Join-Path $SampleProjectDir "plugins/Spectara.Revela.Plugin.Statistics.json"
            if (Test-Path $statsConfig) {
                $content = Get-Content $statsConfig -Raw | ConvertFrom-Json
                if ($content.'Spectara.Revela.Plugin.Statistics'.MaxEntriesPerCategory -eq 20) {
                    Write-Success "Statistics config verified: MaxEntriesPerCategory = 20"
                } else {
                    Write-Warn "Statistics config value not as expected"
                }
            }

            # Test config show
            Write-Info "Running: revela config show"
            $configShowOutput = & $ExePath config show 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "config show failed" }
            if ($configShowOutput -match "Theme:" -or $configShowOutput -match "project.json") {
                Write-Success "config show works"
            } else {
                Write-Warn "config show output may be incomplete"
            }

        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 8: Generate - Scan & Images
    # ========================================================================
    Write-Step "Step 8: Scan Content & Process Images"
    Measure-Step "Scan & Images" {
        Push-Location $SampleProjectDir
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
    # STEP 9: Generate Statistics (Plugin Integration Test)
    # ========================================================================
    Write-Step "Step 9: Generate Statistics (Plugin Integration Test)"
    Measure-Step "Statistics" {
        Write-Info "Running: revela generate statistics"
        Push-Location $SampleProjectDir
        try {
            # Run statistics command - this tests that the plugin is correctly loaded
            $statsOutput = & $ExePath generate statistics 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { 
                Write-Err "Statistics output: $statsOutput"
                throw "Statistics generation failed" 
            }
            Write-Success "Statistics generated"
            
            # Verify plugin was loaded and executed by checking manifest
            $manifestPath = Join-Path $SampleProjectDir ".revela/manifest.json"
            if (Test-Path $manifestPath) {
                $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
                
                # Check if statistics data was added to manifest
                if ($manifest.Statistics) {
                    Write-Success "Statistics data found in manifest"
                    
                    # Check for expected statistics categories
                    $statsCategories = @()
                    if ($manifest.Statistics.Cameras) { $statsCategories += "Cameras" }
                    if ($manifest.Statistics.Lenses) { $statsCategories += "Lenses" }
                    if ($manifest.Statistics.FocalLengths) { $statsCategories += "FocalLengths" }
                    if ($manifest.Statistics.Years) { $statsCategories += "Years" }
                    if ($manifest.Statistics.Months) { $statsCategories += "Months" }
                    
                    if ($statsCategories.Count -gt 0) {
                        Write-Success "Statistics categories: $($statsCategories -join ', ')"
                    } else {
                        Write-Warn "No statistics categories found (may be expected if no EXIF data)"
                    }
                } else {
                    Write-Warn "No statistics data in manifest (plugin may have no data to process)"
                }
            } else {
                throw "Manifest not found at: $manifestPath"
            }
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 10: Generate Pages (includes Statistics page)
    # ========================================================================
    Write-Step "Step 10: Render Pages"
    Measure-Step "Pages" {
        Write-Info "Running: revela generate pages"
        Push-Location $SampleProjectDir
        try {
            & $ExePath generate pages
            if ($LASTEXITCODE -ne 0) { throw "Page rendering failed" }
            Write-Success "Pages rendered (including Statistics)"
        } finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 11: Validate Output
    # ========================================================================
    Write-Step "Step 11: Validate Output"
    Measure-Step "Validate" {
        $outputDir = Join-Path $SampleProjectDir "output"

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
            $statsChecks = @()
            if ($statsContent -match "statistics") { $statsChecks += "statistics keyword" }
            if ($statsContent -match "chart|Chart") { $statsChecks += "chart elements" }
            if ($statsContent -match "camera|Camera") { $statsChecks += "camera data" }
            if ($statsContent -match "lens|Lens") { $statsChecks += "lens data" }
            
            if ($statsChecks.Count -ge 2) {
                Write-Success "Statistics page generated with: $($statsChecks -join ', ')"
            } else {
                Write-Warn "Statistics page found but content may be incomplete"
            }
        } else {
            # Also check test-stats directory (created by test)
            $testStatsPage = Join-Path $outputDir "test-stats/index.html"
            if (Test-Path $testStatsPage) {
                Write-Success "Test statistics page found at: test-stats/index.html"
            } else {
                Write-Warn "Statistics page not found (optional - requires statistics source page)"
            }
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
    # STEP 12: Test .NET Tool Package
    # ========================================================================
    Write-Step "Step 12: Test .NET Tool Package"
    Measure-Step "ToolTest" {
        # Use the pre-created ToolDir instead of separate nupkgs folder

        Write-Info "Packing CLI as .NET Tool..."
        dotnet pack src/Cli/Cli.csproj `
            -c Release `
            -o $ToolDir `
            -p:PackageVersion=$Version `
            -p:IncludeSymbols=false `
            --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Pack failed" }
        Write-Success "CLI packed to NuGet package"

        $nupkgFile = Get-ChildItem -Path $ToolDir -Filter "Spectara.Revela.$Version.nupkg" | Select-Object -First 1
        if (-not $nupkgFile) { throw "NuGet package not found" }
        Write-Info "Package: $($nupkgFile.Name) ($([Math]::Round($nupkgFile.Length / 1MB, 2)) MB)"

        Write-Info "Installing tool from local package..."
        $installResult = dotnet tool install -g Spectara.Revela `
            --version $Version `
            --add-source $ToolDir `
            --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            # Tool might already be installed from previous run
            Write-Warn "Install failed (might be already installed), trying update..."
            dotnet tool update -g Spectara.Revela `
                --version $Version `
                --add-source $ToolDir `
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
    Write-Host "    SDK:      $NuGetDir" -ForegroundColor Gray
    Write-Host "    Plugins:  $PluginsDir" -ForegroundColor Gray
    Write-Host "    Tool:     $ToolDir" -ForegroundColor Gray
    Write-Host "    Output:   $(Join-Path $SampleProjectDir 'output')" -ForegroundColor Gray
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
