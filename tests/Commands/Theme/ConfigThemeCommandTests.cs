using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Tests.Commands.Theme;

[TestClass]
[TestCategory("Unit")]
public sealed class ConfigThemeCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_ViewerWithoutTheme_UpdatesCurrentThemeViewerOnly()
    {
        var (command, themeService) = CreateCommand();

        var exitCode = await command.ExecuteAsync(null, "lightbox", clearViewer: false, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        await themeService.Received(1).UpdateAsync(
            Arg.Is<ThemeUpdateRequest>(request => request.ThemeName == null
                && request.PhotoViewer == PhotoViewerMode.Lightbox
                && !request.ClearPhotoViewer),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_SameThemeWithViewer_ProcessesViewerUpdate()
    {
        var (command, themeService) = CreateCommand();

        var exitCode = await command.ExecuteAsync("Full", "page", clearViewer: false, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        await themeService.Received(1).UpdateAsync(
            Arg.Is<ThemeUpdateRequest>(request => request.ThemeName == "Full"
                && request.PhotoViewer == PhotoViewerMode.Page),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_ViewerWithStaleCurrentTheme_ReturnsErrorWithoutUpdate()
    {
        var (command, themeService) = CreateCommand(currentThemeName: "Missing");

        var exitCode = await command.ExecuteAsync(null, "lightbox", clearViewer: false, CancellationToken.None);

        Assert.AreEqual(1, exitCode);
        await themeService.DidNotReceive().UpdateAsync(
            Arg.Any<ThemeUpdateRequest>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task ExecuteAsync_ViewerWithBlankCurrentTheme_UsesLumina(string currentThemeName)
    {
        var (command, themeService) = CreateCommand(currentThemeName, installedThemeName: "Lumina");

        var exitCode = await command.ExecuteAsync(null, "lightbox", clearViewer: false, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        await themeService.Received(1).UpdateAsync(
            Arg.Is<ThemeUpdateRequest>(request => request.ThemeName == null
                && request.PhotoViewer == PhotoViewerMode.Lightbox),
            Arg.Any<CancellationToken>());
    }

    private static (ConfigThemeCommand Command, IThemeService ThemeService) CreateCommand(
        string currentThemeName = "Full",
        string installedThemeName = "Full")
    {
        var configService = Substitute.For<IConfigService>();
        configService.IsProjectInitialized().Returns(true);

        var capabilities = new PhotoViewerCapabilities
        {
            Supported = [PhotoViewerMode.Page, PhotoViewerMode.Lightbox, PhotoViewerMode.None],
            Default = PhotoViewerMode.Page
        };
        var metadata = new PackageMetadata
        {
            Id = $"Spectara.Revela.Themes.{installedThemeName}",
            Name = installedThemeName,
            Version = "1.0.0",
            Description = "Test theme"
        };
        var themeService = Substitute.For<IThemeService>();
        themeService.GetCurrentTheme().Returns(new ThemeInfoResult
        {
            ThemeName = currentThemeName,
            Source = "local",
            PhotoViewerCapabilities = capabilities
        });
        themeService.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(new ThemeListResult
        {
            Installed =
            [
                new ThemeInfo
                {
                    Metadata = metadata,
                    IsLocal = true,
                    PhotoViewerCapabilities = capabilities
                }
            ]
        });
        themeService.UpdateAsync(Arg.Any<ThemeUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(
            callInfo => new ThemeUpdateResult
            {
                Success = true,
                ThemeName = callInfo.ArgAt<ThemeUpdateRequest>(0).ThemeName ?? installedThemeName
            });

        return (new ConfigThemeCommand(
            NullLogger<ConfigThemeCommand>.Instance,
            configService,
            themeService), themeService);
    }
}
