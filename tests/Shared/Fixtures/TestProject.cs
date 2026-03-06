using System.Text.Json;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// Creates a temporary Revela project directory for integration testing.
/// </summary>
/// <remarks>
/// <para>
/// Provides a fluent builder to set up realistic project structures with
/// galleries, images, markdown, and configuration files. All files are
/// created on the real filesystem in a temporary directory.
/// </para>
/// <para>
/// Usage:
/// </para>
/// <code>
/// using var project = TestProject.Create(p => p
///     .WithProjectJson(new { project = new { name = "Test" } })
///     .AddGallery("Landscapes", g => g
///         .AddImage("sunset.jpg", 1920, 1080)
///         .WithMarkdown("# Landscapes")));
///
/// // project.RootPath → temp directory with full structure
/// </code>
/// </remarks>
public sealed class TestProject : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Root path of the temporary project directory.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Path to the source directory (where galleries live).
    /// </summary>
    public string SourcePath => Path.Combine(RootPath, "source");

    /// <summary>
    /// Path to the output directory.
    /// </summary>
    public string OutputPath => Path.Combine(RootPath, "output");

    /// <summary>
    /// Path to project.json.
    /// </summary>
    public string ProjectJsonPath => Path.Combine(RootPath, "project.json");

    private TestProject(string rootPath) => RootPath = rootPath;

    /// <summary>
    /// Creates a new test project using the fluent builder.
    /// </summary>
    /// <param name="configure">Builder configuration action.</param>
    /// <returns>A disposable TestProject with files on disk.</returns>
    public static TestProject Create(Action<ProjectBuilder>? configure = null)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "revela-test-" + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(tempDir);

        var builder = new ProjectBuilder(tempDir);
        configure?.Invoke(builder);
        builder.Build();

        return new TestProject(tempDir);
    }

    /// <summary>
    /// Creates a minimal project with just a project.json.
    /// </summary>
    public static TestProject CreateMinimal()
    {
        return Create(p => p.WithProjectJson(new
        {
            project = new { name = "Test Project" },
            theme = new { name = "Lumina" }
        }));
    }

    /// <summary>
    /// Cleans up the temporary directory.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — temp directory will be cleaned by OS
        }
    }

    /// <summary>
    /// Fluent builder for constructing test project structures.
    /// </summary>
    public sealed class ProjectBuilder
    {
        private readonly string rootPath;
        private readonly string sourcePath;
        private readonly List<GalleryBuilder> galleries = [];
        private object? projectJsonContent;
        private object? siteJsonContent;

        internal ProjectBuilder(string rootPath)
        {
            this.rootPath = rootPath;
            sourcePath = Path.Combine(rootPath, "source");
        }

        /// <summary>
        /// Sets the project.json content.
        /// </summary>
        /// <param name="content">Anonymous object that will be serialized to JSON.</param>
        public ProjectBuilder WithProjectJson(object content)
        {
            projectJsonContent = content;
            return this;
        }

        /// <summary>
        /// Sets the site.json content.
        /// </summary>
        /// <param name="content">Anonymous object that will be serialized to JSON.</param>
        public ProjectBuilder WithSiteJson(object content)
        {
            siteJsonContent = content;
            return this;
        }

        /// <summary>
        /// Adds a gallery with optional configuration.
        /// </summary>
        /// <param name="name">Gallery directory name.</param>
        /// <param name="configure">Optional gallery configuration.</param>
        public ProjectBuilder AddGallery(string name, Action<GalleryBuilder>? configure = null)
        {
            var gallery = new GalleryBuilder(sourcePath, name);
            configure?.Invoke(gallery);
            galleries.Add(gallery);
            return this;
        }

        internal void Build()
        {
            Directory.CreateDirectory(sourcePath);

            // Write project.json (always — every project needs one)
            var json = projectJsonContent ?? new
            {
                project = new { name = "Test Project" },
                theme = new
                {
                    name = "Lumina",
                    images = new { sizes = new List<int> { 320, 640, 1280, 1920 } }
                }
            };
            File.WriteAllText(
                Path.Combine(rootPath, "project.json"),
                JsonSerializer.Serialize(json, JsonOptions));

            // Write site.json if specified
            if (siteJsonContent is not null)
            {
                File.WriteAllText(
                    Path.Combine(rootPath, "site.json"),
                    JsonSerializer.Serialize(siteJsonContent, JsonOptions));
            }

            // Build galleries
            foreach (var gallery in galleries)
            {
                gallery.Build();
            }
        }
    }

    /// <summary>
    /// Builder for creating gallery directories with images and markdown.
    /// </summary>
    public sealed class GalleryBuilder
    {
        private readonly string galleryPath;
        private readonly List<(string name, int width, int height)> images = [];
        private string? markdownContent;

        internal GalleryBuilder(string sourcePath, string name) => galleryPath = Path.Combine(sourcePath, name);

        /// <summary>
        /// Adds a test image to the gallery.
        /// </summary>
        /// <remarks>
        /// Creates a minimal valid JPEG file (not a real image, but enough
        /// for scanning and metadata tests). For image processing tests
        /// that need real pixels, use TestDataHelper.RequireTestImage.
        /// </remarks>
        /// <param name="filename">Image filename (e.g., "sunset.jpg").</param>
        /// <param name="width">Reported width (stored in filename pattern, not EXIF).</param>
        /// <param name="height">Reported height (stored in filename pattern, not EXIF).</param>
        public GalleryBuilder AddImage(string filename = "test.jpg", int width = 1920, int height = 1080)
        {
            images.Add((filename, width, height));
            return this;
        }

        /// <summary>
        /// Adds multiple test images with sequential names.
        /// </summary>
        /// <param name="count">Number of images to create.</param>
        /// <param name="prefix">Filename prefix (default: "img").</param>
        public GalleryBuilder AddImages(int count, string prefix = "img")
        {
            for (var i = 1; i <= count; i++)
            {
                images.Add(($"{prefix}{i}.jpg", 1920, 1080));
            }

            return this;
        }

        /// <summary>
        /// Sets the gallery markdown content (index.md).
        /// </summary>
        /// <param name="content">Markdown content string.</param>
        public GalleryBuilder WithMarkdown(string content)
        {
            markdownContent = content;
            return this;
        }

        internal void Build()
        {
            Directory.CreateDirectory(galleryPath);

            // Write markdown file
            if (markdownContent is not null)
            {
                File.WriteAllText(
                    Path.Combine(galleryPath, "index.md"),
                    markdownContent);
            }

            // Create minimal JPEG files
            foreach (var (name, _, _) in images)
            {
                CreateMinimalJpeg(Path.Combine(galleryPath, name));
            }
        }

        /// <summary>
        /// Creates a minimal valid JPEG file (smallest possible: 2 bytes SOI + 2 bytes EOI).
        /// </summary>
        private static void CreateMinimalJpeg(string path)
        {
            // Minimal JPEG: SOI marker (FF D8) + EOI marker (FF D9)
            ReadOnlySpan<byte> minimalJpeg = [0xFF, 0xD8, 0xFF, 0xD9];
            File.WriteAllBytes(path, minimalJpeg.ToArray());
        }
    }
}
