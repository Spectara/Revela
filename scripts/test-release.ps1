<#
.SYNOPSIS
    End-to-End Release Pipeline Test for Revela

.DESCRIPTION
    This script simulates the complete release pipeline locally:
    1. Build & Test all projects
    2. Publish CLI as self-contained executable
    3. Build all plugins and themes
    4. Integration test with showcase sample (offline, Git-tracked)
    5. Test plugin install/uninstall/verify, theme list/files/extract
    6. Test CLI commands (create, config, restore)
    7. Test generate all + individual pipeline steps (scan, pages, images)
    8. Test clean (all + individual), compress, idempotency
    9. Test .NET Tool package install/uninstall
    10. Optionally test OneDrive sync with real download

.PARAMETER Version
    Version number for the test build (default: 0.0.0-test)

.PARAMETER SkipTests
    Skip running unit tests (faster iteration)

.PARAMETER IncludeOneDrive
    Also test OneDrive sync (requires network + valid share URL)

.PARAMETER RuntimeIdentifier
    Target runtime (default: current OS and OS architecture; cross-runtime tests fail)

.PARAMETER ArtifactPath
    Existing first-party release directory, .zip or .tar.gz inside this repository.
    Selects Artifact mode, which never builds, restores, publishes or packs the product.

.PARAMETER Variant
    Full (default), Core or Standalone. Build mode produces Full.

.PARAMETER PackageDirectory
    Required external package feed for Core artifacts, copied before testing.

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
    .\scripts\test-release.ps1 -Version "1.0.0-beta.1"
    # Test specific version, keep artifacts for inspection
#>

[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?$')]
    [string]$Version = "0.0.0-test",
    [Parameter(ParameterSetName = 'Build')]
    [switch]$SkipTests,
    [switch]$IncludeOneDrive,
    [string]$RuntimeIdentifier,
    [Parameter(Mandatory, ParameterSetName = 'Artifact')]
    [string]$ArtifactPath,
    [ValidateSet('Full', 'Core', 'Standalone')]
    [string]$Variant = 'Full',
    [Parameter(ParameterSetName = 'Artifact')]
    [string]$PackageDirectory
)

# Strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
$Mode = $PSCmdlet.ParameterSetName
if ($Mode -eq 'Build' -and $Variant -ne 'Full') { throw 'Build mode produces Full; supply -ArtifactPath for other variants.' }
if ($Variant -eq 'Core' -and -not $PackageDirectory) { throw 'Core requires -PackageDirectory.' }
if ($PackageDirectory -and $Variant -ne 'Core') { throw 'Only Core accepts -PackageDirectory; Full must use bundled packages.' }
if ($Variant -eq 'Standalone' -and $IncludeOneDrive) { throw 'Standalone does not run the optional OneDrive suite.' }

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
$TestDir = Join-Path $RepoRoot "artifacts/release-test-$Timestamp-$([Guid]::NewGuid().ToString('N'))"
$CliDir = Join-Path $TestDir "cli"
$PluginsDir = if ($Variant -eq 'Core') { Join-Path $TestDir 'plugins' } else { Join-Path $CliDir 'packages' }
$NuGetDir = $PluginsDir
$ToolDir = Join-Path $TestDir "tool"
$ToolInstallDir = Join-Path $TestDir 'tool-install'
$ToolConfigPath = Join-Path $ToolDir 'NuGet.Config'
$ExtractDir = Join-Path $TestDir 'extracted'
$SampleProjectDir = Join-Path $TestDir "sample"
$ShowcaseDir = Join-Path $RepoRoot "samples/showcase"
$OneDriveDir = Join-Path $RepoRoot "samples/onedrive"
$RelativeFeed = [IO.Path]::GetRelativePath($CliDir, $PluginsDir)

# Determine runtime identifier and executable name
# Note: $IsWindows, $IsMacOS, $IsLinux are automatic variables in PowerShell Core 6+
# For Windows PowerShell 5.x compatibility, we also check $env:OS
$isWindowsOS = $IsWindows -or $env:OS -eq "Windows_NT"

$hostOS = if ($isWindowsOS) { 'win' } elseif ($IsMacOS) { 'osx' } else { 'linux' }
$hostArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$hostRid = "$hostOS-$hostArchitecture"
if (-not $RuntimeIdentifier) { $RuntimeIdentifier = $hostRid }
if ($RuntimeIdentifier -cne $hostRid) { throw "Cannot execute $RuntimeIdentifier artifacts on $hostRid." }

# Determine executable extension after validating the host runtime.
$ExeName = if ($RuntimeIdentifier -like "win-*") { "revela.exe" } else { "revela" }

$ExePath = Join-Path $CliDir $ExeName
$VersionPattern = '^revela ' + [regex]::Escape($Version) + ' \([^\r\n]+\)'
$VersionPattern += if ($Variant -eq 'Standalone') { ' \u2014 Standalone edition$' } else { ' \u2014 Full edition$' }

function Resolve-InputPath {
    param([string]$Path)
    $resolved = (Resolve-Path -LiteralPath $Path).ProviderPath
    $relative = [IO.Path]::GetRelativePath($RepoRoot, $resolved)
    if ($relative -eq '.' -or [IO.Path]::IsPathRooted($relative) -or $relative -match '^\.\.([/\\]|$)') {
        throw 'Artifact inputs must be strictly inside the Revela repository.'
    }
    $ancestor = Get-Item -LiteralPath $resolved -Force
    while ($ancestor.FullName -ne $RepoRoot) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked artifact inputs are not supported.' }
        $ancestor = Get-Item -LiteralPath (Split-Path -Parent $ancestor.FullName) -Force
    }
    return $resolved
}

function Get-InputHashes {
    param([string[]]$Paths)
    $hashes = @{}
    foreach ($path in $Paths) {
        $item = Get-Item -LiteralPath $path -Force
        $items = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $path -Recurse -Force) } else { @($item) }
        foreach ($entry in $items) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked input content is not supported.' }
            if (-not $entry.PSIsContainer) { $hashes[$entry.FullName] = (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash }
        }
    }
    return $hashes
}

