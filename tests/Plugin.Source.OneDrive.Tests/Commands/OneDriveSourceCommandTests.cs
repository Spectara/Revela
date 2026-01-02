using NSubstitute;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Commands;

[TestClass]
public sealed class OneDriveSourceCommandTests
{
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

    private static OneDrive.Commands.OneDriveSourceCommand CreateCommand()
    {
        var commandLogger = Substitute.For<ILogger<OneDrive.Commands.OneDriveSourceCommand>>();
        var providerLogger = Substitute.For<ILogger<OneDrive.Providers.SharedLinkProvider>>();
        var handler = new HttpClientHandler
        {
            CheckCertificateRevocationList = true
        };
        var httpClient = new HttpClient(handler);
        var provider = new OneDrive.Providers.SharedLinkProvider(httpClient, providerLogger);
        var projectEnvironment = Microsoft.Extensions.Options.Options.Create(new Sdk.ProjectEnvironment { Path = Path.GetTempPath() });
        var configMonitor = Substitute.For<Microsoft.Extensions.Options.IOptionsMonitor<OneDrive.Configuration.OneDrivePluginConfig>>();
        configMonitor.CurrentValue.Returns(new OneDrive.Configuration.OneDrivePluginConfig());

        return new OneDrive.Commands.OneDriveSourceCommand(commandLogger, provider, projectEnvironment, configMonitor);
    }
}
