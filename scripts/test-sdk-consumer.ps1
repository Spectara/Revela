<#
.SYNOPSIS
    Tests the SDK NuGet generator with an isolated .NET 10 / C# 14 consumer.
.DESCRIPTION
    Packs the SDK unless PackageDirectory is supplied. Uses only the tested SDK
    package and an explicit Scriban reference, with repository build inheritance
    disabled. Retains packages, generated source and logs in artifacts/sdk-consumer-*.
.PARAMETER PackageDirectory
    Existing package directory inside this repository. Skips packing and tests a
    copy of Spectara.Revela.Sdk.<Version>.nupkg from this directory.
.PARAMETER Version
    Exact SDK package version to pack or consume (default: 0.0.1-beta.21).
.EXAMPLE
    .\scripts\test-sdk-consumer.ps1
.EXAMPLE
    .\scripts\test-sdk-consumer.ps1 -PackageDirectory artifacts/packages -Version 0.0.1-beta.21
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.0.1-beta.21'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$RepoRoot = Split-Path -Parent $PSScriptRoot
$RunRoot = Join-Path $RepoRoot "artifacts/sdk-consumer-$([Guid]::NewGuid().ToString('N'))"
$Feed = Join-Path $RunRoot 'feed'
$ConsumerRoot = Join-Path $RunRoot 'consumer'
$PackageCache = Join-Path $RunRoot 'packages'
$PackageName = "Spectara.Revela.Sdk.$Version.nupkg"
$PackagePath = Join-Path $Feed $PackageName
$AnalyzerEntry = 'analyzers/dotnet/cs/Spectara.Revela.Sdk.Generators.dll'