function Assert-GeneratedOutput {
    $outputDir = Join-Path $SampleProjectDir 'output'
    $expectedPaths = @('index.html', '_assets/main.css')
    if ($Variant -ne 'Standalone') { $expectedPaths += @('test-gallery/index.html', 'test-stats/index.html', 'about/index.html') }
    else {
        # Native AOT trims Scriban's built-in functions to the ones the theme templates use, so the
        # Standalone run must render every template kind: statistics (heatmap), photo pages, 404.
        $expectedPaths += @('galleries/statistics/index.html', 'photo/landscapes/ocean-sunset/index.html', '404.html')
    }
    foreach ($relativePath in $expectedPaths) {
        $path = Join-Path $outputDir $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Missing or empty output: $relativePath"
        }
        if ($relativePath.EndsWith('.html', [StringComparison]::Ordinal) -and (Get-Content -LiteralPath $path -Raw) -notmatch '<html') {
            throw "Invalid HTML output: $relativePath"
        }
    }
    $imageFiles = @(Get-ChildItem -LiteralPath (Join-Path $outputDir 'images') -Recurse -File | Where-Object { $_.Extension -in @('.jpg', '.webp', '.avif') })
    $imageDirectories = @($imageFiles.DirectoryName | Sort-Object -Unique)
    if ($imageFiles.Count -lt 228 -or $imageDirectories.Count -lt 14 -or @($imageFiles | Where-Object Length -eq 0).Count -gt 0) {
        throw "Expected at least 14 images and 228 nonempty variants, found $($imageDirectories.Count) images / $($imageFiles.Count) variants."
    }
    Write-Success "Output verified: $($expectedPaths.Count) required assets/pages, $($imageDirectories.Count) images, $($imageFiles.Count) nonempty variants"
}

