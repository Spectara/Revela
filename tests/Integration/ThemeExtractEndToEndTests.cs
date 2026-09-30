using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Extracts the bundled Lumina theme into the project and generates the site with it,
/// proving the extracted folder is a usable local theme (not just a copy of files).
/// </summary>
[TestClass]
[TestCategory("E2E")]
public sealed class ThemeExtractEndToEndTests
{
    private const string CustomImagesJson = /*lang=json,strict*/ """{ "sizes": [320] }""";

    [TestMethod]
    [DataRow("MyTheme")]
    [DataRow("Lumina")]
    public async Task ExtractAsync_ThenGenerate_UsesExtractedLocalTheme(string targetName)
    {
        using var project = TestProject.Create(builder => builder
            .WithProjectJson(new { project = new { name = "Extracted" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Extracted", author = "Test" })
            .AddGallery("Photos", gallery => gallery.AddRealImage("one.jpg", 800, 600)));
        var themePath = Path.Combine(project.RootPath, ProjectPaths.Themes, targetName);

        using (var extractHost = BuildHost(project.RootPath))
        {
            var extract = await extractHost.Services.GetRequiredService<IThemeService>()
                .ExtractAsync("Lumina", targetName);
            Assert.IsTrue(extract.Success, extract.ErrorMessage);
        }

        Assert.IsTrue(File.Exists(Path.Combine(themePath, "theme.json")), "Extracted theme must have an editable theme.json");
        Assert.IsFalse(File.Exists(Path.Combine(themePath, "manifest.json")), "The bundled manifest.json must not be left behind");

        await File.WriteAllTextAsync(
            Path.Combine(project.RootPath, "project.json"),
            $$"""{ "project": { "name": "Extracted" }, "theme": { "name": "{{targetName}}" } }""");
        await File.WriteAllTextAsync(Path.Combine(themePath, "Configuration", "images.json"), CustomImagesJson);
        using var host = BuildHost(project.RootPath);

        var resolved = host.Services.GetRequiredService<IThemeRegistry>().Resolve(targetName, project.RootPath);
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();

        Assert.IsNotNull(resolved);
        Assert.AreEqual(targetName, resolved.Metadata.Name);
        Assert.IsNull(resolved.Prefix, "Extracted theme must resolve as a base theme");
        Assert.AreEqual("320", string.Join(",", host.Services.GetRequiredService<IImageSizesProvider>().GetSizes()));
        Assert.IsTrue(scan.Success, scan.ErrorMessage);
        Assert.IsTrue(render.Success, render.ErrorMessage);
        var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "photos", "index.html"));
        Assert.Contains("_assets/main.css", html);
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "_assets", "main.css")));
    }

    [TestMethod]
    public async Task PartialImagesJsonOverride_InstalledTheme_UsesLocalSizes()
    {
        using var project = TestProject.Create(builder => builder
            .WithProjectJson(new { project = new { name = "Override" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Override", author = "Test" })
            .AddGallery("Photos", gallery => gallery.AddRealImage("one.jpg", 800, 600)));
        var configurationPath = Path.Combine(project.RootPath, ProjectPaths.Themes, "Lumina", "Configuration");
        Directory.CreateDirectory(configurationPath);
        await File.WriteAllTextAsync(Path.Combine(configurationPath, "images.json"), CustomImagesJson);
        using var host = BuildHost(project.RootPath);

        var sizes = host.Services.GetRequiredService<IImageSizesProvider>().GetSizes();
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.AreEqual("320", string.Join(",", sizes));
        Assert.IsTrue(scan.Success, scan.ErrorMessage);
    }

    private static IHost BuildHost(string projectPath) =>
        RevelaTestHost.Build(projectPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IPackageContext, EmptyPackageContext>();
        });

    private sealed class EmptyPackageContext : IPackageContext
    {
        public IReadOnlyList<LoadedPluginInfo> Plugins => [];

        public IReadOnlyList<LoadedThemeInfo> Themes => [];

        public void RegisterCommands(RootCommand rootCommand, IServiceProvider services, CommandRegisteredCallback? onCommandRegistered = null)
        {
        }
    }
}
