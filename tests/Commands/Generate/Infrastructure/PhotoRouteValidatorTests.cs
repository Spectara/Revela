using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Tests.Commands.Generate.Infrastructure;

/// <summary>
/// Tests for <see cref="SlugValidator.FindPhotoConflicts"/> — photo-route collision detection
/// that must fail before any output is written and list every conflicting source (#77).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PhotoRouteValidatorTests
{
    [TestMethod]
    public void FindPhotoConflicts_TwoSourcesFoldToSameSlug_ReportsPhotoCollisionWithAllSources()
    {
        // "café" and "cafe" both fold to the image slug "cafe".
        var galleries = new[]
        {
            Gal("Set", null, Img("_images/café.jpg"), Img("_images/cafe.jpg"))
        };
        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        var conflicts = SlugValidator.FindPhotoConflicts(pages, galleries);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.PhotoCollision, conflict.Kind);
        Assert.AreEqual("photo/cafe", conflict.Slug);
        string[] expected = ["_images/cafe.jpg", "_images/café.jpg"];
        CollectionAssert.AreEquivalent(expected, conflict.Sources.ToList());
    }

    [TestMethod]
    public void FindPhotoConflicts_GalleryUnderPhotoNamespace_ReportsRouteCollision()
    {
        var galleries = new[]
        {
            Gal("Landscapes", null, Img("Landscapes/x.jpg")),
            Gal("photo/foo", null)
        };
        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        var conflicts = SlugValidator.FindPhotoConflicts(pages, galleries);

        var conflict = conflicts.Single(c => c.Kind == SlugConflictKind.PhotoRouteCollision);
        Assert.AreEqual("photo/foo", conflict.Slug);
    }

    [TestMethod]
    public void FindPhotoConflicts_DistinctSlugsAndRoutes_ReturnsEmpty()
    {
        var galleries = new[]
        {
            Gal("Landscapes", null, Img("_images/ocean.jpg"), Img("_images/forest.jpg"))
        };
        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        var conflicts = SlugValidator.FindPhotoConflicts(pages, galleries);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void FormatPhotoRouteError_ListsRouteAndEverySource()
    {
        var galleries = new[]
        {
            Gal("Set", null, Img("_images/café.jpg"), Img("_images/cafe.jpg"))
        };
        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));
        var conflicts = SlugValidator.FindPhotoConflicts(pages, galleries);

        var message = SlugValidator.FormatPhotoRouteError(conflicts);

        Assert.Contains("photo/cafe", message);
        Assert.Contains("_images/cafe.jpg", message);
        Assert.Contains("_images/café.jpg", message);
    }

    private static Image Img(string sourcePath) => new()
    {
        SourcePath = sourcePath,
        FileName = Path.GetFileNameWithoutExtension(sourcePath),
        Slug = UrlBuilder.ToImageSlug(sourcePath),
        Width = 100,
        Height = 100
    };

    private static IReadOnlyList<PhotoMembership> BaseMemberships(IEnumerable<Gallery> galleries) =>
        [.. galleries.Select(gallery => new PhotoMembership(gallery, gallery.Images, null, PhotoViewerMode.Page))];

    private static Gallery Gal(string path, string? template, params Image[] images) => new()
    {
        Path = path,
        Title = path.Length == 0 ? "Site" : path,
        Slug = path.Length == 0 ? UrlBuilder.BuildPath() : UrlBuilder.BuildPath(path.Split('/')),
        Template = template,
        Images = images
    };
}
