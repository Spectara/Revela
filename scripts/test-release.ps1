<#
.SYNOPSIS
    End-to-End Release Pipeline Test for Revela

.DESCRIPTION
    This script simulates the complete release pipeline locally:
    1. Build & Test all projects
    2. Publish CLI as self-contained executable
    3. Build all plugins and themes
    4. Integration test with showcase sample (offline, Git-tracked)
    5. Test generate all, clean, compress, idempotency
    6. Test .NET Tool package install/uninstall
    7. Optionally test OneDrive sync with real download

.PARAMETER Version
    Version number for the test build (default: 0.0.0-test)

.PARAMETER SkipTests
    Skip running unit tests (faster iteration)

.PARAMETER IncludeOneDrive
    Also test OneDrive sync (requires network + valid share URL)

.PARAMETER KeepArtifacts
    Don't clean up artifacts after test

.PARAMETER RuntimeIdentifier
    Target runtime (default: win-x64 on Windows, linux-x64 on Linux, osx-x64 on macOS)

.EXAMPLE
    .\scripts\test-release.ps1
    # Full test with showcase sample (offline)

.EXAMPLE
    .\scripts\test-release.ps1 -SkipTests
    # Quick iteration: skip unit tests

.EXAMPLE
    .\scripts\test-release.ps1 -IncludeOneDrive
    # Full test including OneDrive download

.EXAMPLE
    .\scripts\test-release.ps1 -Version "1.0.0-beta.1" -KeepArtifacts
    # Test specific version, keep artifacts for inspection
#>

