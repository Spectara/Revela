using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Core.Services;

/// <summary>
/// Unit tests for the SDK's <see cref="IPathResolver"/> as registered by
/// <see cref="ConfigurationServiceCollectionExtensions.AddRevelaConfigSections"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PathResolverTests
{
    private const string ProjectPath = "/projects/my-site";

    [TestMethod]
    public void SourcePath_RelativePath_ResolvesAgainstProject()
    {
        var resolver = CreateResolver("photos", "output");

        var result = resolver.SourcePath;

        Assert.IsTrue(result.EndsWith("photos", StringComparison.Ordinal),
            $"Expected path ending with 'photos', got: {result}");
    }

    [TestMethod]
    public void OutputPath_RelativePath_ResolvesAgainstProject()
    {
        var resolver = CreateResolver("source", "dist");

        var result = resolver.OutputPath;

        Assert.IsTrue(result.EndsWith("dist", StringComparison.Ordinal),
            $"Expected path ending with 'dist', got: {result}");
    }

    [TestMethod]
    public void SourcePath_AbsolutePath_UsedDirectly()
    {
        // Use a platform-appropriate absolute path
        var absolutePath = OperatingSystem.IsWindows()
            ? @"D:\OneDrive\Photos"
            : "/mnt/photos";

        var resolver = CreateResolver(absolutePath, "output");

        var result = resolver.SourcePath;

        // Absolute path should be used as-is (with normalization)
        Assert.IsTrue(Path.IsPathRooted(result), "Result should be an absolute path");
        Assert.IsTrue(result.Contains("Photos", StringComparison.OrdinalIgnoreCase) ||
                      result.Contains("photos", StringComparison.OrdinalIgnoreCase),
            $"Expected absolute path to contain 'photos', got: {result}");
    }

    [TestMethod]
    public void OutputPath_AbsolutePath_UsedDirectly()
    {
        var absolutePath = OperatingSystem.IsWindows()
            ? @"C:\www\output"
            : "/var/www/html";

        var resolver = CreateResolver("source", absolutePath);

        var result = resolver.OutputPath;

        Assert.IsTrue(Path.IsPathRooted(result));
    }

    [TestMethod]
    public void DefaultPaths_AreSourceAndOutput()
    {
        var resolver = CreateResolver("source", "output");

        Assert.IsTrue(resolver.SourcePath.EndsWith("source", StringComparison.Ordinal));
        Assert.IsTrue(resolver.OutputPath.EndsWith("output", StringComparison.Ordinal));
    }

    private static IPathResolver CreateResolver(string source, string output)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{PathsConfig.Section}:{nameof(PathsConfig.Source)}"] = source,
                [$"{PathsConfig.Section}:{nameof(PathsConfig.Output)}"] = output
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Options.Create(new ProjectEnvironment { Path = ProjectPath }));
        services.AddRevelaConfigSections();

        return services.BuildServiceProvider().GetRequiredService<IPathResolver>();
    }
}
