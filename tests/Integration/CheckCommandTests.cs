using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for the first-class <c>check</c> command group backed by
/// <see cref="ISiteValidator"/> — the bespoke collect-all <c>check all</c> and the
/// host-wrapped single-unit <c>check &lt;name&gt;</c> commands.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CheckCommandTests
{
    private static void AddServices(IServiceCollection services)
    {
        services.AddRevelaCommands();
        services.AddGenerateFeature();
        services.AddSingleton<ITheme>(new LuminaTheme());
        services.AddSingleton<IConsoleCapabilities>(FakeConsoleCapabilities.NonInteractive);
    }

    [TestMethod]
    public async Task CheckAll_GoodProject_ExitsZero()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Good", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Good Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var exitCode = await InvokeAsync(host.Services, cmd => cmd.CreateAll());

        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CheckAll_SlugCollision_ExitsTwo()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Bad", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Bad Site", author = "Test" })
            .AddGallery("01 Events", g => g.AddImage("a.jpg"))
            .AddGallery("Events", g => g.AddImage("b.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var exitCode = await InvokeAsync(host.Services, cmd => cmd.CreateAll());

        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ """{"photoViewers":["none"],"defaultPhotoViewer":"none","stylesheets":["main.css"]}""", "$.stylesheets[0]", null)]
    [DataRow("{}", "photoViewers", "none")]
    public async Task CheckAll_MalformedLocalTheme_ReportsManifestInsteadOfInstalledFallback(string manifest, string expectedDetail, string? viewer)
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Broken Local", baseUrl = "https://example.com" }, theme = new { name = "Lumina", photoViewer = viewer } })
            .WithSiteJson(new { title = "Broken Local" })
            .AddGallery("Events", gallery => gallery.AddImage("one.jpg")));
        var themeDirectory = Path.Combine(project.RootPath, "themes", "Lumina");
        Directory.CreateDirectory(themeDirectory);
        await File.WriteAllTextAsync(Path.Combine(themeDirectory, "theme.json"), manifest);
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);
        var check = host.Services.GetServices<ICheck>().Single(item => item.Name == "theme");

        var diagnostics = await check.ValidateAsync();
        var exitCode = await InvokeAsync(host.Services, command => command.CreateAll());

        Assert.HasCount(1, diagnostics);
        Assert.Contains("theme.json", diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains(expectedDetail, diagnostics[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not installed", diagnostics[0].Message, StringComparison.Ordinal);
        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public async Task CheckTheme_MissingTheme_ExitsTwo()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "NoTheme", baseUrl = "https://example.com" },
                theme = new { name = "DoesNotExist" },
            })
            .WithSiteJson(new { title = "No Theme Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var exitCode = await InvokeAsync(host.Services, cmd => cmd.CreateUnit(
            host.Services.GetServices<ICheck>().Single(c => string.Equals(c.Name, "theme", StringComparison.Ordinal))));

        Assert.AreEqual(2, exitCode);
    }

    private static async Task<int> InvokeAsync(IServiceProvider services, Func<CheckCommand, Command> select)
    {
        var checkCommand = services.GetRequiredService<CheckCommand>();
        var command = select(checkCommand);
        return await command.Parse([]).InvokeAsync();
    }
}
