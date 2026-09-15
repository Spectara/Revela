<#
.SYNOPSIS
    Validates the release event and version before publishing a workflow output.
.DESCRIPTION
    Reads RELEASE_EVENT, RELEASE_INPUT_VERSION, RELEASE_REF_NAME, RELEASE_REF_TYPE
    and GITHUB_OUTPUT from the environment. Versions retain the downstream policy
    X.Y.Z[-alphanumeric[.digits]], with no leading zeros in core numbers or purely
    numeric prerelease identifiers. Hyphenated labels, multiple textual prerelease
    identifiers and build metadata are not supported. Values must also be accepted
    by PowerShell's built-in SemanticVersion parser (including its numeric limits).
    Pushes must be tags newer than every other valid release tag by SemVer precedence;
    invalid historical v-prefixed tags fail closed. Manual runs allow older versions
    without consulting or creating tags. Output is appended as UTF-8 without a BOM.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function ConvertTo-ReleaseVersion {
    param([string]$Value, [string]$FailureMessage)

    $pattern = '\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-([0-9A-Za-z]+)(?:\.(?:0|[1-9][0-9]*))?)?\z'
    $match = [regex]::Match($Value, $pattern)
    $parsed = $null
    if (-not $match.Success -or $match.Groups[1].Value -cmatch '\A0[0-9]+\z' -or
        -not [System.Management.Automation.SemanticVersion]::TryParse($Value, [ref]$parsed)) {
        throw $FailureMessage
    }
    return $parsed
}

$eventName = $env:RELEASE_EVENT
$refName = $env:RELEASE_REF_NAME
if ($eventName -ceq 'workflow_dispatch') {
    $version = $env:RELEASE_INPUT_VERSION
}
elseif ($eventName -ceq 'push') {
    if ($env:RELEASE_REF_TYPE -cne 'tag' -or [string]::IsNullOrEmpty($refName) -or
        -not $refName.StartsWith('v', [StringComparison]::Ordinal)) {
        throw 'A push release requires a v-prefixed tag ref.'
    }
    $version = $refName.Substring(1)
}
else {
    throw 'Unsupported or missing release event.'
}

$current = ConvertTo-ReleaseVersion $version 'Invalid or missing release version. Expected canonical X.Y.Z[-alphanumeric[.digits]].'
$outputPath = $env:GITHUB_OUTPUT
if ([string]::IsNullOrWhiteSpace($outputPath)) {
    throw 'GITHUB_OUTPUT is required for the validated release version.'
}

if ($eventName -ceq 'push') {
    Push-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
    try {
        $tags = @(git tag --list 'v*' 2>$null)
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to enumerate release tags with git.'
        }
    }
    finally {
        Pop-Location
    }

    $latest = $null
    foreach ($tag in $tags) {
        if ([string]::IsNullOrEmpty($tag) -or $tag -ceq $refName) {
            continue
        }
        if (-not $tag.StartsWith('v', [StringComparison]::Ordinal)) {
            throw 'Invalid historical release tag. All listed tags must be v-prefixed.'
        }
        $historical = ConvertTo-ReleaseVersion $tag.Substring(1) 'Invalid historical v-prefixed release tag. Expected canonical X.Y.Z[-alphanumeric[.digits]].'
        if ($null -eq $latest -or $historical.CompareTo($latest) -gt 0) {
            $latest = $historical
        }
    }
    if ($null -ne $latest -and $current.CompareTo($latest) -le 0) {
        throw 'The pushed release version must be strictly newer than every other release tag.'
    }
}

[IO.File]::AppendAllText($outputPath, "version=$version`n", [Text.UTF8Encoding]::new($false))