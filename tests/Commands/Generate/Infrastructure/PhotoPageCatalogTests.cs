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
    private static readonly string[] CollisionSlugs =
    [
        string.Empty, "home", "a/b", "a-b", "a_b", "a%2fb", "a%2Fb", "a%b",
        "r", "g-0061", "r-grid-1", "home-grid-1", "a/b-grid-1", "a-b-grid-1",
        "grid-1", "photo-i-0061", "a\\b", " a ", "a//b", "\u00e9", "e\u0301", "\ud83d\ude00"
    ];

    [TestMethod]
    public void Build_CollidingGallerySlugs_PreservesEveryDistinctBaseAndGridContext()
    {
        var shared = Img("_images/shared.jpg");
        var memberships = CollisionSlugs.SelectMany(slug =>
        {
            var gallery = new Gallery
            {
                Path = slug,
                Name = slug,
                Slug = slug.Length == 0 ? string.Empty : $"{slug}/",
                Images = [shared]
            };
            return new int?[] { null, 1, 12 }.Select(gridNumber =>
                new PhotoMembership(gallery, gallery.Images, gridNumber, PhotoViewerMode.Page));
        }).ToArray();

        var page = PhotoPageCatalog.Build(memberships).Single();

        Assert.HasCount(memberships.Length, page.Contexts);
        CollectionAssert.AreEqual(
            memberships.Select(membership => membership.Gallery.Slug).ToArray(),
            page.Contexts.Select(context => context.Route).ToArray());
        Assert.AreSame(page.Contexts[0], page.PrimaryContext);
        for (var firstIndex = 0; firstIndex < page.Contexts.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < page.Contexts.Count; secondIndex++)
            {
                Assert.AreNotEqual(page.Contexts[firstIndex].ContextId, page.Contexts[secondIndex].ContextId,
                    $"Contexts for '{page.Contexts[firstIndex].Route}' and '{page.Contexts[secondIndex].Route}' must be distinct.");
            }
        }
    }

    [TestMethod]
    public void Build_CollidingImageSlugs_PreservesOrderAndDistinctAnchorsWithinGallery()
    {
        var images = CollisionSlugs.Select((slug, index) => new Image
        {
            SourcePath = FormattableString.Invariant($"_images/{index}.jpg"),
            FileName = slug,
            Slug = slug,
            Width = 100,
            Height = 100
        }).ToArray();
        var gallery = Gal("Gallery", null, images);
        var memberships = new int?[] { null, 1, 12 }.Select(gridNumber =>
            new PhotoMembership(gallery, images, gridNumber, PhotoViewerMode.Page)).ToArray();

        var pages = PhotoPageCatalog.Build(memberships);

        Assert.HasCount(images.Length, pages);
        CollectionAssert.AreEqual(CollisionSlugs, pages.Select(page => page.Slug).ToArray());
        for (var imageIndex = 0; imageIndex < images.Length; imageIndex++)
        {
            Assert.HasCount(memberships.Length, pages[imageIndex].Contexts);
            foreach (var context in pages[imageIndex].Contexts)
            {
                Assert.AreSame(imageIndex == 0 ? null : images[imageIndex - 1], context.PreviousPhoto);
                Assert.AreSame(imageIndex == images.Length - 1 ? null : images[imageIndex + 1], context.NextPhoto);
            }
        }

        var anchors = pages.SelectMany(page => page.Contexts.Select(context => context.Anchor)).ToArray();
        for (var firstIndex = 0; firstIndex < anchors.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < anchors.Length; secondIndex++)
            {
                Assert.AreNotEqual(anchors[firstIndex], anchors[secondIndex]);
            }
        }
    }

    [TestMethod]
    [DataRow("", "r")]
    [DataRow("///", "r")]
    [DataRow("home/", "g-0068006f006d0065")]
    [DataRow("/a/", "g-0061")]
    [DataRow("a/b/", "g-0061002f0062")]
    [DataRow("a-b/", "g-0061002d0062")]
    [DataRow("\ud83d\ude00", "g-d83dde00")]
    public void BaseContextId_CanonicalSlug_UsesDeclaredEncoding(string slug, string expected) =>
        Assert.AreEqual(expected, PhotoPageCatalog.BaseContextId(slug));

    [TestMethod]
    [DataRow("", null, "photo-i-")]
    [DataRow("/a/b/", null, "photo-i-0061002f0062")]
    [DataRow("a-b", 12, "grid-12-photo-i-0061002d0062")]
    [DataRow("\ud83d\ude00", 1, "grid-1-photo-i-d83dde00")]
    public void Anchor_CanonicalSlug_UsesDeclaredEncoding(string slug, int? gridNumber, string expected) =>
        Assert.AreEqual(expected, PhotoPageCatalog.Anchor(slug, gridNumber));

    [TestMethod]
    public void Build_BareBlockAlongsideBaseMembership_DoesNotDuplicateContext()
    {
        var image = Img("Gallery/photo.jpg");
        var gallery = Gal("Gallery", null, image);
        var memberships = new[] { new PhotoMembership(gallery, gallery.Images, null, PhotoViewerMode.Page) };

        var pages = PhotoPageCatalog.Build(memberships);

        var context = pages.Single().Contexts.Single();
        Assert.AreEqual("g-00670061006c006c006500720079", context.ContextId);
        Assert.AreEqual("photo-i-00670061006c006c006500720079002f00700068006f0074006f", context.Anchor);
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
        Assert.AreEqual("g-00660065006100740075007200650064-grid-1", context.ContextId);
        Assert.AreEqual("grid-1-photo-i-00660069007200730074", context.Anchor);
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
        Assert.AreEqual("g-00660065006100740075007200650064-grid-1", contexts[0].ContextId);
        Assert.AreEqual("grid-1-photo-i-007300680061007200650064", contexts[0].Anchor);
        Assert.AreEqual("g-00660065006100740075007200650064-grid-2", contexts[1].ContextId);
        Assert.AreEqual("grid-2-photo-i-007300680061007200650064", contexts[1].Anchor);
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
    public void Build_RootGalleryContext_UsesRootContextId()
    {
        var shared = Img("_images/ocean.jpg");
        var galleries = new[] { Gal(string.Empty, null, shared) };

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        Assert.AreEqual("r", pages.Single().Contexts.Single().ContextId);
    }

    [TestMethod]
    public void Build_Anchor_UsesEncodedImageSlug()
    {
        var image = Img("Landscapes/ocean-sunset.jpg");
        var galleries = new[] { Gal("Landscapes", null, image) };

        var pages = PhotoPageCatalog.Build(BaseMemberships(galleries));

        Assert.AreEqual("photo-i-006c0061006e0064007300630061007000650073002f006f006300650061006e002d00730075006e007300650074",
            pages.Single().Contexts.Single().Anchor);
    }

    [TestMethod]
    public void BaseContextId_NonRootGallery_TrimsAndEncodesSeparators() =>
        Assert.AreEqual("g-00740072006900700073002f006900740061006c0079", PhotoPageCatalog.BaseContextId("trips/italy/"));

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