function Invoke-DotNet {
    param([string]$LogName, [string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    $commandOutput = @(& dotnet @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $commandOutput | Set-Content -LiteralPath (Join-Path $RunRoot "$LogName.log") -Encoding utf8
    foreach ($line in $commandOutput) {
        Write-Host $line
    }
    Write-Host "$LogName exit code: $exitCode"
    if ($exitCode -ne 0) {
        throw "$LogName failed with exit code $exitCode. Evidence: $RunRoot"
    }
    return $commandOutput
}

New-Item -ItemType Directory -Path $Feed, $ConsumerRoot, $PackageCache | Out-Null
Write-Host "SDK consumer evidence: $RunRoot"
Push-Location $RepoRoot
try {
    if ($PackageDirectory) {
        $sourceDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
        $relativeDirectory = [IO.Path]::GetRelativePath($RepoRoot, $sourceDirectory)
        if ([IO.Path]::IsPathRooted($relativeDirectory) -or $relativeDirectory -eq '..' -or
            $relativeDirectory.StartsWith('../', [StringComparison]::Ordinal) -or
            $relativeDirectory.StartsWith('..\', [StringComparison]::Ordinal)) {
            throw 'PackageDirectory must be inside the Revela repository.'
        }
        Copy-Item -LiteralPath (Join-Path $sourceDirectory $PackageName) -Destination $PackagePath
        Write-Host "Reusing $PackageName without rebuilding."
    }
    else {
        Invoke-DotNet 'pack' @(
            'pack', 'src/Sdk/Sdk.csproj', '-c', 'Release',
            "-p:Version=$Version", "-p:PackageVersion=$Version",
            '-p:DebugType=embedded', '-p:IncludeSymbols=false', '-o', $Feed
        ) | Out-Null
    }

    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entries = @($archive.Entries.FullName)
        $entries | Set-Content -LiteralPath (Join-Path $RunRoot 'package-entries.txt') -Encoding utf8
        $analyzerCount = @($entries | Where-Object { $_ -ceq $AnalyzerEntry }).Count
        Write-Host "Packaged Revela generator entries: $analyzerCount (expected 1)"
        if (@($entries | Where-Object { $_ -match '(^|/)Microsoft\.CodeAnalysis[^/]*\.dll$' }).Count -ne 0) {
            throw 'SDK package must not bundle Roslyn assemblies.'
        }
        if (@($entries | Where-Object { $_ -match '^(lib|runtimes)/.*Spectara\.Revela\.Sdk\.Generators\.dll$' }).Count -ne 0) {
            throw 'Generator must not be a runtime assembly.'
        }
        $nuspecEntry = $archive.GetEntry('Spectara.Revela.Sdk.nuspec')
        if ($null -eq $nuspecEntry) {
            throw 'Expected SDK nuspec is missing.'
        }
        $reader = [IO.StreamReader]::new($nuspecEntry.Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
        if ($nuspec.package.metadata.id -cne 'Spectara.Revela.Sdk' -or
            $nuspec.package.metadata.version -cne $Version) {
            throw 'SDK package identity/version does not match its requested archive name.'
        }
        foreach ($dependency in $nuspec.SelectNodes('//*[local-name()="dependency"]')) {
            if ($dependency.id -match '^(Microsoft\.CodeAnalysis|Spectara\.Revela\.Sdk\.Generators|Scriban)') {
                throw "Unexpected SDK runtime dependency: $($dependency.id)"
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $consumerProject = Join-Path $ConsumerRoot 'SdkConsumer.csproj'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
    <ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Spectara.Revela.Sdk" Version="[$Version]" />
    <PackageReference Include="Scriban" Version="[7.4.0]" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $consumerProject -Encoding utf8

    @'
using Scriban;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.TemplateModels;

namespace SdkConsumer;

internal static class Program
{
    private static void Main()
    {
        var model = new GalleryModel { DisplayName = "Package generator works" };
        var scriptObject = model.ToScriptObject();
        if (scriptObject["display_name"] is not string value ||
            !string.Equals(value, model.DisplayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated ScriptObject field is incorrect.");
        }

        var template = Template.Parse("{{ display_name }}");
        if (template.HasErrors)
        {
            throw new InvalidOperationException("Consumer template failed to parse.");
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        var rendered = template.Render(context);
        if (!string.Equals(rendered, model.DisplayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated ScriptObject did not render correctly.");
        }

        Console.WriteLine($"SDK_CONSUMER_OK: display_name={rendered}");
    }
}

[RevelaTemplateModel]
internal sealed class GalleryModel
{
    public required string DisplayName { get; init; }
}
'@ | Set-Content -LiteralPath (Join-Path $ConsumerRoot 'Program.cs') -Encoding utf8

    $nugetConfig = Join-Path $ConsumerRoot 'NuGet.Config'
    $escapedFeed = [Security.SecurityElement]::Escape($Feed)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="sdk-under-test" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="sdk-under-test"><package pattern="Spectara.Revela.Sdk" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding utf8

    $isolatedProperties = @(
        '-p:ImportDirectoryBuildProps=false', '-p:ImportDirectoryBuildTargets=false',
        '-p:ImportDirectoryPackagesProps=false', '-p:ManagePackageVersionsCentrally=false',
        "-p:RestorePackagesPath=$PackageCache"
    )
    Invoke-DotNet 'restore' (@('restore', $consumerProject, '--configfile', $nugetConfig) + $isolatedProperties) | Out-Null

    $evaluatedOutput = Invoke-DotNet 'evaluated' (@(
        'msbuild', $consumerProject, '-nologo', '-target:ResolveReferences',
        '-getProperty:ImportDirectoryBuildProps,ImportDirectoryBuildTargets,ImportDirectoryPackagesProps,ManagePackageVersionsCentrally,EnableConfigurationBindingGenerator,TargetFramework,LangVersion',
        '-getItem:ProjectReference,Analyzer,PackageReference'
    ) + $isolatedProperties)
    $evaluation = ($evaluatedOutput -join [Environment]::NewLine) | ConvertFrom-Json
    foreach ($property in @('ImportDirectoryBuildProps', 'ImportDirectoryBuildTargets', 'ImportDirectoryPackagesProps', 'ManagePackageVersionsCentrally')) {
        if ($evaluation.Properties.$property -ne 'false') {
            throw "Consumer isolation property $property is not false."
        }
    }
    if ($evaluation.Properties.TargetFramework -ne 'net10.0' -or $evaluation.Properties.LangVersion -ne '14' -or
        $evaluation.Properties.EnableConfigurationBindingGenerator -eq 'true') {
        throw 'Consumer framework/language/configuration generator settings are not isolated as expected.'
    }
    if (@($evaluation.Items.ProjectReference).Count -ne 0) {
        throw 'Consumer unexpectedly has project references.'
    }
    [xml]$projectXml = Get-Content -LiteralPath $consumerProject -Raw
    if ($projectXml.SelectNodes('//Analyzer | //ProjectReference').Count -ne 0) {
        throw 'Consumer must not manually include an analyzer or project reference.'
    }
    $references = @($evaluation.Items.PackageReference.Identity | Sort-Object)
    if (($references -join ',') -cne 'Scriban,Spectara.Revela.Sdk') {
        throw 'Consumer must reference exactly Scriban and the SDK package.'
    }

    $preprocessedPath = Join-Path $RunRoot 'consumer.preprocessed.xml'
    Invoke-DotNet 'imports' (@('msbuild', $consumerProject, '-nologo', "-preprocess:$preprocessedPath") + $isolatedProperties) | Out-Null
    $preprocessed = (Get-Content -LiteralPath $preprocessedPath -Raw).Replace('\', '/')
    foreach ($fileName in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        $forbiddenImport = (Join-Path $RepoRoot $fileName).Replace('\', '/')
        if ($preprocessed.Contains($forbiddenImport, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Consumer imported repository $fileName."
        }
    }
    Write-Host 'PASS: repository build/package imports absent; no ProjectReference or manual Analyzer include.'

    Invoke-DotNet 'build' (@('build', $consumerProject, '-c', 'Release', '--no-restore') + $isolatedProperties) | Out-Null
    if ($analyzerCount -ne 1) {
        throw "Package must contain exactly one $AnalyzerEntry."
    }
    $generatorAnalyzers = @($evaluation.Items.Analyzer | Where-Object {
        [IO.Path]::GetFileName($_.Identity) -ceq 'Spectara.Revela.Sdk.Generators.dll'
    })
    $expectedAnalyzerPath = Join-Path $PackageCache "spectara.revela.sdk/$Version/$AnalyzerEntry"
    if ($generatorAnalyzers.Count -ne 1 -or
        [IO.Path]::GetFullPath($generatorAnalyzers[0].Identity) -ne [IO.Path]::GetFullPath($expectedAnalyzerPath)) {
        throw 'Consumer must load exactly one Revela generator from the isolated SDK package cache.'
    }
    $generatedFiles = @(Get-ChildItem -LiteralPath (Join-Path $ConsumerRoot 'obj/generated') -Recurse -Filter '*GalleryModel.ScriptObject.g.cs')
    if ($generatedFiles.Count -ne 1) {
        throw 'Expected exactly one generated GalleryModel ScriptObject source file.'
    }
    $cachedPackage = Join-Path $PackageCache "spectara.revela.sdk/$Version/spectara.revela.sdk.$Version.nupkg"
    if ((Get-FileHash -LiteralPath $cachedPackage).Hash -ne (Get-FileHash -LiteralPath $PackagePath).Hash) {
        throw 'Restored SDK does not match the actual package under test.'
    }
    $assets = Get-Content -LiteralPath (Join-Path $ConsumerRoot 'obj/project.assets.json') -Raw | ConvertFrom-Json
    if (@($assets.libraries.PSObject.Properties.Name | Where-Object { $_ -match '^(Microsoft\.CodeAnalysis|Spectara\.Revela\.Sdk\.Generators)/' }).Count -ne 0) {
        throw 'Consumer acquired a runtime Roslyn/generator package dependency.'
    }

    $runtimeOutput = Invoke-DotNet 'run' (@('run', '--project', $consumerProject, '-c', 'Release', '--no-build', '--no-restore') + $isolatedProperties)
    if ('SDK_CONSUMER_OK: display_name=Package generator works' -cnotin $runtimeOutput) {
        throw 'Consumer runtime success marker is missing.'
    }
    Write-Host 'PASS: one packaged analyzer, one generated model, exact restored package hash, correct ScriptObject field and Scriban rendering.'
    Write-Host "SDK consumer passed. Evidence: $RunRoot"
}
finally {
    Pop-Location
}