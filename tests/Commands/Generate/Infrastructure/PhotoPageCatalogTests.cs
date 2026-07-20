using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Tests.Commands.Generate.Infrastructure;

/// <summary>
/// Tests for <see cref="PhotoPageCatalog"/> — the render-time aggregate that folds every
/// eligible gallery membership into one canonical photo page per source image (#77).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PhotoPageCatalogTests
{
    [TestMethod]
    public void Build_SameSharedImageInTwoFilterGalleries_ProducesOnePageWithTwoOrderedContexts()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[]
        {
            Gal("01 Canon", null, shared),
            Gal("02 Sony", null, shared)
        };

        var pages = PhotoPageCatalog.Build(galleries);

        var page = pages.Single();
        Assert.AreEqual("ocean", page.Slug);
        Assert.HasCount(2, page.Contexts);
        Assert.AreEqual("canon/", page.Contexts[0].GallerySlug);
        Assert.AreEqual("sony/", page.Contexts[1].GallerySlug);
    }

    [TestMethod]
    public void Build_PrevNext_FollowGalleryImageOrder()
    {
        var a = Img("_images/a.jpg");
        var b = Img("_images/b.jpg");
        var c = Img("_images/c.jpg");
        var galleries = new[] { Gal("Set", null, a, b, c) };

        var pages = PhotoPageCatalog.Build(galleries);

        var middle = pages.Single(p => p.Slug == "b");
        var context = middle.Contexts.Single();
        Assert.AreEqual("a", context.PreviousPhoto!.Slug);
        Assert.AreEqual("c", context.NextPhoto!.Slug);
    }

    [TestMethod]
    public void Build_Boundaries_HaveNoWraparoundAndNeverPointToSelf()
    {
        var a = Img("_images/a.jpg");
        var b = Img("_images/b.jpg");
        var c = Img("_images/c.jpg");
        var galleries = new[] { Gal("Set", null, a, b, c) };

        var pages = PhotoPageCatalog.Build(galleries);

        var first = pages.Single(p => p.Slug == "a").Contexts.Single();
        var last = pages.Single(p => p.Slug == "c").Contexts.Single();
        Assert.IsNull(first.PreviousPhoto);
        Assert.AreEqual("b", first.NextPhoto!.Slug);
        Assert.IsNull(last.NextPhoto);
        Assert.AreEqual("b", last.PreviousPhoto!.Slug);
    }

    [TestMethod]
    public void Build_PhysicalGallery_IsPrimaryContext()
    {
        // The image physically lives in "Landscapes" and is also pulled by a filter gallery.
        var physical = Img("Landscapes/mountain.jpg");
        var galleries = new[]
        {
            Gal("All", null, physical),
            Gal("Landscapes", null, physical)
        };

        var pages = PhotoPageCatalog.Build(galleries);

        var page = pages.Single();
        Assert.AreEqual("landscapes/", page.PrimaryContext.GallerySlug);
        Assert.IsTrue(page.PrimaryContext.IsPhysical);
    }

    [TestMethod]
    public void Build_NoPhysicalGallery_PrimaryIsFirstEligibleInOrder()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[]
        {
            Gal("01 Canon", null, shared),
            Gal("02 Sony", null, shared)
        };

        var pages = PhotoPageCatalog.Build(galleries);

        Assert.AreEqual("canon/", pages.Single().PrimaryContext.GallerySlug);
    }

    [TestMethod]
    public void Build_CustomTemplateGallery_ProducesNoPageOrContext()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[]
        {
            Gal("Statistics", "statistics/overview", shared),
            Gal("Page", "page", shared)
        };

        var pages = PhotoPageCatalog.Build(galleries);

        Assert.IsEmpty(pages);
    }

    [TestMethod]
    public void Build_ImageInBothCustomAndDefaultGallery_PageHasOnlyEligibleContext()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[]
        {
            Gal("Canon", null, shared),
            Gal("Statistics", "statistics/overview", shared)
        };

        var pages = PhotoPageCatalog.Build(galleries);

        var page = pages.Single();
        var context = page.Contexts.Single();
        Assert.AreEqual("canon/", context.GallerySlug);
    }

    [TestMethod]
    public void Build_RootGalleryContext_UsesHomeContextId()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[] { Gal(string.Empty, null, shared) };

        var pages = PhotoPageCatalog.Build(galleries);

        Assert.AreEqual("home", pages.Single().Contexts.Single().ContextId);
    }

    [TestMethod]
    public void Build_Anchor_UsesPhotoPrefixWithDashes()
    {
        var image = Img("Landscapes/ocean-sunset.jpg");
        var galleries = new[] { Gal("Landscapes", null, image) };

        var pages = PhotoPageCatalog.Build(galleries);

        Assert.AreEqual("photo-landscapes-ocean-sunset", pages.Single().Anchor);
    }

    [TestMethod]
    public void IsEligible_DefaultBodies_AreEligible()
    {
        Assert.IsTrue(PhotoPageCatalog.IsEligible(Gal("A", null)));
        Assert.IsTrue(PhotoPageCatalog.IsEligible(Gal("A", "gallery")));
        Assert.IsTrue(PhotoPageCatalog.IsEligible(Gal("A", "body/gallery")));
    }

    [TestMethod]
    public void IsEligible_CustomBodies_AreNotEligible()
    {
        Assert.IsFalse(PhotoPageCatalog.IsEligible(Gal("A", "page")));
        Assert.IsFalse(PhotoPageCatalog.IsEligible(Gal("A", "statistics/overview")));
    }

    [TestMethod]
    public void ContextId_NonRootGallery_ReplacesSeparators() =>
        Assert.AreEqual("trips-italy-", PhotoPageCatalog.ContextId("trips/italy/"));

    private static Image Img(string sourcePath) => new()
    {
        SourcePath = sourcePath,
        FileName = Path.GetFileNameWithoutExtension(sourcePath),
        Slug = UrlBuilder.ToImageSlug(sourcePath),
        Width = 100,
        Height = 100
    };

    private static Gallery Gal(string path, string? template, params Image[] images) => new()
    {
        Path = path,
        Name = path.Length == 0 ? "Home" : path,
        Title = path.Length == 0 ? "Home" : path,
        Slug = path.Length == 0 ? UrlBuilder.BuildPath() : UrlBuilder.BuildPath(path.Split('/')),
        Template = template,
        Images = images
    };
}
