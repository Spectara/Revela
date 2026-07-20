using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Spectara.Revela.Core;

namespace Spectara.Revela.Tests.Core;

/// <summary>
/// Parity guard between the two independent encodings of the shared-assembly policy: the runtime
/// loader (<see cref="PackageLoadContext.IsSharedAssembly"/>) and the SDK packaging target
/// (<c>_RevelaIncludePluginDependencies</c> in <c>src/Sdk/build/Spectara.Revela.Sdk.targets</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both sides come from REAL sources: the C# predicate is invoked directly, and the MSBuild filter
/// is executed by running the actual target from the real <c>.targets</c> file (via
/// <c>dotnet msbuild</c>). The names below are only probes — no expected shared/excluded verdict is
/// hand-written here, so this file is NOT a third copy of the policy.
/// </para>
/// <para>
/// "Shared at runtime" is the inverse of "included in the plugin package", so the invariant is:
/// <c>IsSharedAssembly(name) == !packagedNames.Contains(name)</c>.
/// </para>
/// <para>
/// Issue: #101. Regression covered: #27 (Spectre.Console.* must be treated as a wildcard).
/// </para>
/// </remarks>
[TestClass]
public sealed class SharedAssemblyParityTests
{
    /// <summary>
    /// Probe names spanning every policy category. This is the SINGLE place to extend when a new
    /// shared / plugin-specific category is introduced: add a representative assembly name here and
    /// the two real policies decide (and must agree on) its verdict.
    /// </summary>
    private static readonly string[] RepresentativeAssemblyNames =
    [
        // Exact shared third-party
        "System.CommandLine",

        // Spectara.Revela.* -> shared (host provides SDK, Core, Commands)
        "Spectara.Revela.Sdk",
        "Spectara.Revela.Core",
        "Spectara.Revela.Commands",

        // Spectre.Console* -> shared (wildcard regression, #27)
        "Spectre.Console",
        "Spectre.Console.Cli",
        "Spectre.Console.ImageSharp",

        // Normal Microsoft.Extensions.* -> shared (framework plumbing)
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Primitives",

        // Plugin-specific Microsoft.Extensions exceptions -> NOT shared
        "Microsoft.Extensions.Http",
        "Microsoft.Extensions.Http.Resilience",
        "Microsoft.Extensions.Telemetry",
        "Microsoft.Extensions.Compliance.Abstractions",
        "Microsoft.Extensions.Diagnostics.ExceptionSummarization",
        "Microsoft.Extensions.AmbientMetadata.Application",
        "Microsoft.Extensions.DependencyInjection.AutoActivation",
        "Microsoft.Extensions.ObjectPool",

        // Clearly plugin-specific third-party -> NOT shared
        "Polly",
        "Newtonsoft.Json",
        "Markdig",
        "NetVips",
    ];

    /// <summary>
    /// Filenames the REAL packaging target decided to include in the plugin package
    /// (i.e. the assemblies it considers plugin-specific / not host-provided). Computed once by
    /// invoking the actual SDK target on first access.
    /// </summary>
    private static readonly Lazy<FrozenSet<string>> PackagedNames =
        new(() => EvaluatePackagingTarget(RepresentativeAssemblyNames));

