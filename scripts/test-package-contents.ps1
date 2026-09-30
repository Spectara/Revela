<#
.SYNOPSIS
    Verifies that packed Revela plugins and themes are self-contained.
.DESCRIPTION
    Every package with the RevelaPlugin or RevelaTheme package type must carry
    lib/<tfm>/<id>.dll and lib/<tfm>/<id>.deps.json. Both are added by
    _RevelaIncludePluginDependencies in src/Sdk/build/Spectara.Revela.Sdk.targets
    together with the plugin-specific dependencies, so a missing .deps.json means
    the SDK targets were not imported (for example because of a path casing
    mismatch on a case-sensitive file system).
.PARAMETER PackageDirectory
    Directory containing the .nupkg files to verify.
.EXAMPLE
    .\scripts\test-package-contents.ps1 -PackageDirectory ./nupkgs
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg')
if ($packages.Count -eq 0) {
    throw "No .nupkg files found in '$PackageDirectory'."
}

$failures = [System.Collections.Generic.List[string]]::new()
$checked = 0

foreach ($package in $packages) {
    $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $nuspecEntry = @($zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.FullName.EndsWith('.nuspec', [StringComparison]::Ordinal) })
        if ($nuspecEntry.Count -ne 1) {
            $failures.Add("$($package.Name): expected exactly one root .nuspec")
            continue
        }

        $reader = [IO.StreamReader]::new($nuspecEntry[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() }
        finally { $reader.Dispose() }

        $metadata = $nuspec.package.metadata
        $packageTypes = @($metadata.SelectNodes("*[local-name()='packageTypes']/*[local-name()='packageType']") | ForEach-Object { $_.GetAttribute('name') })
        if (-not ($packageTypes -contains 'RevelaPlugin' -or $packageTypes -contains 'RevelaTheme')) {
            continue
        }

        $checked++
        $id = $metadata.id
        $libEntries = @($zip.Entries | Where-Object { $_.FullName.StartsWith('lib/', [StringComparison]::Ordinal) } | ForEach-Object FullName)
        foreach ($suffix in @('.dll', '.deps.json')) {
            $pattern = "^lib/[^/]+/$([regex]::Escape($id))$([regex]::Escape($suffix))$"
            if (@($libEntries | Where-Object { $_ -cmatch $pattern }).Count -ne 1) {
                $failures.Add("$($package.Name): missing lib/<tfm>/$id$suffix")
            }
        }

        Write-Host "$($package.Name): $($libEntries.Count) lib entries"
    }
    finally {
        $zip.Dispose()
    }
}

if ($checked -eq 0) {
    throw "No RevelaPlugin/RevelaTheme packages found in '$PackageDirectory'."
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "::error::$_" }
    throw "$($failures.Count) package content check(s) failed."
}

Write-Host "Verified $checked plugin/theme package(s)."
