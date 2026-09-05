using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Models;

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
    public void Build_BareBlockAlongsideBaseMembership_DoesNotDuplicateContext()
    {
        var image = Img("Gallery/photo.jpg");
        var gallery = Gal("Gallery", null, image);
        var memberships = new[] { new PhotoMembership(gallery, gallery.Images, null, PhotoViewerMode.Page) };

        var pages = PhotoPageCatalog.Build(memberships);

        var context = pages.Single().Contexts.Single();
        Assert.AreEqual("gallery", context.ContextId);
        Assert.AreEqual("photo-gallery-photo", context.Anchor);
    }

    [TestMethod]
    public void Build_FilteredMembership_PreservesFrozenImageOrderForNavigation()
    {
        var first = Img("_images/first.jpg");
        var second = Img("_images/second.jpg");
        var third = Img("_images/third.jpg");
        var gallery = Gal("Featured", null);
        IReadOnlyList<PhotoMembership> memberships =
            [new PhotoMembership(gallery, [third, first, second], 1, PhotoViewerMode.Page)];

        var pages = PhotoPageCatalog.Build(memberships);

        var context = pages.Single(page => page.Slug == "first").Contexts.Single();
        Assert.AreEqual("third", context.PreviousPhoto!.Slug);
        Assert.AreEqual("second", context.NextPhoto!.Slug);
        Assert.AreEqual("featured-grid-1", context.ContextId);
        Assert.AreEqual("grid-1-photo-first", context.Anchor);
    }

    [TestMethod]
    public void Build_FilteredOnlySharedImage_CreatesCanonicalPage()
    {
        var image = Img("_images/filtered-only.jpg");
        var gallery = Gal("Featured", null);
        IReadOnlyList<PhotoMembership> memberships =
            [new PhotoMembership(gallery, [image], 1, PhotoViewerMode.Page)];

        var page = PhotoPageCatalog.Build(memberships).Single();

        Assert.AreEqual("filtered-only", page.Slug);
        Assert.AreEqual("featured/", page.PrimaryContext.Route);
    }

    [TestMethod]
    public void Build_OverlappingFilteredMemberships_UseDistinctContextIdsAndAnchors()
    {
        var image = Img("_images/shared.jpg");
        var gallery = Gal("Featured", null);
        IReadOnlyList<PhotoMembership> memberships =
        [
            new PhotoMembership(gallery, [image], 1, PhotoViewerMode.Page),
            new PhotoMembership(gallery, [image], 2, PhotoViewerMode.Page)
        ];

        var contexts = PhotoPageCatalog.Build(memberships).Single().Contexts;

        Assert.HasCount(2, contexts);
        Assert.AreEqual("featured-grid-1", contexts[0].ContextId);
        Assert.AreEqual("grid-1-photo-shared", contexts[0].Anchor);
        Assert.AreEqual("featured-grid-2", contexts[1].ContextId);
        Assert.AreEqual("grid-2-photo-shared", contexts[1].Anchor);
    }

    [TestMethod]
    public void Build_CustomBodyWithoutMemberships_ContributesNoPageOrContext()
    {
        IReadOnlyList<PhotoMembership> memberships = [];

        var pages = PhotoPageCatalog.Build(memberships);

        Assert.IsEmpty(pages);
    }

    [TestMethod]
    public void Build_SameSharedImageInTwoFilterGalleries_ProducesOnePageWithTwoOrderedContexts()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[]
        {
            Gal("01 Canon", null, shared),
            Gal("02 Sony", null, shared)
        };

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        var page = pages.Single();
        Assert.AreEqual("ocean", page.Slug);
        Assert.HasCount(2, page.Contexts);
        Assert.AreEqual("canon/", page.Contexts[0].Route);
        Assert.AreEqual("sony/", page.Contexts[1].Route);
    }

    [TestMethod]
    public void Build_PrevNext_FollowGalleryImageOrder()
    {
        var a = Img("_images/a.jpg");
        var b = Img("_images/b.jpg");
        var c = Img("_images/c.jpg");
        var galleries = new[] { Gal("Set", null, a, b, c) };

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

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

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

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

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        var page = pages.Single();
        Assert.AreEqual("landscapes/", page.PrimaryContext.Route);
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

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        Assert.AreEqual("canon/", pages.Single().PrimaryContext.Route);
    }

    [TestMethod]
    public void Build_NoneMembership_ProducesNoPageOrContext()
    {
        var shared = Img("_images/ocean.jpg");
        IReadOnlyList<PhotoMembership> memberships =
        [
            new(Gal("Statistics", "statistics/overview", shared), [shared], null, PhotoViewerMode.None)
        ];

        var pages = PhotoPageCatalog.Build(PageMemberships(memberships));

        Assert.IsEmpty(pages);
    }

    [TestMethod]
    public void Build_SameImageInPageAndNoneMemberships_PageHasOnlyPageContext()
    {
        var shared = Img("_images/ocean.jpg");
        IReadOnlyList<PhotoMembership> memberships =
        [
            new(Gal("Canon", null, shared), [shared], null, PhotoViewerMode.Page),
            new(Gal("Statistics", "statistics/overview", shared), [shared], null, PhotoViewerMode.None)
        ];

        var pages = PhotoPageCatalog.Build(PageMemberships(memberships));

        var page = pages.Single();
        var context = page.Contexts.Single();
        Assert.AreEqual("canon/", context.Route);
    }

    [TestMethod]
    public void Build_RootGalleryContext_UsesHomeContextId()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[] { Gal(string.Empty, null, shared) };

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        Assert.AreEqual("home", pages.Single().Contexts.Single().ContextId);
    }

    [TestMethod]
    public void Build_Anchor_UsesPhotoPrefixWithDashes()
    {
        var image = Img("Landscapes/ocean-sunset.jpg");
        var galleries = new[] { Gal("Landscapes", null, image) };

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        Assert.AreEqual("photo-landscapes-ocean-sunset", pages.Single().Contexts.Single().Anchor);
    }

    [TestMethod]
    public void BaseContextId_NonRootGallery_TrimsAndReplacesSeparators() =>
        Assert.AreEqual("trips-italy", PhotoPageCatalog.BaseContextId("trips/italy/"));

    private static IReadOnlyList<PhotoMembership> BaseMemberships(IEnumerable<Gallery> galleries) =>
        [.. galleries.Select(gallery => new PhotoMembership(gallery, gallery.Images, null, PhotoViewerMode.Page))];

    private static IReadOnlyList<PhotoMembership> PageMemberships(IEnumerable<PhotoMembership> memberships) =>
        [.. memberships.Where(membership => membership.ViewerMode is PhotoViewerMode.Page)];

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
