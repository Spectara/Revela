using NSubstitute;
using Spectara.Revela.Plugins.Source.OneDrive.Commands;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;
using Spectara.Revela.Plugins.Source.OneDrive.Providers;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Http;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Commands;

[TestClass]
[TestCategory("Unit")]
public sealed class OneDriveSourceCommandTests : IDisposable
{
    private readonly MockHttpMessageHandler mockHandler = new();
    private readonly HttpClient httpClient;

    public OneDriveSourceCommandTests() =>
        httpClient = new HttpClient(mockHandler);

    public void Dispose()
    {
        httpClient.Dispose();
        mockHandler.Dispose();
    }

    [TestMethod]
    public void Create_ShouldReturnSyncCommand()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Create();

        // Assert
        Assert.AreEqual("sync", result.Name);
        Assert.IsNotNull(result.Description);
    }

    [TestMethod]
    public void Create_ShouldHaveExpectedOptions()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Create();
        var optionNames = result.Options.Select(o => o.Name).ToList();

        // Assert - verify only essential CLI options exist (rest is config-only)
        Assert.Contains("--share-url", optionNames);
        Assert.Contains("--force", optionNames);
        Assert.Contains("--dry-run", optionNames);
        Assert.Contains("--clean", optionNames);

        // Config-only options should NOT be CLI options
        Assert.DoesNotContain("--output", optionNames);
        Assert.DoesNotContain("--include", optionNames);
        Assert.DoesNotContain("--exclude", optionNames);
        Assert.DoesNotContain("--concurrency", optionNames);
        Assert.DoesNotContain("--debug", optionNames);
    }

    private OneDriveSourceCommand CreateCommand()
    {
        var commandLogger = Substitute.For<ILogger<OneDriveSourceCommand>>();
        var providerLogger = Substitute.For<ILogger<SharedLinkProvider>>();
        var provider = new SharedLinkProvider(httpClient, providerLogger);
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(Path.GetTempPath());
        var configMonitor = Substitute.For<Microsoft.Extensions.Options.IOptionsMonitor<OneDrivePluginConfig>>();
        configMonitor.CurrentValue.Returns(new OneDrivePluginConfig());

        return new OneDriveSourceCommand(commandLogger, provider, pathResolver, configMonitor);
    }
}
