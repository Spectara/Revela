using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NuGetSourceManager"/>
/// </summary>
/// <remarks>
/// Tests that access GlobalConfigManager (file I/O) are marked with DoNotParallelize
/// to avoid race conditions when multiple tests try to create/access revela.json simultaneously.
/// </remarks>
[TestClass]
[TestCategory("Unit")]
public sealed class NuGetSourceManagerTests
{
    private NuGetSourceManager service = null!;
    private IOptionsMonitor<PackagesConfig> packagesConfig = null!;

    [TestInitialize]
    public void Setup()
    {
        packagesConfig = Substitute.For<IOptionsMonitor<PackagesConfig>>();
        packagesConfig.CurrentValue.Returns(new PackagesConfig());
        service = new NuGetSourceManager(NullLogger<NuGetSourceManager>.Instance, packagesConfig);
    }

    #region Static Properties Tests

    [TestMethod]
    public void DefaultSource_IsNuGetOrg()
    {
        // Arrange & Act
        var source = NuGetSourceManager.DefaultSource;

        // Assert
        Assert.AreEqual("nuget.org", source.Name);
        Assert.AreEqual("https://api.nuget.org/v3/index.json", source.Url);
        Assert.IsTrue(source.Enabled);
    }

    [TestMethod]
    public void ConfigFilePath_IsNotEmpty()
    {
        // Arrange & Act
        var path = NuGetSourceManager.ConfigFilePath;

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(path));
        Assert.IsTrue(path.EndsWith("revela.json", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region File I/O Tests (sequential to avoid race conditions)

    [TestMethod]
    [DoNotParallelize]
    public async Task LoadSourcesAsync_AlwaysIncludesNuGetOrg()
    {
        // Act
        var sources = await service.LoadSourcesAsync();

        // Assert
        Assert.IsNotEmpty(sources);
        var nugetOrg = sources.FirstOrDefault(s => s.Name == "nuget.org");
        Assert.IsNotNull(nugetOrg);
        Assert.AreEqual("https://api.nuget.org/v3/index.json", nugetOrg.Url);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task GetAllSourcesWithLocationAsync_NuGetOrgIsBuiltIn()
    {
        // Act
        var sources = await service.GetAllSourcesWithLocationAsync();

        // Assert - nuget.org is always first and built-in
        // Find the source and verify in one step to avoid trivially-true assertion warning
        var (_, location) = sources.Single(s => s.Source.Name == "nuget.org");
        Assert.AreEqual("built-in", location);
    }

    [TestMethod]
    public async Task RemoveSourceAsync_ThrowsForNuGetOrg()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await service.RemoveSourceAsync("nuget.org"));
    }

    [TestMethod]
    public async Task RemoveSourceAsync_ThrowsForNuGetOrgCaseInsensitive()
    {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await service.RemoveSourceAsync("NuGet.Org"));
    }

    #endregion

    #region Path Resolution Tests (using reflection to test private method)

    [TestMethod]
    public void ResolvePathIfRelative_HttpUrl_ReturnsUnchanged()
    {
        // Arrange
        var url = "https://api.nuget.org/v3/index.json";

        // Act
        var result = InvokeResolvePathIfRelative(url);

        // Assert
        Assert.AreEqual(url, result);
    }

    [TestMethod]
    public void ResolvePathIfRelative_HttpsUrl_ReturnsUnchanged()
    {
        // Arrange
        var url = "http://my-nuget-server.local/v3/index.json";

        // Act
        var result = InvokeResolvePathIfRelative(url);

        // Assert
        Assert.AreEqual(url, result);
    }

    [TestMethod]
    public void ResolvePathIfRelative_AbsoluteWindowsPath_ReturnsUnchanged()
    {
        // Arrange
        var path = @"C:\NuGet\packages";

        // Act
        var result = InvokeResolvePathIfRelative(path);

        // Assert
        // On Windows, this is recognized as an absolute path and returned unchanged
        // On Linux/macOS, Windows paths are NOT recognized as absolute (no drive letters),
        // so they get resolved relative to the config directory - this is expected behavior
        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(path, result);
        }
        else
        {
            // On Unix, the path will be treated as relative and resolved
            Assert.IsTrue(Path.IsPathRooted(result), $"Expected rooted path, got: {result}");
            Assert.IsTrue(result.Contains("NuGet", StringComparison.Ordinal), $"Expected path containing 'NuGet', got: {result}");
        }
    }

    [TestMethod]
    public void ResolvePathIfRelative_AbsoluteUnixPath_ReturnsUnchanged()
    {
        // Arrange
        var path = "/home/user/nuget/packages";

        // Act
        var result = InvokeResolvePathIfRelative(path);

        // Assert
        // On Windows, Unix paths may not be recognized as rooted
        // On Unix, this should return unchanged
        if (OperatingSystem.IsWindows())
        {
            // On Windows, /path is not rooted, so it will be resolved relative to config
            // This is expected behavior - Unix paths on Windows are treated as relative
            Assert.IsTrue(result.EndsWith("nuget\\packages", StringComparison.OrdinalIgnoreCase)
                || result.Contains("home", StringComparison.Ordinal));
        }
        else
        {
            Assert.AreEqual(path, result);
        }
    }

    [TestMethod]
    public void ResolvePathIfRelative_RelativePath_ResolvesToAbsolute()
    {
        // Arrange
        var relativePath = "../plugins";

        // Act
        var result = InvokeResolvePathIfRelative(relativePath);

        // Assert
        Assert.IsTrue(Path.IsPathRooted(result), $"Expected rooted path, got: {result}");
        Assert.DoesNotContain("..", result, $"Expected resolved path without '..', got: {result}");
    }

    [TestMethod]
    public void ResolvePathIfRelative_CurrentDirRelative_ResolvesToAbsolute()
    {
        // Arrange
        var relativePath = "./local-packages";

        // Act
        var result = InvokeResolvePathIfRelative(relativePath);

        // Assert
        Assert.IsTrue(Path.IsPathRooted(result), $"Expected rooted path, got: {result}");
        Assert.IsTrue(result.EndsWith("local-packages", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ResolvePathIfRelative_SimpleRelative_ResolvesToAbsolute()
    {
        // Arrange
        var relativePath = "packages";

        // Act
        var result = InvokeResolvePathIfRelative(relativePath);

        // Assert
        Assert.IsTrue(Path.IsPathRooted(result), $"Expected rooted path, got: {result}");
        Assert.IsTrue(result.EndsWith("packages", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Helper method to invoke the private ResolvePathIfRelative method via reflection
    /// </summary>
    private string InvokeResolvePathIfRelative(string url)
    {
        var method = typeof(NuGetSourceManager)
            .GetMethod("ResolvePathIfRelative", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(method, "ResolvePathIfRelative method not found");

        var result = method.Invoke(service, [url]);
        Assert.IsNotNull(result);

        return (string)result;
    }

    #endregion
}
