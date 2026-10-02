using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Abstractions.Engine;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for the shared <see cref="ISiteValidator"/> and its use as Phase 0
/// of <c>generate all</c> via <see cref="IRevelaEngine.GenerateAllAsync"/>.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class SiteValidatorTests
{
    private static void AddServices(IServiceCollection services)
    {
        services.AddRevelaCommands();
        services.AddGenerateFeature();

        // A resolvable Lumina theme so structural theme/template checks pass.
        services.AddSingleton<ITheme>(new LuminaTheme());

        // The image pipeline step depends on console capabilities; provide a
        // non-interactive stub so the engine (IEnumerable<IPipelineStep>) resolves.
        services.AddSingleton<IConsoleCapabilities>(new NonInteractiveConsole());

        // The host normally populates step order during command registration; supply
        // the production ordering so the engine runs check (50) → scan → pages → images.
        services.AddSingleton<IPipelineStepOrderProvider>(new TestStepOrderProvider());
    }

    [TestMethod]
    public async Task ValidateAsync_GoodProject_ReportsNoErrors()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Good", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Good Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task ValidateAsync_SlugCollisionAndInvalidFrontMatter_ReportsBothInOnePass()
    {
        // Arrange: "01 Events" and "Events" both slugify to "events/"; a third gallery
        // has a broken frontmatter block.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Bad", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Bad Site", author = "Test" })
            .AddGallery("01 Events", g => g.AddImage("a.jpg"))
            .AddGallery("Events", g => g.AddImage("b.jpg")));

        var brokenDir = Path.Combine(project.SourcePath, "Broken");
        Directory.CreateDirectory(brokenDir);
        await File.WriteAllTextAsync(
            Path.Combine(brokenDir, "_index.revela"),
            "+++\ntitle = \"unterminated\n+++\n");

        using var host = RevelaTestHost.Build(project.RootPath, AddServices);
        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act — collect-all: a single pass surfaces every problem.
        var diagnostics = await validator.ValidateAsync();
        var errors = diagnostics.Where(d => d.Severity == ValidationSeverity.Error).ToList();

        // Assert
        Assert.IsTrue(
            errors.Any(d => d.Message.Contains("Slug collision", StringComparison.Ordinal)),
            "Expected a slug-collision error.");
        Assert.IsTrue(
            errors.Any(d => d.Message.Contains("frontmatter", StringComparison.OrdinalIgnoreCase)),
            "Expected an invalid-frontmatter error.");
    }

    [TestMethod]
    public async Task ValidateAsync_EmptySource_WarnsButDoesNotBlock()
    {
        // Arrange: source exists (created by TestProject) but has no galleries/content.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Empty", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Empty Site", author = "Test" }));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert — a warning is surfaced, but it is not an error.
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Warning),
            "Expected an empty-source warning.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task ValidateAsync_MissingSiteTitle_ReportsErrorWithoutThrowing()
    {
        // Arrange: site.json without a title. The requirement moved off the model
        // (SiteCoreConfig.Title is no longer [Required], so the wizard can write
        // site.json incrementally) onto the `check` call site, which must still
        // surface a missing title as a blocking error.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "NoTitle", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("title", StringComparison.OrdinalIgnoreCase)),
            "Expected a blocking error about the missing site title.");
    }

    [TestMethod]
    public async Task ValidateAsync_StrayProjectLanguage_ReportsErrorWithoutThrowing()
    {
        // Arrange: 'language' belongs in site.json now (#75); leaving it in project.json
        // must surface as a friendly error, not an unhandled exception.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Stray", language = "en", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Stray Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("language", StringComparison.OrdinalIgnoreCase)),
            "Expected a configuration error about the stray 'language' key.");
    }

    [TestMethod]
    public async Task ValidateAsync_NoBaseUrl_EmitsHint()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "NoBase" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "No Base Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Hint
                && d.Message.Contains("baseUrl", StringComparison.OrdinalIgnoreCase)),
            "Expected a baseUrl hint.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task GenerateAll_WithSlugCollision_AbortsBeforeImages()
    {
        // Arrange: two galleries whose slugs collide. Validation is no longer a hidden
        // phase, but the scan step still fails on a slug conflict before any rendering,
        // so the expensive image step never runs.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Collision", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Collision Site", author = "Test" })
            .AddGallery("01 Events", g => g.AddRealImage("a.jpg", 640, 480))
            .AddGallery("Events", g => g.AddRealImage("b.jpg", 640, 480)));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var engine = host.Services.GetRequiredService<IRevelaEngine>();

        // Act
        var result = await engine.GenerateAllAsync(progress: null, CancellationToken.None);

        // Assert — pipeline aborted before images.
        Assert.IsFalse(result.Success, "Pipeline should abort on a slug collision.");
        Assert.IsNotNull(result.ErrorMessage);

        var imagesDir = Path.Combine(project.OutputPath, "images");
        Assert.IsFalse(
            Directory.Exists(imagesDir) && Directory.EnumerateFiles(imagesDir, "*", SearchOption.AllDirectories).Any(),
            "Image processing must not have run when the pipeline aborted.");
    }

    [TestMethod]
    public async Task GenerateAll_ValidProject_Builds()
    {
        // Arrange: a clean project. Run the pipeline in order (scan → render → images);
        // there is no longer a hidden check phase.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Valid", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
                generate = new { images = new { avif = 80, webp = 85, jpg = 90 } },
            })
            .WithSiteJson(new { title = "Valid Site", author = "Test" })
            .AddGallery("Landscapes", g => g
                .WithMarkdown("# Landscapes")
                .AddRealImage("sunset.jpg", 640, 480)));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();


        // Act
        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();
        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        Assert.IsTrue(imageResult.Success, $"Images failed: {imageResult.ErrorMessage}");
    }

    [TestMethod]
    public async Task ValidateAsync_PluginCheckError_SurfacedAndBlocksBuild()
    {
        // Arrange: an otherwise-clean project plus a plugin check that reports an error.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Plugin", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Plugin Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));

        var pluginError = ValidationDiagnostic.Error("Plugin precondition failed", file: "data.ics");
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            AddServices(services);
            services.AddSingleton<ICheck>(new FakeCheck([pluginError]));
        });

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert — the plugin diagnostic joins the host's collect-all report.
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("Plugin precondition failed", StringComparison.Ordinal)),
            "Plugin diagnostic should appear in the collect-all report.");
    }

    [TestMethod]
    public async Task ValidateAsync_PluginCheckWarningOnly_SurfacedButDoesNotBlock()
    {
        // Arrange: a plugin check that reports only a warning and a hint.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Plugin", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Plugin Site", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));

        var warning = ValidationDiagnostic.Warning("Plugin note");
        var hint = ValidationDiagnostic.Hint("Plugin hint");
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            AddServices(services);
            services.AddSingleton<ICheck>(new FakeCheck([warning, hint]));
        });

        var validator = host.Services.GetRequiredService<ISiteValidator>();

        // Act
        var diagnostics = await validator.ValidateAsync();

        // Assert — the note is surfaced, but it is not an error.
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Warning
                && d.Message.Contains("Plugin note", StringComparison.Ordinal)),
            "Plugin warning should appear in the collect-all report.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    private sealed class FakeCheck(IReadOnlyList<ValidationDiagnostic> diagnostics) : ICheck
    {
        public string Name => "fake";

        public string Title => "Fake check";

        public ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(diagnostics);
    }

    private sealed class NonInteractiveConsole : IConsoleCapabilities
    {
        public bool IsInteractive => false;

        public bool CanRenderLive => false;
    }

    private sealed class TestStepOrderProvider : IPipelineStepOrderProvider
    {
        public int GetOrder(string category, string name) => name switch
        {
            "scan" => PipelineOrder.Scan,
            "pages" => PipelineOrder.Pages,
            "images" => PipelineOrder.Images,
            _ => int.MaxValue,
        };
    }
}