$InputPaths = @((Join-Path $ShowcaseDir 'source'), (Join-Path $ShowcaseDir 'project.json'), (Join-Path $ShowcaseDir 'site.json'))
if ($ArtifactPath) {
    $ArtifactPath = Resolve-InputPath $ArtifactPath
    $InputPaths += $ArtifactPath
    if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Container) -and $ArtifactPath -notmatch '(?i)\.(zip|tar\.gz)$') {
        throw 'ArtifactPath must be an extracted directory, .zip or .tar.gz.'
    }
}
if ($PackageDirectory) {
    $PackageDirectory = Resolve-InputPath $PackageDirectory
    if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) { throw 'PackageDirectory must be a directory.' }
    $InputPaths += $PackageDirectory
}
$InputHashes = Get-InputHashes $InputPaths
$SavedEnvironment = @{}
foreach ($name in @('HOME', 'USERPROFILE', 'APPDATA', 'LOCALAPPDATA', 'XDG_CONFIG_HOME', 'XDG_DATA_HOME', 'XDG_CACHE_HOME', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'NUGET_HTTP_CACHE_PATH', 'NUGET_PLUGINS_CACHE_PATH', 'DOTNET_BUNDLE_EXTRACT_BASE_DIR')) {
    $SavedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$PipelineFailure = $null
$TranscriptStarted = $false

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
Write-Info "Mode:       $Mode"
Write-Info "Variant:    $Variant"
Write-Info "Runtime:    $RuntimeIdentifier"
Write-Info "Skip Tests: $SkipTests"
Write-Info "OneDrive:   $IncludeOneDrive"
Write-Info "Artifacts are retained for inspection."
Write-Info "Repo Root:  $RepoRoot"

Push-Location $RepoRoot
try {
    # ========================================================================
    # STEP 1: Clean & Prepare
    # ========================================================================
    Write-Step "Step 1: Clean & Prepare"
    Measure-Step "Clean" {
        New-Item -ItemType Directory -Path $TestDir | Out-Null
        New-Item -ItemType Directory -Path $CliDir -Force | Out-Null
        New-Item -ItemType Directory -Path $ToolDir -Force | Out-Null
        foreach ($name in $SavedEnvironment.Keys) {
            $isolatedPath = Join-Path $TestDir "environment/$name"
            New-Item -ItemType Directory -Path $isolatedPath -Force | Out-Null
            [Environment]::SetEnvironmentVariable($name, $isolatedPath, 'Process')
        }
        Write-Success "Created test directories"
    }

    Start-Transcript -Path (Join-Path $TestDir 'verification.log') | Out-Null
    $TranscriptStarted = $true
    $InputHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $TestDir 'input-hashes.json') -Encoding utf8

    if ($Mode -eq 'Build') {
    # ========================================================================
    # STEP 2: Restore & Build
    # ========================================================================
    Write-Step "Step 2: Restore & Build"
    Measure-Step "Build" {
        Write-Info "Running dotnet restore..."
        dotnet restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
        Write-Success "Restore completed"

        Write-Info "Running dotnet build (solution, Release)..."
        dotnet build Spectara.Revela.slnx -c Release --no-restore -p:Version=$Version -p:DebugType=embedded --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        Write-Success "Solution built"
    }

    # ========================================================================
    # STEP 3: Run Tests (optional)
    # ========================================================================
    if (-not $SkipTests) {
        Write-Step "Step 3: Run Tests"
        Measure-Step "Tests" {
            # `--solution` is required for the new MTP-based dotnet test to
            # discover plugin test projects (without it only Core/Commands/
            # Integration are exercised — 168 plugin tests are silently skipped).
            dotnet test --solution Spectara.Revela.slnx -c Release --no-build --no-restore
            if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
            Write-Success "All tests passed"
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
            -p:DebugType=embedded `
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
        # Pack everything from the existing Release build (no rebuild). The
        # solution-wide pack respects each csproj's <IsPackable>, so exactly
        # exactly the published packages are produced — no hardcoded list to maintain.
        Write-Info "Packing all NuGet packages (solution-wide)..."
        New-Item -ItemType Directory -Path $PluginsDir | Out-Null

        dotnet pack Spectara.Revela.slnx `
            -c Release -o $PluginsDir -p:PackageVersion=$Version -p:Version=$Version -p:DebugType=embedded -p:IncludeSymbols=false `
            --no-build --no-restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Pack failed" }

        & (Join-Path $PSScriptRoot 'test-package-contents.ps1') -PackageDirectory $PluginsDir

        Write-Success "Packages produced"

        # List SDK package (for developers)
        Write-Info "SDK package (cli/packages/):"
        Get-ChildItem $NuGetDir -Filter "Spectara.Revela.Sdk.*.nupkg" | ForEach-Object {
            $size = [math]::Round($_.Length / 1KB, 1)
            Write-Info "  $($_.Name) ($size KB)"
        }

        # List plugin packages (for installation)
        Write-Info "Release packages (cli/packages/):"
        Get-ChildItem $PluginsDir -Filter "*.nupkg" | ForEach-Object {
            $size = [math]::Round($_.Length / 1KB, 1)
            Write-Info "  $($_.Name) ($size KB)"
        }
    }
    }
    else {
        Write-Step 'Steps 2-5 [SKIPPED]: using existing release bytes; no product restore/build/publish/pack'
        Measure-Step 'Stage Artifact' {
            $stagingInput = $ArtifactPath
            if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Container)) {
                New-Item -ItemType Directory -Path $ExtractDir | Out-Null
                if ($ArtifactPath.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
                    [IO.Compression.ZipFile]::ExtractToDirectory($ArtifactPath, $ExtractDir)
                }
                else {
                    $archiveStream = [IO.File]::OpenRead($ArtifactPath)
                    try {
                        $gzipStream = [IO.Compression.GZipStream]::new($archiveStream, [IO.Compression.CompressionMode]::Decompress)
                        try { [System.Formats.Tar.TarFile]::ExtractToDirectory($gzipStream, $ExtractDir, $false) }
                        finally { $gzipStream.Dispose() }
                    }
                    finally { $archiveStream.Dispose() }
                }
                $stagingInput = $ExtractDir
            }
            foreach ($entry in Get-ChildItem -LiteralPath $stagingInput -Recurse -Force) {
                if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked archive content is not supported.' }
            }
            Get-ChildItem -LiteralPath $stagingInput -Force | Copy-Item -Destination $CliDir -Recurse
            if ($Variant -eq 'Core') {
                New-Item -ItemType Directory -Path $PluginsDir | Out-Null
                Get-ChildItem -LiteralPath $PackageDirectory -Force | Copy-Item -Destination $PluginsDir -Recurse
            }
        }
    }

    if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "Artifact is missing $ExeName at its root." }
    if ($Variant -ne 'Standalone') {
        foreach ($entry in Get-ChildItem -LiteralPath $CliDir -Force) {
            if ($entry.PSIsContainer) {
                if ($Variant -ne 'Full' -or $entry.Name -cne 'packages') { throw "Dirty $Variant artifact layout: $($entry.Name)" }
            }
            elseif ($entry.Name -cne $ExeName -and $entry.Name -notmatch '^(?i:readme(?:\.(md|txt))?|license(?:\.(md|txt))?|start-revela\.sh|Start Revela\.command)$') {
                throw "Unexpected loose file in $Variant artifact: $($entry.Name)"
            }
        }
        if (-not (Test-Path -LiteralPath $PluginsDir -PathType Container) -or @(Get-ChildItem -LiteralPath $PluginsDir -File -Filter '*.nupkg').Count -eq 0) {
            throw "$Variant package feed is missing or empty."
        }
    }
    else {
        $vipsName = if ($isWindowsOS) { 'libvips-42.dll' } elseif ($IsMacOS) { 'libvips.42.dylib' } else { 'libvips.so.42' }
        $vipsPath = Join-Path $CliDir $vipsName
        if (-not (Test-Path -LiteralPath $vipsPath -PathType Leaf) -or (Get-Item -LiteralPath $vipsPath).Length -eq 0) {
            throw "Standalone requires its actual native companion: $vipsName"
        }
    }
    if (-not $isWindowsOS) {
        if (([IO.File]::GetUnixFileMode($ExePath) -band [IO.UnixFileMode]::UserExecute) -eq 0) {
            throw 'Release executable is missing its owner execute permission.'
        }
    }
    $releaseVersion = (& $ExePath --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $releaseVersion -cnotmatch $VersionPattern) { throw "Expected exact $Variant version $Version, got: $releaseVersion" }
    Write-Success "$Variant exact version verified: $releaseVersion"

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

    if ($Variant -eq 'Standalone') {
        Measure-Step 'Standalone Config' {
            Push-Location $SampleProjectDir
            try {
                $configPath = Join-Path $SampleProjectDir 'project.json'
                $before = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
                # Plugin settings live below plugins:<key>; Statistics claims 'statistics'.
                if (-not $before.ContainsKey('plugins')) { $before['plugins'] = @{} }
                if (-not $before['plugins'].ContainsKey('statistics')) {
                    $before['plugins']['statistics'] = @{ maxEntriesPerCategory = 19; sortByCount = $true }
                    $before | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding utf8
                }
                $previousValue = $before['plugins']['statistics']['maxEntriesPerCategory']
                $newValue = if ($previousValue -eq 20) { 21 } else { 20 }
                & $ExePath config statistics --max-entries $newValue
                if ($LASTEXITCODE -ne 0) { throw 'Standalone statistics config failed' }
                $after = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
                if ($after['plugins']['statistics']['maxEntriesPerCategory'] -ne $newValue) { throw 'Standalone statistics value was not changed' }
                $before['plugins']['statistics'].Remove('maxEntriesPerCategory')
                $after['plugins']['statistics'].Remove('maxEntriesPerCategory')
                if (($before | ConvertTo-Json -Depth 100 -Compress) -cne ($after | ConvertTo-Json -Depth 100 -Compress)) {
                    throw 'Standalone config changed unrelated settings'
                }
                Write-Success "Standalone fresh-process config changed max entries from $previousValue to $newValue and preserved unrelated settings"
            }
            finally { Pop-Location }
        }
    }
    else {
    # ========================================================================
    # STEP 7: Install Plugins via NuGet (local feed)
    # ========================================================================

    Write-Step "Step 7: Install Plugins (NuGet from local feed)"
    Measure-Step "Install Plugins" {
        Push-Location $SampleProjectDir
        try {
            # Core features (Generate, Theme, Projects) are built into the CLI — no install needed

            Write-Info 'Installing base Lumina from the exact release package...'
            & $ExePath theme install Lumina --version $Version --source $PluginsDir
            if ($LASTEXITCODE -ne 0) { throw 'Base Lumina installation failed' }

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
            & $ExePath theme install Lumina.Statistics --version $Version --source $PluginsDir
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
            & $ExePath theme install Lumina.Calendar --version $Version --source $PluginsDir
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
            if ($LASTEXITCODE -ne 0) { throw 'plugin list failed' }
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

        # Verify the five external plugins installed above; built-in features are not plugins.
        # Themes (Lumina, Lumina.Statistics, Lumina.Calendar) are shown in 'theme list' instead
        $pluginListOutput = & $ExePath plugin list 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -and $pluginListOutput -match "Installed Plugins.*\(5\)") {
            Write-Success "Verified: 5 external plugins loaded"
        }
        else {
            Write-Warn "Plugin list output: $pluginListOutput"
            throw "Expected 5 external plugins in panel header, got unexpected output"
        }

        # Verify installed plugins are local (5 plugins + 2 theme extensions should show 'installed')
        $localMatches = ([regex]::Matches($pluginListOutput, '\binstalled\b')).Count
        if ($localMatches -ge 5) {
            Write-Success "Verified: 5+ packages installed locally (next to exe)"
        }
        else {
            Write-Warn "Expected 5+ installed packages, found $localMatches in output"
        }

        # Verify plugin folders exist with main DLLs (new structure: plugins/{PackageId}/{PackageId}.dll)
        # Core features (Generate, Theme, Projects) are built into the CLI — no plugin folder needed
        $expectedPlugins = @(
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
        & $ExePath plugin install Spectara.Revela.Plugins.Statistics --version $Version --source $PluginsDir
        if ($LASTEXITCODE -ne 0) { throw "Plugin re-install failed" }
        Write-Success "Plugin re-installed for subsequent tests"

        # Test Serve plugin command is registered (--help is non-blocking, unlike actual serve)
        # Must run from sample project dir because serve requires a project
        Write-Info "Testing serve command (--help)..."
        Push-Location $SampleProjectDir
        try {
            $serveHelpOutput = & $ExePath serve --help 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0 -and $serveHelpOutput -match "Preview generated site") {
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
            # Verifies Issue #32: themes appear in 'theme list', NOT in 'plugin list'
            Write-Info "Running: revela theme list"
            $themeListOutput = & $ExePath theme list 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw 'theme list failed' }
            if ($themeListOutput -match "Lumina") {
                Write-Success "Found built-in Lumina theme"
            }
            else {
                throw "Built-in Lumina theme not found in theme list"
            }

            # Verify theme extensions appear under their parent theme
            if ($themeListOutput -match "Statistics" -and $themeListOutput -match "Calendar") {
                Write-Success "Theme extensions (Statistics, Calendar) visible in theme list"
            }
            else {
                Write-Warn "Theme extensions may not be visible in theme list output"
            }

            # Test theme list --online (searches NuGet sources)
            # Add local NuGet source first (for testing without nuget.org)
            # Use a relative path from config directory (cli/) to the variant's feed.
            # This tests that relative paths are correctly resolved at runtime
            Write-Info "Adding local NuGet feed for testing (relative path)..."
            & $ExePath config feed add local-test $RelativeFeed
            if ($LASTEXITCODE -ne 0) { throw 'Adding relative local-test feed failed' }

            Write-Info "Running: revela theme list --online"
            $themeOnlineOutput = & $ExePath theme list --online 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw 'theme list --online failed' }
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
                if ($content.plugins.statistics.maxEntriesPerCategory -eq 20) {
                    Write-Success "Statistics config verified: MaxEntriesPerCategory = 20"
                }
                else {
                    throw 'Statistics config value was not persisted as requested'
                }
            }
            else { throw 'Statistics config did not create project.json' }

            # Test config locations (shows where configs are stored)
            Write-Info "Running: revela config locations"
            $configLocationsOutput = & $ExePath config locations 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "config locations failed" }
            $plainLocationsOutput = $configLocationsOutput -replace '\x1b\[[0-?]*[ -/]*[@-~]', ''
            $compactLocationsOutput = $plainLocationsOutput -replace '[^\x21-\x7e]|\|', ''
            foreach ($configName in @('project.json', 'site.json', 'logging.json')) {
                if ($compactLocationsOutput -notmatch [regex]::Escape($configName)) {
                    throw "config locations did not report $configName"
                }
            }
            Write-Success "config locations reports project, site and logging paths"

        }
        finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 7f: Test Theme & Restore Commands
    # ========================================================================
    Write-Step "Step 7f: Test Theme & Restore Commands"
    Measure-Step "Theme & Restore" {
        Push-Location $SampleProjectDir
        try {
            # Test restore --check (validates all dependencies are present)
            Write-Info "Running: revela restore --check"
            $restoreOutput = & $ExePath restore --check 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "restore --check failed" }
            Write-Success "restore --check passed"

            $projectConfigPath = Join-Path $SampleProjectDir 'project.json'
            $originalProjectBytes = [IO.File]::ReadAllBytes($projectConfigPath)
            try {
                $dependencyProbe = [Text.Encoding]::UTF8.GetString($originalProjectBytes) | ConvertFrom-Json -AsHashtable
                if (-not $dependencyProbe.ContainsKey('dependencies')) { $dependencyProbe['dependencies'] = @{} }
                if (-not $dependencyProbe.dependencies.ContainsKey('packages')) { $dependencyProbe.dependencies['packages'] = @{} }
                $dependencyProbe.dependencies.packages['Spectara.Revela.Plugins.ReleaseTestMissing'] = $Version
                $dependencyProbe | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $projectConfigPath -Encoding utf8
                $missingDependencyOutput = & $ExePath restore --check 2>&1 | Out-String
                $missingDependencyExit = $LASTEXITCODE
                $compactDependencyOutput = $missingDependencyOutput -replace '\s', ''
                if ($missingDependencyExit -ne 1 -or
                    $missingDependencyOutput -notmatch 'Checking dependencies' -or
                    $compactDependencyOutput -notmatch 'Spectara\.Revela\.Plugins\.ReleaseTestMissing.*missing') {
                    throw "restore --check did not diagnose the declared missing plugin (exit $missingDependencyExit): $missingDependencyOutput"
                }
                Write-Success "restore --check rejects a declared missing package"
            }
            finally {
                [IO.File]::WriteAllBytes($projectConfigPath, $originalProjectBytes)
            }

                $registrationProject = Join-Path $TestDir 'registration-project'
                New-Item -ItemType Directory -Path $registrationProject | Out-Null
                $registrationConfig = Join-Path $registrationProject 'project.json'
                $seedVersion = if ($Version -eq '0.0.0-test') { '0.0.0-registration-seed' } else { '0.0.0-test' }
                if ($seedVersion -eq $Version) { throw 'Registration seed must differ from the installed version' }
                $registrationProbe = [ordered]@{
                    Dependencies = [ordered]@{ Packages = [ordered]@{ 'spectara.revela.plugins.statistics' = $seedVersion } }
                    retained = [ordered]@{ value = 'registration-sentinel' }
                }
                $registrationProbe | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $registrationConfig -Encoding utf8
                Push-Location $registrationProject
                try {
                    & $ExePath plugin install Statistics --version $Version --source $PluginsDir
                    if ($LASTEXITCODE -ne 0) { throw 'Mixed-case package registration failed' }
                    $savedRegistration = Get-Content -LiteralPath $registrationConfig -Raw | ConvertFrom-Json -AsHashtable
                    if (-not (@($savedRegistration.Keys) -ccontains 'Dependencies') -or
                        (@($savedRegistration.Keys) -ccontains 'dependencies') -or
                        -not (@($savedRegistration.Dependencies.Keys) -ccontains 'Packages') -or
                        (@($savedRegistration.Keys) -ccontains 'Plugins') -or
                        $savedRegistration.Dependencies.Packages['spectara.revela.plugins.statistics'] -ne $Version -or
                        $savedRegistration.retained.value -ne 'registration-sentinel') {
                        throw 'Mixed-case registration did not preserve keys and unrelated data'
                    }
                    & $ExePath restore --check
                    if ($LASTEXITCODE -ne 0) { throw 'Registered project could not be reopened by the real provider' }
                    Write-Success 'Mixed-case registration changed the version and preserved a readable project'

                    foreach ($packageId in @('Spectara.Revela.Plugins.Statistics', 'Spectara.Revela.Plugins.Serve')) {
                        Remove-Item -LiteralPath (Join-Path $CliDir "plugins/$packageId") -Recurse -Force
                    }
                    # Both packages are declared by the project with 'latest'; restore must pin the exact
                    # installed versions in parallel without losing either entry or unrelated data.
                    $registrationProbe.Dependencies.Packages = [ordered]@{ 'Spectara.Revela.Plugins.Statistics' = 'latest'; 'Spectara.Revela.Plugins.Serve' = 'latest' }
                    $registrationProbe | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $registrationConfig -Encoding utf8
                    $coreRestoreFeed = Join-Path $CliDir 'packages'
                    try {
                        if ($Variant -eq 'Core') {
                            Write-Info 'Core restore fixture: temporarily prioritize the supplied local packages (not shipped Core content).'
                            New-Item -ItemType Directory -Path $coreRestoreFeed | Out-Null
                            Get-ChildItem -LiteralPath $PluginsDir -Filter '*.nupkg' | Copy-Item -Destination $coreRestoreFeed
                        }
                        & $ExePath restore
                        if ($LASTEXITCODE -ne 0) { throw 'Parallel restore registration failed' }
                    }
                    finally {
                        if ($Variant -eq 'Core' -and (Test-Path -LiteralPath $coreRestoreFeed)) {
                            Remove-Item -LiteralPath $coreRestoreFeed -Recurse -Force
                        }
                    }
                    $restoredRegistration = Get-Content -LiteralPath $registrationConfig -Raw | ConvertFrom-Json -AsHashtable
                    if ($restoredRegistration.Dependencies.Packages.Count -ne 2 -or
                        $restoredRegistration.retained.value -ne 'registration-sentinel') {
                        throw 'Parallel registration did not preserve both new entries and unrelated data'
                    }
                    foreach ($packageId in @('Spectara.Revela.Plugins.Statistics', 'Spectara.Revela.Plugins.Serve')) {
                        if ($restoredRegistration.Dependencies.Packages[$packageId] -ne $Version -or
                            -not (Test-Path -LiteralPath (Join-Path $CliDir "plugins/$packageId/$packageId.dll"))) {
                            throw "Parallel restore lost registration or files for $packageId"
                        }
                        $package = [IO.Compression.ZipFile]::OpenRead((Join-Path $PluginsDir "$packageId.$Version.nupkg"))
                        try {
                            $assemblyEntries = @($package.Entries | Where-Object FullName -match "^lib/[^/]+/$([regex]::Escape($packageId))\.dll$")
                            if ($assemblyEntries.Count -ne 1) { throw "Expected one primary assembly in $packageId" }
                            $stream = $assemblyEntries[0].Open()
                            try { $expectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
                            finally { $stream.Dispose() }
                            if ((Get-FileHash -LiteralPath (Join-Path $CliDir "plugins/$packageId/$packageId.dll")).Hash -cne $expectedHash) {
                                throw "Restore did not install the supplied package bytes for $packageId"
                            }
                        }
                        finally { $package.Dispose() }
                    }
                    & $ExePath restore --check
                    if ($LASTEXITCODE -ne 0) { throw 'Restored project did not pass a fresh check' }
                    Write-Success 'Parallel restore preserved both registered packages'

                    [IO.File]::WriteAllText($registrationConfig, '{"Dependencies":{"Packages":{"Spectara.Revela.Plugins.Statistics":"0.0.0-test"}},"dependencies":{"packages":{"Other":"1.0.0"}}}')
                    $beforeFailedRegistration = (Get-FileHash -LiteralPath $registrationConfig).Hash
                    $failureOutput = & $ExePath plugin install Statistics --version $Version --source $PluginsDir 2>&1 | Out-String
                    # Redirected output wraps at 80 columns; compare the message with collapsed whitespace.
                    $failureText = $failureOutput -replace '\s+', ' '
                    if ($LASTEXITCODE -ne 1 -or $failureText -notmatch 'was installed, but declaring it' -or
                        $failureText -match 'installed successfully' -or
                        (Get-FileHash -LiteralPath $registrationConfig).Hash -ne $beforeFailedRegistration -or
                        -not (Test-Path -LiteralPath (Join-Path $CliDir 'plugins/Spectara.Revela.Plugins.Statistics/Spectara.Revela.Plugins.Statistics.dll'))) {
                        throw "Registration failure was not reported with retained files and unchanged configuration: $failureOutput"
                    }
                    Write-Success 'Registration failure is explicit, original configuration and extracted files retained'
                }
                finally {
                    Pop-Location
                }

            # Test theme files (shows all template/asset sources)
            Write-Info "Running: revela theme files"
            if (Test-Path (Join-Path $SampleProjectDir "themes")) {
                throw "theme files must be tested before local themes are extracted"
            }
            $themeFilesOutput = & $ExePath theme files 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "theme files failed" }
            if ($themeFilesOutput -notmatch 'Layout\.revela' -or $themeFilesOutput -notmatch 'main\.css') {
                throw "theme files did not list bundled templates and assets: $themeFilesOutput"
            }
            Write-Success "theme files shows bundled templates and assets"

            Write-Info "Running: revela theme files --theme MissingReleaseTestTheme"
            $missingThemeOutput = & $ExePath theme files --theme MissingReleaseTestTheme 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) { throw "theme files returned success for an unknown theme" }
            if ($missingThemeOutput -notmatch "Theme Not Found") {
                throw "theme files did not report the missing theme: $missingThemeOutput"
            }
            Write-Success "theme files rejects unknown themes"

            # Test theme extract selective (extract a single file to themes/ directory)
            Write-Info "Running: revela theme extract Lumina --file Partials/ --force"
            & $ExePath theme extract Lumina --file "Partials/" --force
            if ($LASTEXITCODE -ne 0) { throw "theme extract failed" }

            $themesDir = Join-Path $SampleProjectDir "themes"
            if (Test-Path $themesDir) {
                $extractedFiles = @(Get-ChildItem $themesDir -Recurse -File)
                if ($extractedFiles.Count -gt 0) {
                    Write-Success "theme extract created $($extractedFiles.Count) files in themes/"
                }
                else {
                    Write-Warn "theme extract created folder but no files"
                }
            }
            else {
                throw "themes/ directory not created by theme extract"
            }

            # Clean up selective extract for full extract test
            if (Test-Path $themesDir) { Remove-Item $themesDir -Recurse -Force }

            # Test full theme extract (verifies Issue #32: extensions extracted to subfolders)
            Write-Info "Running: revela theme extract Lumina --force (full with extensions)"
            & $ExePath theme extract Lumina --force
            if ($LASTEXITCODE -ne 0) { throw "theme full extract failed" }

            $luminaDir = Join-Path $themesDir "Lumina"
            if (Test-Path $luminaDir) {
                $allFiles = @(Get-ChildItem $luminaDir -Recurse -File)
                Write-Success "Full theme extract: $($allFiles.Count) files in themes/Lumina/"

                # Verify extension files are in correct subfolders (Issue #32: extensions extractable)
                $hasStatisticsFolder = Get-ChildItem $luminaDir -Directory -Recurse | Where-Object { $_.Name -eq "Statistics" }
                $hasCalendarFolder = Get-ChildItem $luminaDir -Directory -Recurse | Where-Object { $_.Name -eq "Calendar" }
                if ($hasStatisticsFolder -and $hasCalendarFolder) {
                    Write-Success "Extension subfolders present: Statistics/, Calendar/"
                }
                else {
                    Write-Warn "Extension subfolders may be missing (Statistics=$($null -ne $hasStatisticsFolder), Calendar=$($null -ne $hasCalendarFolder))"
                }
            }
            else {
                throw "themes/Lumina/ not created by full extract"
            }

            # Test create page text
            Write-Info "Running: revela create page text about --title 'About Me'"
            & $ExePath create page text about --title "About Me"
            if ($LASTEXITCODE -ne 0) { throw "create page text failed" }

            $textFile = Join-Path $SampleProjectDir "source/about/_index.revela"
            if (Test-Path $textFile) {
                $textContent = Get-Content $textFile -Raw
                if ($textContent -match 'template\s*=\s*"page"') {
                    Write-Success "Text page created with correct template"
                }
                else {
                    Write-Warn "Text page created but template may differ"
                }
            }
            else {
                throw "Text page file not created"
            }

            # Test config feed list (verify local-test feed from Step 7c is visible)
            Write-Info "Running: revela config feed list"
            $feedListOutput = & $ExePath config feed list 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "config feed list failed" }
            if ($feedListOutput -match "local-test") {
                Write-Success "config feed list shows local-test feed"
            }
            else {
                Write-Warn "local-test feed not visible in feed list"
            }
        }
        finally {
            Pop-Location
        }
    }

    # ========================================================================
    # STEP 8: Generate All (primary user command)
    # ========================================================================
    }
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
        Assert-GeneratedOutput
    }

    # ========================================================================
    # STEP 10: Compress (Plugin Integration Test)
    # ========================================================================
    if ($Variant -eq 'Standalone') {
        Measure-Step 'Standalone Regenerate' {
            Push-Location $SampleProjectDir
            try {
                & $ExePath clean all
                if ($LASTEXITCODE -ne 0) { throw 'Standalone clean all failed' }
                $outputPath = Join-Path $SampleProjectDir 'output'
                if (Test-Path -LiteralPath $outputPath) {
                    if (@(Get-ChildItem -LiteralPath $outputPath -Recurse -File).Count -ne 0) { throw 'Standalone clean retained output' }
                }
                & $ExePath generate all
                if ($LASTEXITCODE -ne 0) { throw 'Standalone regeneration failed' }
                Assert-GeneratedOutput
            }
            finally { Pop-Location }
        }
    }
    else {
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
            if ($gzFiles.Count -gt 0 -and $brFiles.Count -gt 0) {
                Write-Success "Compressed files: $($gzFiles.Count) .gz, $($brFiles.Count) .br"
            }
            else {
                throw 'Expected both gzip and Brotli output for the generated showcase.'
            }

            # The ownership record lives in the plugin's folder, never in the published output
            $ownershipRecord = Join-Path $SampleProjectDir ".revela/compress/ownership.json"
            if (-not (Test-Path -LiteralPath $ownershipRecord)) {
                throw 'generate compress did not write .revela/compress/ownership.json.'
            }
            $internalOutput = @(Get-ChildItem -LiteralPath $outputDir -Recurse -Force | Where-Object { $_.Name.StartsWith('.revela', [StringComparison]::Ordinal) })
            if ($internalOutput.Count -gt 0) {
                throw "Output contains Revela-internal files: $($internalOutput.FullName -join ', ')"
            }
            Write-Success "Ownership record kept in .revela/compress; output contains only the site"

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
                throw 'Compressed files remain after clean compress.'
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
            $revelaDir = Join-Path $SampleProjectDir ".revela"
            if (-not (Test-Path $outputDir) -or @(Get-ChildItem $outputDir -Recurse -File -ErrorAction SilentlyContinue).Count -eq 0) {
                Write-Success "clean all removed output"
            }
            else {
                throw 'Output directory not fully cleaned.'
            }
            # The sample has no durable plugin data, so clean all leaves no file in .revela
            if ((Test-Path -LiteralPath $revelaDir) -and @(Get-ChildItem -LiteralPath $revelaDir -Recurse -File).Count -gt 0) {
                throw 'clean all retained files in .revela.'
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
    # STEP 11b: Individual Pipeline Steps (isolation test)
    # ========================================================================
    Write-Step "Step 11b: Individual Pipeline Steps"
    Measure-Step "Pipeline Steps" {
        Push-Location $SampleProjectDir
        try {
            # Clean everything first to test individual steps in order
            Write-Info "Cleaning output for individual step test..."
            & $ExePath clean all
            if ($LASTEXITCODE -ne 0) { throw "clean all (pre-step-test) failed" }

            # Test generate scan (creates manifest)
            Write-Info "Running: revela generate scan"
            & $ExePath generate scan
            if ($LASTEXITCODE -ne 0) { throw "generate scan failed" }

            $revelaDir = Join-Path $SampleProjectDir ".revela"
            if (Test-Path (Join-Path $revelaDir "core/manifest.json")) {
                Write-Success "generate scan created .revela/core/manifest.json"
            }
            else {
                throw "generate scan did not create .revela/core/manifest.json"
            }

            # Test generate statistics (must run before pages — templates reference stats data)
            Write-Info "Running: revela generate statistics"
            & $ExePath generate statistics
            if ($LASTEXITCODE -ne 0) { throw "generate statistics failed" }
            Write-Success "generate statistics completed"

            # Test generate pages (renders HTML from manifest)
            Write-Info "Running: revela generate pages"
            & $ExePath generate pages
            if ($LASTEXITCODE -ne 0) { throw "generate pages failed" }

            $outputDir = Join-Path $SampleProjectDir "output"
            $indexPath = Join-Path $outputDir "index.html"
            if (Test-Path $indexPath) {
                Write-Success "generate pages created HTML output"
            }
            else {
                throw "generate pages did not create index.html"
            }

            # Test generate images (processes and resizes images)
            Write-Info "Running: revela generate images"
            & $ExePath generate images
            if ($LASTEXITCODE -ne 0) { throw "generate images failed" }

            $imagesDir = Join-Path $outputDir "images"
            if (Test-Path $imagesDir) {
                $imageCount = (Get-ChildItem $imagesDir -Recurse -File).Count
                Write-Success "generate images created $imageCount image files"
            }
            else {
                throw "generate images did not create images/ directory"
            }

            # Test individual clean steps
            $validImageHashes = @{}
            foreach ($imageFile in Get-ChildItem -LiteralPath $imagesDir -Recurse -File) {
                $validImageHashes[$imageFile.FullName] = (Get-FileHash -LiteralPath $imageFile.FullName).Hash
            }
            if ($validImageHashes.Count -eq 0) { throw "No valid image variants for cleanup preservation test" }
            $referenceImage = Get-ChildItem -LiteralPath $imagesDir -Recurse -File -Filter '*.jpg' | Select-Object -First 1
            if ($null -eq $referenceImage) { throw "No JPEG variant for obsolete-size fixture" }
            $obsoleteVariant = Join-Path $referenceImage.DirectoryName '2147483647.jpg'
            if (Test-Path -LiteralPath $obsoleteVariant) { throw "Obsolete-size fixture already exists" }
            Copy-Item -LiteralPath $referenceImage.FullName -Destination $obsoleteVariant
            & $ExePath clean images --dry-run
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $obsoleteVariant)) {
                throw "clean images dry-run changed the obsolete variant or failed"
            }
            Write-Info "Running: revela clean images"
            & $ExePath clean images
            if ($LASTEXITCODE -ne 0) { throw "clean images failed" }
            if (Test-Path -LiteralPath $obsoleteVariant) { throw "clean images retained the obsolete size" }
            foreach ($entry in $validImageHashes.GetEnumerator()) {
                if (-not (Test-Path -LiteralPath $entry.Key) -or (Get-FileHash -LiteralPath $entry.Key).Hash -ne $entry.Value) {
                    throw "clean images removed or changed a valid variant: $($entry.Key)"
                }
            }
            if (@(Get-ChildItem -LiteralPath $imagesDir -Recurse -File).Count -ne $validImageHashes.Count) {
                throw "Unexpected image count after clean images"
            }
            Write-Success "clean images removed the obsolete variant and preserved $($validImageHashes.Count) valid image hashes"

            Write-Info "Running: revela clean output"
            & $ExePath clean output
            if ($LASTEXITCODE -ne 0) { throw "clean output failed" }
            if ((Test-Path -LiteralPath $outputDir) -and @(Get-ChildItem -LiteralPath $outputDir -Recurse -File).Count -gt 0) {
                throw 'clean output retained generated files.'
            }
            if (Test-Path -LiteralPath (Join-Path $revelaDir "core/images.json")) {
                throw 'clean output retained the image state .revela/core/images.json.'
            }
            if (-not (Test-Path -LiteralPath (Join-Path $revelaDir "core/manifest.json"))) {
                throw 'clean output removed the manifest, a cache artifact.'
            }

            Write-Info "Running: revela clean cache"
            & $ExePath clean cache
            if ($LASTEXITCODE -ne 0) { throw "clean cache failed" }
            # After clean output and clean cache nothing remains (the sample has no durable data)
            if (-not (Test-Path $revelaDir) -or @(Get-ChildItem $revelaDir -Recurse -File -ErrorAction SilentlyContinue).Count -eq 0) {
                Write-Success "clean cache removed cache files"
            }
            else {
                throw 'clean cache retained cache files.'
            }

            # Restore output for subsequent steps
            Write-Info "Running: revela generate all (restore for subsequent steps)"
            & $ExePath generate all
            if ($LASTEXITCODE -ne 0) { throw "generate all (restore) failed" }
            Write-Success "Individual pipeline steps all working correctly"
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
        $nupkgFile = Get-Item -LiteralPath (Join-Path $PluginsDir "Spectara.Revela.$Version.nupkg")
        if (-not $nupkgFile) { throw "NuGet package not found" }
        Write-Info "Package: $($nupkgFile.Name) ($([Math]::Round($nupkgFile.Length / 1MB, 2)) MB)"

        Write-Info "Installing tool from local package..."
        $escapedFeed = [Security.SecurityElement]::Escape($PluginsDir)
        [IO.File]::WriteAllText($ToolConfigPath, "<configuration><packageSources><clear /><add key=`"release-under-test`" value=`"$escapedFeed`" /></packageSources></configuration>")
        $installResult = dotnet tool install --tool-path $ToolInstallDir Spectara.Revela `
            --version $Version `
            --configfile $ToolConfigPath `
            --no-cache `
            --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Isolated tool installation failed: $installResult"
        }
        Write-Success "Tool installed in isolated test directory"
        $installedPackages = @(Get-ChildItem -LiteralPath $ToolInstallDir -Recurse -File -Force -Filter "spectara.revela.$Version.nupkg")
        if ($installedPackages.Count -ne 1) {
            throw "Installed tool package lookup expected exactly one archive, found $($installedPackages.Count)."
        }
        if ((Get-FileHash -LiteralPath $installedPackages[0].FullName).Hash -cne (Get-FileHash -LiteralPath $nupkgFile.FullName).Hash) {
            throw 'Installed tool package does not match the supplied release package hash.'
        }
        Write-Success 'Installed tool package SHA256 matches the exact supplied release package'
        $toolExe = Join-Path $ToolInstallDir $ExeName

        $toolCheckFailed = $true
        try {
            Write-Info "Testing tool command..."
            $versionOutput = & $toolExe --version 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Tool command failed: $versionOutput" }

            # Verify version matches what we packed
            if (($versionOutput | Out-String).Trim() -cmatch $VersionPattern) {
                Write-Success "Version matches: $versionOutput"
            }
            else {
                throw "Version mismatch: expected $Version, got $versionOutput"
            }

            Write-Info "Testing plugin list..."
            $pluginOutput = & $toolExe plugin list 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Plugin list failed: $pluginOutput" }
            Write-Success "Plugin list command works"
            $toolCheckFailed = $false
        }
        finally {
            Write-Info "Uninstalling tool..."
            try {
                $null = dotnet tool uninstall --tool-path $toolInstallDir Spectara.Revela 2>&1
                if ($LASTEXITCODE -ne 0) { throw "Tool uninstall failed" }
                Write-Success "Tool uninstalled"
            }
            catch {
                if (-not $toolCheckFailed) { throw }
                Write-Warn "Isolated tool cleanup also failed; retaining the original test failure. Inspect $toolInstallDir."
            }
        }

        Write-Success "✓ .NET Tool package verified"
    }

    # ========================================================================
    # STEP 13: Summary
    # ========================================================================
    Measure-Step "SDK Consumer" {
        & (Join-Path $ScriptDir 'test-sdk-consumer.ps1') -PackageDirectory $NuGetDir -Version $Version
        Write-Success "Actual release SDK package consumer verified"
    }
    }

    Write-Banner "$Variant Release Test Complete ($Mode)"

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

    Write-Host "  $Variant release suite passed; checking input preservation..." -ForegroundColor Green
    Write-Host ""

}
catch {
    $PipelineFailure = $_
    Write-Host ""
    Write-Err "Pipeline failed: $_"
    Write-Host ""
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    throw
}
finally {
    Pop-Location
    foreach ($name in $SavedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $SavedEnvironment[$name], 'Process')
    }
    try {
        $afterHashes = Get-InputHashes $InputPaths
        if (Test-Path -LiteralPath $TestDir) {
            $afterHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $TestDir 'after-hashes.json') -Encoding utf8
        }
        if ($afterHashes.Count -ne $InputHashes.Count) { throw 'Input file inventory changed during release tests.' }
        foreach ($entry in $InputHashes.GetEnumerator()) {
            if (-not $afterHashes.ContainsKey($entry.Key) -or $afterHashes[$entry.Key] -cne $entry.Value) {
                throw "Input file changed during release tests: $($entry.Key)"
            }
        }
        Write-Success "Input preservation verified: $($InputHashes.Count) SHA256 hashes unchanged (artifact/feed and original showcase)"
        if ($null -eq $PipelineFailure) { Write-Success "$Variant release pipeline test PASSED ($Mode)" }
    }
    catch {
        if ($null -eq $PipelineFailure) { throw }
        Write-Err "Input preservation also failed; retaining original pipeline failure: $_"
    }
    finally {
        if ($TranscriptStarted) { Stop-Transcript | Out-Null }
    }

}