    [TestMethod]
    public void RuntimeAndPackagingPolicies_AgreeForEveryRepresentativeAssembly()
    {
        var packaged = PackagedNames.Value;
        var mismatches = new List<string>();

        foreach (var name in RepresentativeAssemblyNames)
        {
            var sharedAtRuntime = PackageLoadContext.IsSharedAssembly(name);
            var excludedFromPackage = !packaged.Contains(name);

            if (sharedAtRuntime != excludedFromPackage)
            {
                mismatches.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: IsSharedAssembly={1} but packaging {2} it (expected the inverse).",
                    name,
                    sharedAtRuntime,
                    excludedFromPackage ? "excludes" : "includes"));
            }
        }

        Assert.IsEmpty(
            mismatches,
            $"Runtime loader and SDK packaging policies diverged:{Environment.NewLine}"
                + string.Join(Environment.NewLine, mismatches));
    }

    /// <summary>
    /// Explicit regression for #27: <c>Spectre.Console.*</c> must be treated as a wildcard on BOTH
    /// sides. A prior divergence matched <c>Spectre.Console</c> exactly in one place, so extensions
    /// such as <c>Spectre.Console.Cli</c> were packaged with plugins and broke type identity.
    /// </summary>
    [TestMethod]
    [DataRow("Spectre.Console")]
    [DataRow("Spectre.Console.Cli")]
    [DataRow("Spectre.Console.ImageSharp")]
    public void SpectreConsoleWildcard_IsSharedOnBothSides_Regression27(string assemblyName)
    {
        Assert.IsTrue(
            PackageLoadContext.IsSharedAssembly(assemblyName),
            $"{assemblyName} must be shared at runtime.");
        Assert.IsFalse(
            PackagedNames.Value.Contains(assemblyName),
            $"{assemblyName} must be EXCLUDED from the plugin package (shared by the host).");
    }

    /// <summary>
    /// Executes the real <c>_RevelaIncludePluginDependencies</c> target from the actual SDK
    /// <c>.targets</c> file over stub DLLs named after each probe, returning the filenames the
    /// target chose to pack (the plugin-specific / non-shared set).
    /// </summary>
    private static FrozenSet<string> EvaluatePackagingTarget(IReadOnlyList<string> names)
    {
        var targetsFile = Path.Combine(RepoRoot(), "src", "Sdk", "build", "Spectara.Revela.Sdk.targets");
        Assert.IsTrue(File.Exists(targetsFile), $"Real SDK targets file not found at '{targetsFile}'.");

        var workDir = Path.Combine(
            Path.GetTempPath(),
            "revela-parity-" + Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(workDir, "out");
        Directory.CreateDirectory(outDir);

        try
        {
            const string pluginAssembly = "PluginUnderTest";
            foreach (var name in names)
            {
                File.WriteAllText(Path.Combine(outDir, name + ".dll"), "stub");
            }

            File.WriteAllText(Path.Combine(outDir, pluginAssembly + ".dll"), "stub");
            File.WriteAllText(Path.Combine(outDir, pluginAssembly + ".deps.json"), "{}");

            var resultFile = Path.Combine(workDir, "packaged.txt");
            var outDirWithSeparator = outDir + Path.DirectorySeparatorChar;
            var projectFile = Path.Combine(workDir, "parity-probe.proj");

            var projectXml = $"""
                <Project>
                  <PropertyGroup>
                    <PackageType>RevelaPlugin</PackageType>
                    <AssemblyName>{pluginAssembly}</AssemblyName>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputPath>{outDirWithSeparator}</OutputPath>
                  </PropertyGroup>
                  <Import Project="{targetsFile}" />
                  <Target Name="DumpPackageFiles" DependsOnTargets="_RevelaIncludePluginDependencies">
                    <WriteLinesToFile File="{resultFile}" Lines="@(_PackageFiles-&gt;'%(Filename)')" Overwrite="true" />
                  </Target>
                </Project>
                """;
            File.WriteAllText(projectFile, projectXml);

            RunDotnetMsBuild(projectFile);

            Assert.IsTrue(File.Exists(resultFile), "Packaging target produced no result file.");

            return File.ReadAllLines(resultFile)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToFrozenSet(StringComparer.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the temporary probe workspace.
            }
        }
    }

    private static void RunDotnetMsBuild(string projectFile)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(projectFile),
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectFile);
        startInfo.ArgumentList.Add("/t:DumpPackageFiles");
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/v:quiet");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start 'dotnet msbuild'.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(
            0,
            process.ExitCode,
            $"'dotnet msbuild' failed (exit {process.ExitCode}):{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
    }

    private static string RepoRoot([CallerFilePath] string thisFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", ".."));
}
