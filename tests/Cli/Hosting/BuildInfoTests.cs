using System.Reflection;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Tests.Cli.Hosting;

[TestClass]
[TestCategory("Unit")]
public sealed class BuildInfoTests
{
    [TestMethod]
    public void FormatVersionLine_Standalone_OmitsHostSuffix()
    {
        var info = new BuildInfo(
            HostKind.Standalone,
            version: "1.2.3",
            informationalVersion: "1.2.3+abc1234",
            framework: ".NET 10.0.4",
            configuration: "Release",
            runtimeIdentifier: "linux-x64");

        var line = info.FormatVersionLine();

        Assert.AreEqual("revela 1.2.3 (.NET 10.0.4)", line);
    }

    [TestMethod]
    public void FormatVersionLine_Embedded_AppendsEmbeddedSuffix()
    {
        var info = new BuildInfo(
            HostKind.Embedded,
            version: "1.2.3",
            informationalVersion: "1.2.3",
            framework: ".NET 10.0.4",
            configuration: "Release",
            runtimeIdentifier: "linux-x64");

        var line = info.FormatVersionLine();

        Assert.AreEqual("revela 1.2.3 (.NET 10.0.4) \u2014 embedded build", line);
    }

    [TestMethod]
    [DataRow("1.0.0", "1.0.0")]
    [DataRow("1.0.0+abc1234", "1.0.0")]
    [DataRow("1.0.0-rc.1+abc1234", "1.0.0-rc.1")]
    [DataRow("2.5.0+", "2.5.0")]
    public void StripBuildMetadata_RemovesEverythingFromFirstPlus(string input, string expected)
    {
        var actual = BuildInfo.StripBuildMetadataForTesting(input);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DetectHostKind_AssemblyWithoutMetadata_ReturnsStandalone()
    {
        // The test runner assembly itself has no Revela.HostKind metadata.
        var asm = Assembly.GetExecutingAssembly();

        var kind = BuildInfo.DetectHostKindForTesting(asm);

        Assert.AreEqual(HostKind.Standalone, kind);
    }

    [TestMethod]
    public void DetectHostKind_CliEmbeddedAssembly_ReturnsEmbedded()
    {
        // Locate Cli.Embedded's revela.dll relative to this test assembly.
        // Layout: artifacts/bin/Tests.Cli/{Config}/net10.0/  →  ../../../Cli.Embedded/{Config}/net10.0/revela.dll
        var testAsmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var configuration = Path.GetFileName(Path.GetDirectoryName(testAsmDir))!;
        var embeddedDll = Path.GetFullPath(Path.Combine(
            testAsmDir, "..", "..", "..", "Cli.Embedded", configuration, "net10.0", "revela.dll"));

        if (!File.Exists(embeddedDll))
        {
            Assert.Inconclusive($"Cli.Embedded build artifact not found at '{embeddedDll}'. Run a full solution build first.");
        }

        var asm = Assembly.LoadFile(embeddedDll);

        var kind = BuildInfo.DetectHostKindForTesting(asm);

        Assert.AreEqual(HostKind.Embedded, kind);
    }

    [TestMethod]
    public void Constructor_FromTestRunnerAssembly_FillsAllProperties()
    {
        var info = new BuildInfo(Assembly.GetExecutingAssembly());

        Assert.AreEqual(HostKind.Standalone, info.Kind);
        Assert.IsFalse(string.IsNullOrEmpty(info.Version));
        Assert.IsFalse(string.IsNullOrEmpty(info.Framework));
        Assert.IsFalse(string.IsNullOrEmpty(info.RuntimeIdentifier));
        // Configuration is build-time; either Debug or Release depending on test run config.
        Assert.IsTrue(
            info.Configuration is "Debug" or "Release",
            $"Expected Debug or Release, got '{info.Configuration}'.");
        Assert.IsFalse(info.Version.Contains('+', StringComparison.Ordinal));
    }
}
