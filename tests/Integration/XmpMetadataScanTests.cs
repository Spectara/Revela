using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Scan tests for descriptive XMP metadata (rating, keywords, title, description)
/// embedded in real JPEGs, as Capture One and Lightroom export it.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class XmpMetadataScanTests
{
    private static void AddServices(IServiceCollection services)
    {
        services.AddRevelaCommands();
        services.AddGenerateFeature();
        services.AddSingleton<ITheme>(new LuminaTheme());
    }

    private static async Task<ImageContent> ScanSingleImageAsync(TestProject project)
    {
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);
        var result = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(result.Success, result.ErrorMessage);
        return host.Services.GetRequiredService<IManifestRepository>().Images.Values.Single();
    }

    [TestMethod]
    public async Task ScanAsync_JpegWithXmp_StoresDescriptiveMetadata()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 800, 600, exif => exif
                .WithXmpMetadata(
                    title: "Abendlicht",
                    description: "Sonnenuntergang am See",
                    keywords: ["Selected", "Startseite"],
                    rating: 4))));

        var image = await ScanSingleImageAsync(project);

        Assert.AreEqual("Abendlicht", image.Title);
        Assert.AreEqual("Sonnenuntergang am See", image.Description);
        Assert.AreEqual("Selected,Startseite", string.Join(',', image.Keywords));
        Assert.AreEqual(4, image.Rating);
    }

    [TestMethod]
    public async Task ScanAsync_CachedManifest_PreservesDescriptiveMetadata()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 800, 600, exif => exif
                .WithXmpMetadata(title: "Abendlicht", keywords: ["Selected"], rating: 5))));
        _ = await ScanSingleImageAsync(project);

        var manifestJson = await File.ReadAllTextAsync(Path.Combine(project.RootPath, ".cache", "manifest.json"));
        var cached = await ScanSingleImageAsync(project);

        Assert.Contains("\"rating\": 5", manifestJson);
        Assert.AreEqual("Abendlicht", cached.Title);
        Assert.AreEqual("Selected", string.Join(',', cached.Keywords));
        Assert.AreEqual(5, cached.Rating);
    }

    [TestMethod]
    public async Task ScanAsync_MalformedXmp_KeepsExifAndIgnoresXmp()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 800, 600, exif => exif
                .WithIso(400)
                .WithXmp("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF><broken"))));

        var image = await ScanSingleImageAsync(project);

        Assert.AreEqual(400, image.Exif?.Iso);
        Assert.IsNull(image.Title);
        Assert.IsEmpty(image.Keywords);
        Assert.IsNull(image.Rating);
    }

    [TestMethod]
    public async Task ScanAsync_XmpAndExifDescription_PrefersXmp()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 800, 600, exif => exif
                .WithDescription("OLYMPUS DIGITAL CAMERA")
                .WithXpTitle("Windows title")
                .WithXmpMetadata(title: "XMP title", description: "XMP description"))));

        var image = await ScanSingleImageAsync(project);

        Assert.AreEqual("XMP title", image.Title);
        Assert.AreEqual("XMP description", image.Description);
    }

    [TestMethod]
    public async Task ScanAsync_ExifOnly_FallsBackToImageDescriptionAndXpTitle()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 800, 600, exif => exif
                .WithDescription("Evening light")
                .WithXpTitle("Windows title"))));

        var image = await ScanSingleImageAsync(project);

        Assert.AreEqual("Windows title", image.Title);
        Assert.AreEqual("Evening light", image.Description);
        Assert.IsEmpty(image.Keywords);
        Assert.IsNull(image.Rating);
    }

    [TestMethod]
    public async Task ScanAsync_GallerySortByRating_OrdersHighestRatingFirstAndUnratedLast()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g
                .AddRealImage("a.jpg", 640, 480, exif => exif.WithXmpMetadata(rating: 2))
                .AddRealImage("b.jpg", 640, 480, exif => exif.WithXmpMetadata(rating: 5))
                .AddRealImage("c.jpg", 640, 480, exif => exif.WithXmpMetadata(rating: 3))
                .AddRealImage("x.jpg", 640, 480)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Photos", "_index.revela"),
            "+++\nsort = \"rating:desc\"\n+++\n");

        using var host = RevelaTestHost.Build(project.RootPath, AddServices);
        var result = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        var gallery = host.Services.GetRequiredService<IManifestRepository>().Root!.Children.Single();
        var order = gallery.Content.OfType<ImageContent>().Select(image => image.Filename).ToArray();
        Assert.AreEqual("b.jpg,c.jpg,a.jpg,x.jpg", string.Join(',', order));
    }

    [TestMethod]
    public async Task ScanAsync_GallerySortByRatingAscending_KeepsUnratedLast()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g
                .AddRealImage("0.jpg", 640, 480)
                .AddRealImage("a.jpg", 640, 480, exif => exif.WithXmpMetadata(rating: 4))
                .AddRealImage("b.jpg", 640, 480, exif => exif.WithXmpMetadata(rating: 1))));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Photos", "_index.revela"),
            "+++\nsort = \"rating:asc\"\n+++\n");

        using var host = RevelaTestHost.Build(project.RootPath, AddServices);
        var result = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        var gallery = host.Services.GetRequiredService<IManifestRepository>().Root!.Children.Single();
        var order = gallery.Content.OfType<ImageContent>().Select(image => image.Filename).ToArray();
        Assert.AreEqual("b.jpg,a.jpg,0.jpg", string.Join(',', order));
    }
}
