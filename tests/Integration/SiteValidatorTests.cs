using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Commands;
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

    private static IPipelineStep GetCheckStep(IServiceProvider services) =>
        services.GetRequiredService<ValidateCommand>();

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
    public async Task ValidateAsync_GoodProject_PipelineStepSucceeds()
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

        var step = GetCheckStep(host.Services);

        // Act
        var result = await step.ExecuteAsync();

        // Assert — no errors means the build is allowed to proceed (exit 0 semantics).
        Assert.IsTrue(result.Success);
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
    public async Task ValidateAsync_SlugCollision_PipelineStepFails()
    {
        // Arrange
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

        var step = GetCheckStep(host.Services);

        // Act
        var result = await step.ExecuteAsync();

        // Assert — an error blocks the build (exit 2 semantics).
        Assert.IsFalse(result.Success);
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
        var step = GetCheckStep(host.Services);

        // Act
        var diagnostics = await validator.ValidateAsync();
        var result = await step.ExecuteAsync();

        // Assert — a warning is surfaced, but the build still proceeds (exit 0).
        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Warning),
            "Expected an empty-source warning.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
        Assert.IsTrue(result.Success);
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
    public async Task GenerateAll_WithSlugCollision_AbortsAtCheckBeforeImages()
    {
        // Arrange: a project that fails validation; if check is truly Phase 0, the
        // expensive image step never runs.
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

        // Assert — pipeline aborted at the check step, before images.
        Assert.IsFalse(result.Success, "Pipeline should abort on validation errors.");
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("check", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var imagesDir = Path.Combine(project.OutputPath, "images");
        Assert.IsFalse(
            Directory.Exists(imagesDir) && Directory.EnumerateFiles(imagesDir, "*", SearchOption.AllDirectories).Any(),
            "Image processing must not have run when validation failed.");
    }

    [TestMethod]
    public async Task GenerateAll_ValidProject_ChecksFirstThenBuilds()
    {
        // Arrange: a clean project. Run the check step first (Phase 0), then the rest of
        // the pipeline in order (mirrors the real generate-all sequence, without relying
        // on the order provider that the host populates at command-registration time).
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

        var check = GetCheckStep(host.Services);
        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        renderService.SetTheme(host.Services.GetRequiredService<ITheme>());
        renderService.SetExtensions([]);

        // Act — Phase 0: check must pass before anything expensive happens.
        var checkResult = await check.ExecuteAsync();
        Assert.IsTrue(checkResult.Success, "Check should pass for a clean project.");

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();
        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        Assert.IsTrue(imageResult.Success, $"Images failed: {imageResult.ErrorMessage}");
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
            "check" => PipelineOrder.Validate,
            "scan" => PipelineOrder.Scan,
            "pages" => PipelineOrder.Pages,
            "images" => PipelineOrder.Images,
            _ => int.MaxValue,
        };
    }
}
