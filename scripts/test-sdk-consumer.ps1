<#
.SYNOPSIS
    Tests the SDK NuGet generator with an isolated .NET 10 / C# 14 consumer.
.DESCRIPTION
    Packs the SDK unless PackageDirectory is supplied. Uses only the tested SDK
    package and an explicit Scriban reference, with repository build inheritance
    disabled. Retains packages, generated source and logs in artifacts/sdk-consumer-*.

    Also builds three third-party plugin projects (PackageType RevelaPlugin) against
    the package to prove plugin configuration isolation flows through NuGet: a
    compliant plugin builds, a plugin reading IConfiguration fails with RS0030 and a
    plugin binding a foreign section fails with REVELA003 — both as errors without
    TreatWarningsAsErrors. The non-plugin consumer uses IConfiguration and must build.
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
$BannedApiPackage = 'Microsoft.CodeAnalysis.BannedApiAnalyzers'
$BannedSymbolsEntries = @('build/BannedSymbols.RevelaPlugin.txt', 'buildTransitive/BannedSymbols.RevelaPlugin.txt')

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

function Invoke-DotNetExpectFailure {
    param([string]$LogName, [string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ') (expected to fail)"
    $commandOutput = @(& dotnet @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $commandOutput | Set-Content -LiteralPath (Join-Path $RunRoot "$LogName.log") -Encoding utf8
    foreach ($line in $commandOutput) {
        Write-Host $line
    }
    Write-Host "$LogName exit code: $exitCode"
    if ($exitCode -eq 0) {
        throw "$LogName unexpectedly succeeded. Evidence: $RunRoot"
    }
    return $commandOutput
}

function New-PluginConsumer {
    param([string]$Name, [string]$Source)

    $root = Join-Path $RunRoot $Name
    New-Item -ItemType Directory -Path $root | Out-Null
    $project = Join-Path $root "$Name.csproj"
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageType>RevelaPlugin</PackageType>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
    <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
    <ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Spectara.Revela.Sdk" Version="[$Version]" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
    $Source | Set-Content -LiteralPath (Join-Path $root 'Plugin.cs') -Encoding utf8
    return $project
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
        foreach ($bannedSymbolsEntry in $BannedSymbolsEntries) {
            if ($bannedSymbolsEntry -cnotin $entries) {
                throw "SDK package is missing the plugin configuration ban list: $bannedSymbolsEntry"
            }
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
        $bannedApiDependencies = 0
        foreach ($dependency in $nuspec.SelectNodes('//*[local-name()="dependency"]')) {
            if ($dependency.id -ceq $BannedApiPackage) {
                # The RS0030 analyzer for the plugin configuration ban: it must flow as an analyzer only.
                $excluded = @($dependency.GetAttribute('exclude').Split(',') | ForEach-Object { $_.Trim() })
                foreach ($asset in @('Runtime', 'Compile', 'Build', 'Native', 'BuildTransitive')) {
                    if ($asset -notin $excluded) {
                        throw "$BannedApiPackage must not flow $asset assets (exclude='$($dependency.GetAttribute('exclude'))')."
                    }
                }
                if ('Analyzers' -in $excluded -or $dependency.HasAttribute('include')) {
                    throw "$BannedApiPackage must flow its analyzer to plugin projects."
                }
                $bannedApiDependencies++
                continue
            }
            if ($dependency.id -match '^(Microsoft\.CodeAnalysis|Spectara\.Revela\.Sdk\.Generators|Scriban)') {
                throw "Unexpected SDK runtime dependency: $($dependency.id)"
            }
        }
        if ($bannedApiDependencies -ne 1) {
            throw "Expected exactly one $BannedApiPackage dependency, found $bannedApiDependencies."
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

// Not a plugin (no RevelaPlugin PackageType): configuration APIs stay available.
internal static class HostStyleSettings
{
    public static string? Read(Microsoft.Extensions.Configuration.IConfiguration configuration) => configuration["sdk:consumer"];
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
    if (@($assets.libraries.PSObject.Properties.Name | Where-Object {
        $_ -match '^(Microsoft\.CodeAnalysis|Spectara\.Revela\.Sdk\.Generators)/' -and -not $_.StartsWith("$BannedApiPackage/", [StringComparison]::Ordinal)
    }).Count -ne 0) {
        throw 'Consumer acquired a runtime Roslyn/generator package dependency.'
    }
    foreach ($target in $assets.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties | Where-Object { $_.Name.StartsWith("$BannedApiPackage/", [StringComparison]::Ordinal) }) {
            # NuGet records an excluded asset group as the placeholder "_._".
            foreach ($group in $library.Value.PSObject.Properties | Where-Object { $_.Name -match '^(compile|runtime|native|build|buildMultiTargeting)$' }) {
                if (@($group.Value.PSObject.Properties.Name | Where-Object { -not $_.EndsWith('/_._', [StringComparison]::Ordinal) }).Count -ne 0) {
                    throw "$BannedApiPackage must contribute no $($group.Name) assets to consumers."
                }
            }
        }
    }

    $runtimeOutput = Invoke-DotNet 'run' (@('run', '--project', $consumerProject, '-c', 'Release', '--no-build', '--no-restore') + $isolatedProperties)
    if ('SDK_CONSUMER_OK: display_name=Package generator works' -cnotin $runtimeOutput) {
        throw 'Consumer runtime success marker is missing.'
    }
    Write-Host 'PASS: one packaged analyzer, one generated model, exact restored package hash, correct ScriptObject field and Scriban rendering.'

    # Plugin configuration isolation, as a third-party plugin author gets it from NuGet.
    $pluginPreamble = @'
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace ProbePlugin;

[RevelaConfig("plugins:probe")]
internal sealed class ProbeConfig
{
    public const string Section = "plugins:probe";

    public int Retries { get; set; } = 3;
}

internal sealed class ProbeSettings(IPluginSettingsWriter<ProbeConfig> writer)
{
    public Task SaveAsync(int retries, CancellationToken cancellationToken) =>
        writer.WriteAsync(new JsonObject { ["retries"] = retries }, cancellationToken);
}

public sealed class ProbePlugin : IPlugin
{
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Probe.Plugin",
        Name = "Probe",
        Version = "1.0.0",
        Description = "Plugin configuration isolation probe",
    };

'@
    $pluginConsumers = @(
        @{
            Name = 'CompliantPlugin'
            Body = '    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<ProbeConfig>().BindConfiguration(ProbeConfig.Section);
        services.AddTransient<ProbeSettings>();
    }
}'
            ExpectedError = $null
        },
        @{
            Name = 'ConfigurationReaderPlugin'
            Body = '    public void ConfigureServices(IServiceCollection services) =>
        services.AddOptions<ProbeConfig>().BindConfiguration(ProbeConfig.Section);

    internal static string? ReadForeign(Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        configuration["plugins:serve:port"];
}'
            ExpectedError = 'error RS0030'
        },
        @{
            Name = 'ForeignBindingPlugin'
            Body = '    public void ConfigureServices(IServiceCollection services) =>
        services.AddOptions<ProbeConfig>().BindConfiguration("plugins:serve");
}'
            ExpectedError = 'error REVELA003'
        }
    )
    foreach ($consumer in $pluginConsumers) {
        $pluginProject = New-PluginConsumer -Name $consumer.Name -Source ($pluginPreamble + $consumer.Body)
        Invoke-DotNet "$($consumer.Name)-restore" (@('restore', $pluginProject, '--configfile', $nugetConfig) + $isolatedProperties) | Out-Null
        $buildArguments = @('build', $pluginProject, '-c', 'Release', '--no-restore') + $isolatedProperties
        if ($null -eq $consumer.ExpectedError) {
            Invoke-DotNet "$($consumer.Name)-build" $buildArguments | Out-Null
            Write-Host "PASS: $($consumer.Name) builds."
            continue
        }
        $failureOutput = (Invoke-DotNetExpectFailure "$($consumer.Name)-build" $buildArguments) -join [Environment]::NewLine
        if (-not $failureOutput.Contains($consumer.ExpectedError, [StringComparison]::Ordinal)) {
            throw "$($consumer.Name) failed without the expected '$($consumer.ExpectedError)'. Evidence: $RunRoot"
        }
        Write-Host "PASS: $($consumer.Name) fails with $($consumer.ExpectedError)."
    }
    Write-Host "SDK consumer passed. Evidence: $RunRoot"
}
finally {
    Pop-Location
}