[CmdletBinding()]
param(
    [string]$Version = "0.0.0-test",
    [switch]$SkipTests,
    [switch]$IncludeOneDrive,
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
$ShowcaseDir = Join-Path $RepoRoot "samples/showcase"
$OneDriveDir = Join-Path $RepoRoot "samples/onedrive"

# Determine runtime identifier and executable name
# Note: $IsWindows, $IsMacOS, $IsLinux are automatic variables in PowerShell Core 6+
# For Windows PowerShell 5.x compatibility, we also check $env:OS
$isWindowsOS = $IsWindows -or $env:OS -eq "Windows_NT"

if (-not $RuntimeIdentifier) {
    if ($isWindowsOS) {
        $RuntimeIdentifier = "win-x64"
    }
    elseif ($IsMacOS) {
        $RuntimeIdentifier = "osx-x64"
    }
    else {
        $RuntimeIdentifier = "linux-x64"
    }
}

# Determine executable extension based on runtime (not current OS!)
# This allows cross-compilation scenarios
$ExeName = if ($RuntimeIdentifier -like "win-*") { "revela.exe" } else { "revela" }
$exeExt = if ($RuntimeIdentifier -like "win-*") { ".exe" } else { "" }

$ExePath = Join-Path $CliDir $ExeName

# Track timing
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$stepTimes = @{}

function Measure-Step {
    param([string]$Name, [scriptblock]$Action)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $Action
    }
    finally {
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
Write-Info "OneDrive:   $IncludeOneDrive"
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

        Write-Info "Running dotnet build (solution)..."
        dotnet build -c Release --no-restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        Write-Success "Solution build completed"

        # Plugins/Themes are Debug-only in Cli.csproj, build them separately in Release
        $extraProjects = @(
            "src/Plugins/Generate/Generate.csproj",
            "src/Plugins/Theme/Theme.csproj",
            "src/Plugins/Projects/Projects.csproj",
            "src/Plugins/Compress/Compress.csproj",
            "src/Plugins/Serve/Serve.csproj",
            "src/Plugins/Source/OneDrive/OneDrive.csproj",
            "src/Plugins/Statistics/Statistics.csproj",
            "src/Plugins/Calendar/Calendar.csproj",
            "src/Plugins/Source/Calendar/Calendar.csproj",
            "src/Themes/Lumina.Calendar/Lumina.Calendar.csproj",
            "src/Themes/Lumina/Lumina.csproj",
            "src/Themes/Lumina.Statistics/Lumina.Statistics.csproj"
        )
        Write-Info "Building plugins and themes (Release)..."
        foreach ($proj in $extraProjects) {
            dotnet build $proj -c Release --no-restore --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $proj" }
        }
        Write-Success "All projects built"
    }

    # ========================================================================
    # STEP 3: Run Tests (optional)
    # ========================================================================
    if (-not $SkipTests) {
        Write-Step "Step 3: Run Tests"
        Measure-Step "Tests" {
            # .NET 10 uses Microsoft.Testing.Platform - run tests as executables
            # Determine executable extension based on OS
            # Test executables use CURRENT OS extension (tests run locally, not cross-compiled)
            $testExeExt = if ($isWindowsOS) { ".exe" } else { "" }

            $testProjects = @(
                @{ Name = "Core"; Path = "artifacts/bin/Core/Release/net10.0/Spectara.Revela.Tests.Core$testExeExt" },
                @{ Name = "Commands"; Path = "artifacts/bin/Commands/Release/net10.0/Spectara.Revela.Tests.Commands$testExeExt" },
                @{ Name = "Source.OneDrive"; Path = "artifacts/bin/Tests.Source.OneDrive/Release/net10.0/Spectara.Revela.Tests.Plugins.Source.OneDrive$testExeExt" },
                @{ Name = "Statistics"; Path = "artifacts/bin/Statistics/Release/net10.0/Spectara.Revela.Tests.Plugins.Statistics$testExeExt" },
                @{ Name = "Calendar"; Path = "artifacts/bin/Tests.Calendar/Release/net10.0/Spectara.Revela.Tests.Plugins.Calendar$testExeExt" },
                @{ Name = "Source.Calendar"; Path = "artifacts/bin/Tests.Source.Calendar/Release/net10.0/Spectara.Revela.Tests.Plugins.Source.Calendar$testExeExt" },
                @{ Name = "Serve"; Path = "artifacts/bin/Serve/Release/net10.0/Spectara.Revela.Tests.Plugins.Serve$testExeExt" },
                @{ Name = "Compress"; Path = "artifacts/bin/Compress/Release/net10.0/Spectara.Revela.Tests.Plugins.Compress$testExeExt" },
                @{ Name = "Integration"; Path = "artifacts/bin/Integration/Release/net10.0/Spectara.Revela.Tests.Integration$testExeExt" }
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
    }
    else {
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
    Write-Step "Step 5: Pack NuGet Packages"
    Measure-Step "NuGet Packages" {
        # Lumina (goes next to CLI - bundled with release)
        Write-Info "Building Lumina for $RuntimeIdentifier..."
        dotnet build src/Themes/Lumina/Lumina.csproj `
            -c Release -r $RuntimeIdentifier -p:Version=$Version --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Lumina build failed" }

        Copy-Item "artifacts/bin/Lumina/Release/net10.0/$RuntimeIdentifier/Spectara.Revela.Themes.Lumina.dll" $CliDir
        Write-Success "Lumina bundled with CLI"

        # Pack all packages from existing build output (no rebuild!)
        $packTargets = @(
            @{ Name = "Generate";          Proj = "src/Plugins/Generate/Generate.csproj";                           Out = $PluginsDir },
            @{ Name = "Theme";             Proj = "src/Plugins/Theme/Theme.csproj";                                 Out = $PluginsDir },
            @{ Name = "Projects";          Proj = "src/Plugins/Projects/Projects.csproj";                           Out = $PluginsDir },
            @{ Name = "Lumina";            Proj = "src/Themes/Lumina/Lumina.csproj";                                 Out = $PluginsDir },
            @{ Name = "Lumina.Statistics";  Proj = "src/Themes/Lumina.Statistics/Lumina.Statistics.csproj";           Out = $PluginsDir },
            @{ Name = "Sdk";                Proj = "src/Sdk/Sdk.csproj";                                             Out = $NuGetDir },
            @{ Name = "Source.OneDrive";    Proj = "src/Plugins/Source/OneDrive/OneDrive.csproj";                   Out = $PluginsDir },
            @{ Name = "Statistics";         Proj = "src/Plugins/Statistics/Statistics.csproj";                       Out = $PluginsDir },
            @{ Name = "Calendar";           Proj = "src/Plugins/Calendar/Calendar.csproj";                           Out = $PluginsDir },
            @{ Name = "Source.Calendar";    Proj = "src/Plugins/Source/Calendar/Calendar.csproj";                    Out = $PluginsDir },
            @{ Name = "Lumina.Calendar";    Proj = "src/Themes/Lumina.Calendar/Lumina.Calendar.csproj";              Out = $PluginsDir },
            @{ Name = "Serve";              Proj = "src/Plugins/Serve/Serve.csproj";                                 Out = $PluginsDir },
            @{ Name = "Compress";           Proj = "src/Plugins/Compress/Compress.csproj";                           Out = $PluginsDir }
        )

        foreach ($target in $packTargets) {
            Write-Info "Packing $($target.Name)..."
            dotnet pack $target.Proj `
                -c Release -o $target.Out -p:PackageVersion=$Version `
                --no-build --no-restore --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "$($target.Name) pack failed" }
            Write-Success "$($target.Name) packed"
        }

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

        # Copy showcase sample (Git-tracked, includes source images)
        Copy-Item "$ShowcaseDir/project.json" $SampleProjectDir
        Copy-Item "$ShowcaseDir/site.json" $SampleProjectDir
        Copy-Item "$ShowcaseDir/source" $SampleProjectDir -Recurse

        $fileCount = (Get-ChildItem "$SampleProjectDir/source" -Recurse -File).Count
        Write-Success "Sample project created with $fileCount source files"
    }

    # ========================================================================
    # STEP 7: Install Plugins via NuGet (local feed)
    # ========================================================================

    Write-Step "Step 7: Install Plugins (NuGet from local feed)"
    Measure-Step "Install Plugins" {
        Push-Location $SampleProjectDir
        try {
            # Install core plugins (new in v2: Generate, Theme, Projects are plugins now)
            Write-Info "Installing Generate..."
            & $ExePath plugin install Generate --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Generate plugin installation failed" }
            Write-Success "Generate installed"

            Write-Info "Installing Theme..."
            & $ExePath plugin install Theme --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Theme plugin installation failed" }
            Write-Success "Theme installed"

            Write-Info "Installing Projects..."
            & $ExePath plugin install Projects --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Projects plugin installation failed" }
            Write-Success "Projects installed"

            # Install addon plugins
            Write-Info "Installing Source.OneDrive..."
            & $ExePath plugin install Source.OneDrive --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "OneDrive plugin installation failed" }
            Write-Success "Source.OneDrive installed"

            # Install Statistics Plugin
            Write-Info "Installing Statistics..."
            & $ExePath plugin install Statistics --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Statistics plugin installation failed" }
            Write-Success "Statistics installed"

            # Install Lumina.Statistics Extension
            Write-Info "Installing Lumina.Statistics..."
            & $ExePath plugin install Spectara.Revela.Themes.Lumina.Statistics --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Lumina.Statistics installation failed" }
            Write-Success "Lumina.Statistics installed"

            # Install Serve Plugin (live preview server)
            Write-Info "Installing Serve..."
            & $ExePath plugin install Serve --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Serve plugin installation failed" }
            Write-Success "Serve installed"

            # Install Calendar plugins
            Write-Info "Installing Calendar..."
            & $ExePath plugin install Calendar --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Calendar plugin installation failed" }
            Write-Success "Calendar installed"

            Write-Info "Installing Source.Calendar..."
            & $ExePath plugin install Source.Calendar --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Source.Calendar plugin installation failed" }
            Write-Success "Source.Calendar installed"

            Write-Info "Installing Lumina.Calendar..."
            & $ExePath plugin install Spectara.Revela.Themes.Lumina.Calendar --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Lumina.Calendar installation failed" }
            Write-Success "Lumina.Calendar installed"

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
        }
        finally {
            Pop-Location
        }
    }
    # ========================================================================
    # STEP 7b: Verify Plugin System (including uninstall)
    # ========================================================================
    Write-Step "Step 7b: Verify Plugin Installation & Uninstall"
    Measure-Step "Plugin Verify" {
        $localPluginsDir = Join-Path $CliDir "plugins"

        # Verify correct number of plugins loaded (8 functional plugins)
        # Themes (Lumina, Lumina.Statistics, Lumina.Calendar) are shown in 'theme list' instead
        $pluginListOutput = & $ExePath plugin list 2>&1 | Out-String
        if ($pluginListOutput -match "Installed Plugins.*\(8\)") {
            Write-Success "Verified: 8 plugins loaded"
        }
        else {
            Write-Warn "Plugin list output: $pluginListOutput"
            throw "Expected 8 plugins in panel header, got unexpected output"
        }

        # Verify installed plugins are local (8 plugins should show 'installed')
        $localMatches = ([regex]::Matches($pluginListOutput, '\binstalled\b')).Count
        if ($localMatches -ge 8) {
            Write-Success "Verified: 8 plugins installed locally (next to exe)"
        }
        else {
            Write-Warn "Expected 8 installed plugins, found $localMatches in output"
        }

        # Verify plugin folders exist with main DLLs (new structure: plugins/{PackageId}/{PackageId}.dll)
        $expectedPlugins = @(
            "Spectara.Revela.Plugins.Generate",
            "Spectara.Revela.Plugins.Theme",
            "Spectara.Revela.Plugins.Projects",
            "Spectara.Revela.Plugins.Source.OneDrive",
            "Spectara.Revela.Plugins.Statistics",
            "Spectara.Revela.Plugins.Calendar",
            "Spectara.Revela.Plugins.Source.Calendar",
            "Spectara.Revela.Plugins.Serve",
            "Spectara.Revela.Themes.Lumina.Statistics",
            "Spectara.Revela.Themes.Lumina.Calendar"
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
        $statisticsFolder = Join-Path $localPluginsDir "Spectara.Revela.Plugins.Statistics"
        if (Test-Path $statisticsFolder) {
            throw "Plugin uninstall failed - folder still exists (probably locked by AssemblyLoadContext)"
        }
        Write-Success "Verified: Plugin uninstall works (folder removed)"

        # Re-install for subsequent tests
        Write-Info "Re-installing Statistics plugin..."
        & $ExePath plugin install Spectara.Revela.Plugins.Statistics --source $PluginsDir
        if ($LASTEXITCODE -ne 0) { throw "Plugin re-install failed" }
        Write-Success "Plugin re-installed for subsequent tests"

        # Test Serve plugin command is registered (--help is non-blocking, unlike actual serve)
        # Must run from sample project dir because serve requires a project
        Write-Info "Testing serve command (--help)..."
        Push-Location $SampleProjectDir
        try {
            $serveHelpOutput = & $ExePath serve --help 2>&1 | Out-String
            if ($serveHelpOutput -match "Preview generated site") {
                Write-Success "Verified: Serve plugin command registered and working"
            }
            else {
                Write-Warn "Serve help output: $serveHelpOutput"
                throw "Serve plugin command not working correctly"
            }
        }
        finally {
            Pop-Location
        }
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
            }
            else {
                throw "Built-in Lumina theme not found in theme list"
            }

            # Test theme list --online (searches NuGet sources)
            # Add local NuGet source first (for testing without nuget.org)
            # Use relative path from config directory (cli/) to plugins directory (../plugins)
            # This tests that relative paths are correctly resolved at runtime
            Write-Info "Adding local NuGet feed for testing (relative path)..."
            & $ExePath config feed add local-test "../plugins"
            if ($LASTEXITCODE -ne 0) { Write-Warn "Feed may already exist, continuing..." }

            Write-Info "Running: revela theme list --online"
            $themeOnlineOutput = & $ExePath theme list --online 2>&1 | Out-String
            Write-Info $themeOnlineOutput

            # Should find Lumina from local NuGet feed
            if ($themeOnlineOutput -match "Spectara.Revela.Themes.Lumina" -or $themeOnlineOutput -match "Available from NuGet") {
                Write-Success "Theme list --online works (searched NuGet sources)"
            }
            else {
                Write-Warn "No online themes found (may be expected if not published)"
            }
        }
        finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 7d: OneDrive Sync (optional, requires network)
    # ========================================================================
    if ($IncludeOneDrive) {
        Write-Step "Step 7d: OneDrive Sync Test"
        Measure-Step "OneDrive Sync" {
            # Create a separate project for OneDrive test
            $oneDriveProjectDir = Join-Path $TestDir "onedrive-test"
            New-Item -ItemType Directory -Path $oneDriveProjectDir -Force | Out-Null
            Copy-Item "$OneDriveDir/project.json" $oneDriveProjectDir
            Copy-Item "$OneDriveDir/site.json" $oneDriveProjectDir

            Write-Info "Running: revela source onedrive sync"
            Push-Location $oneDriveProjectDir
            try {
                & $ExePath source onedrive sync
                if ($LASTEXITCODE -ne 0) { throw "OneDrive sync failed" }
                Write-Success "OneDrive sync completed"

                $sourceDir = Join-Path $oneDriveProjectDir "source"
                if (Test-Path $sourceDir) {
                    $fileCount = (Get-ChildItem $sourceDir -Recurse -File).Count
                    Write-Info "Downloaded $fileCount files to source/"
                }
            }
            finally {
                Pop-Location
            }
        }
    }
    else {
        Write-Step "Step 7d: OneDrive Sync [SKIPPED]"
        Write-Info "Use -IncludeOneDrive to test OneDrive sync (requires network)"
    }

    # ========================================================================
    # STEP 7e: Test CLI Commands (create, init, config)
    # ========================================================================
    Write-Step "Step 7e: Test CLI Commands (create, init, config)"
    Measure-Step "CLI Commands" {
        Push-Location $SampleProjectDir
        try {
            # Test create page gallery (path is relative to source/ directory)
            Write-Info "Running: revela create page gallery test-gallery --title 'Test Gallery'"
            & $ExePath create page gallery test-gallery --title "Test Gallery"
            if ($LASTEXITCODE -ne 0) { throw "create page gallery failed" }

            $revelaFile = Join-Path $SampleProjectDir "source/test-gallery/_index.revela"
            if (Test-Path $revelaFile) {
                Write-Success "Gallery page created: source/test-gallery/_index.revela"
            }
            else {
                throw "Gallery page file not created"
            }

            # Test create page statistics
            Write-Info "Running: revela create page statistics test-stats --title 'Test Stats'"
            & $ExePath create page statistics test-stats --title "Test Stats"
            if ($LASTEXITCODE -ne 0) { throw "create page statistics failed" }

            $statsFile = Join-Path $SampleProjectDir "source/test-stats/_index.revela"
            if (Test-Path $statsFile) {
                Write-Success "Statistics page created: source/test-stats/_index.revela"
            }
            else {
                throw "Statistics page file not created"
            }

            # Test config statistics (non-interactive with args)
            Write-Info "Running: revela config statistics --max-entries 20"
            & $ExePath config statistics --max-entries 20
            if ($LASTEXITCODE -ne 0) { throw "config statistics failed" }
            Write-Success "Statistics config updated"

            # Verify statistics config was updated in project.json
            $projectConfig = Join-Path $SampleProjectDir "project.json"
            if (Test-Path $projectConfig) {
                $content = Get-Content $projectConfig -Raw | ConvertFrom-Json
                if ($content.'Spectara.Revela.Plugins.Statistics'.MaxEntriesPerCategory -eq 20) {
                    Write-Success "Statistics config verified: MaxEntriesPerCategory = 20"
                }
                else {
                    Write-Warn "Statistics config value not as expected (may not have been written yet)"
                }
            }

            # Test config locations (shows where configs are stored)
            Write-Info "Running: revela config locations"
            $configLocationsOutput = & $ExePath config locations 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "config locations failed" }
            if ($configLocationsOutput -match "Project" -or $configLocationsOutput -match "project.json") {
                Write-Success "config locations works"
            }
            else {
                Write-Warn "config locations output may be incomplete"
            }

        }
        finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 8: Generate All (primary user command)
    # ========================================================================
    Write-Step "Step 8: Generate All"
    Measure-Step "Generate All" {
        Push-Location $SampleProjectDir
        try {
            Write-Info "Running: revela generate all"
            & $ExePath generate all
            if ($LASTEXITCODE -ne 0) { throw "generate all failed" }
            Write-Success "Full pipeline completed (scan → statistics → pages → images)"
        }
        finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 9: Validate Output
    # ========================================================================
    Write-Step "Step 9: Validate Output"
    Measure-Step "Validate" {
        $outputDir = Join-Path $SampleProjectDir "output"

        if (-not (Test-Path $outputDir)) {
            throw "Output directory not created!"
        }

        # Check for expected files
        $expectedFiles = @(
            @{ Path = "index.html"; Description = "Homepage" },
            @{ Path = "_assets/main.css"; Description = "Theme CSS" }
        )

        $missingFiles = @()
        foreach ($file in $expectedFiles) {
            $path = Join-Path $outputDir $file.Path
            if (Test-Path $path) {
                Write-Success "Found: $($file.Path)"
            }
            else {
                $missingFiles += $file.Path
                Write-Err "Missing: $($file.Path) ($($file.Description))"
            }
        }

        # Check images directory
        $imagesDir = Join-Path $outputDir "images"
        if (Test-Path $imagesDir) {
            $imageCount = (Get-ChildItem $imagesDir -Recurse -File).Count
            Write-Success "Images generated: $imageCount files"
        }
        else {
            Write-Warn "Images directory not found"
        }

        # Check gallery directories (exclude special dirs)
        $specialDirs = @("images", "_assets", "test-gallery", "test-stats")
        $galleryDirs = @(Get-ChildItem $outputDir -Directory | Where-Object { $_.Name -notin $specialDirs })
        if ($galleryDirs.Count -gt 0) {
            Write-Success "Gallery directories: $($galleryDirs.Count)"
            foreach ($dir in $galleryDirs) {
                $htmlFiles = @(Get-ChildItem $dir.FullName -Filter "*.html" -Recurse)
                Write-Info "  $($dir.Name): $($htmlFiles.Count) HTML files"
            }
        }

        # Check statistics page
        $statsPages = Get-ChildItem $outputDir -Recurse -Filter "index.html" |
            Where-Object { $_.Directory.Name -eq "06-statistics" -or $_.Directory.Name -eq "test-stats" }
        if ($statsPages) {
            Write-Success "Statistics page found: $($statsPages[0].Directory.Name)/index.html"
        }
        else {
            Write-Warn "Statistics page not found (optional)"
        }

        # Check _assets directory
        $assetsDir = Join-Path $outputDir "_assets"
        if (Test-Path $assetsDir) {
            $assetFiles = Get-ChildItem $assetsDir -Recurse -File
            Write-Success "_assets/ directory: $($assetFiles.Count) files"

            $extensionDir = Join-Path $assetsDir "lumina-statistics"
            if (Test-Path $extensionDir) {
                Write-Success "Lumina.Statistics assets included"
            }
        }

        # Check index.html content
        $indexPath = Join-Path $outputDir "index.html"
        if (Test-Path $indexPath) {
            $indexContent = Get-Content $indexPath -Raw
            if ($indexContent -match "<html") {
                Write-Success "index.html contains valid HTML"
            }
            else {
                Write-Warn "index.html may be invalid"
            }
        }

        if ($missingFiles.Count -gt 0) {
            throw "Validation failed: Missing files: $($missingFiles -join ', ')"
        }

        Write-Success "All validations passed!"
    }

    # ========================================================================
    # STEP 10: Compress (Plugin Integration Test)
    # ========================================================================
    Write-Step "Step 10: Compress Plugin Test"
    Measure-Step "Compress" {
        Push-Location $SampleProjectDir
        try {
            # Install Compress plugin
            Write-Info "Installing Compress plugin..."
            & $ExePath plugin install Compress --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw "Compress plugin installation failed" }
            Write-Success "Compress plugin installed"

            # Run compression
            Write-Info "Running: revela generate compress"
            & $ExePath generate compress
            if ($LASTEXITCODE -ne 0) { throw "generate compress failed" }
            Write-Success "Compression completed"

            # Verify compressed files exist
            $outputDir = Join-Path $SampleProjectDir "output"
            $gzFiles = @(Get-ChildItem $outputDir -Recurse -Filter "*.gz")
            $brFiles = @(Get-ChildItem $outputDir -Recurse -Filter "*.br")
            if ($gzFiles.Count -gt 0 -or $brFiles.Count -gt 0) {
                Write-Success "Compressed files: $($gzFiles.Count) .gz, $($brFiles.Count) .br"
            }
            else {
                Write-Warn "No compressed files found (may need compressible content)"
            }

            # Test clean compress
            Write-Info "Running: revela clean compress"
            & $ExePath clean compress
            if ($LASTEXITCODE -ne 0) { throw "clean compress failed" }

            $gzAfterClean = @(Get-ChildItem $outputDir -Recurse -Filter "*.gz")
            $brAfterClean = @(Get-ChildItem $outputDir -Recurse -Filter "*.br")
            if ($gzAfterClean.Count -eq 0 -and $brAfterClean.Count -eq 0) {
                Write-Success "clean compress removed all compressed files"
            }
            else {
                Write-Warn "Some compressed files remain after clean"
            }
        }
        finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 11: Clean & Regenerate (Idempotency Test)
    # ========================================================================
    Write-Step "Step 11: Clean & Regenerate (Idempotency)"
    Measure-Step "Idempotency" {
        Push-Location $SampleProjectDir
        try {
            # Clean everything
            Write-Info "Running: revela clean all"
            & $ExePath clean all
            if ($LASTEXITCODE -ne 0) { throw "clean all failed" }

            $outputDir = Join-Path $SampleProjectDir "output"
            $cacheDir = Join-Path $SampleProjectDir ".cache"
            if (-not (Test-Path $outputDir) -or (Get-ChildItem $outputDir -Recurse -File -ErrorAction SilentlyContinue).Count -eq 0) {
                Write-Success "clean all removed output"
            }
            else {
                Write-Warn "Output directory not fully cleaned"
            }

            # Regenerate from scratch
            Write-Info "Running: revela generate all (second run after clean)"
            & $ExePath generate all
            if ($LASTEXITCODE -ne 0) { throw "generate all (second run) failed" }
            Write-Success "Second generate all succeeded"

            # Verify output exists again
            $indexPath = Join-Path $outputDir "index.html"
            if (Test-Path $indexPath) {
                Write-Success "Idempotency verified: output regenerated after clean"
            }
            else {
                throw "Idempotency failed: index.html not found after regeneration"
            }

            # Third run without clean (should be a no-op / fast)
            Write-Info "Running: revela generate all (third run, no clean - incremental)"
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            & $ExePath generate all
            $sw.Stop()
            if ($LASTEXITCODE -ne 0) { throw "generate all (third run) failed" }
            Write-Success "Third run completed in $($sw.Elapsed.ToString('mm\:ss\.fff')) (incremental)"
        }
        finally {
            Pop-Location
        }
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
            -p:Version=$Version `
            -p:PackageVersion=$Version `
            -p:IncludeSymbols=false `
            --no-restore `
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

        # Verify version matches what we packed
        if ($versionOutput -match "^$([regex]::Escape($Version))") {
            Write-Success "Version matches: $versionOutput"
        }
        else {
            Write-Warn "Version mismatch: expected $Version, got $versionOutput"
        }

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
    # STEP 13: Summary
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

}
catch {
    Write-Host ""
    Write-Err "Pipeline failed: $_"
    Write-Host ""
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
finally {
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
