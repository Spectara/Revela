using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Tests.Commands.Generate.Infrastructure;

/// <summary>
/// Tests for <see cref="SlugValidator"/> — scan-time detection of empty or colliding
/// normalized output slugs across galleries and images (#97).
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class SlugValidatorTests
{
    #region Gallery collisions

    [TestMethod]
    public void FindConflicts_DistinctGallerySlugs_ReturnsNoConflicts()
    {
        var galleries = new[] { Gallery("Landscapes"), Gallery("Portraits") };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void FindConflicts_TwoGalleriesNormalizeToSameSlug_ReportsCollisionWithAllSources()
    {
        // "01 Events" and "02 Events" both normalize to "events".
        var galleries = new[] { Gallery("01 Events"), Gallery("02 Events") };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.GalleryCollision, conflict.Kind);
        Assert.AreEqual("events", conflict.Slug);
        string[] expected = ["01 Events", "02 Events"];
        CollectionAssert.AreEquivalent(expected, conflict.Sources.ToList());
    }

    [TestMethod]
    public void FindConflicts_DiacriticFoldingCollision_IsDetected()
    {
        // "Café" and "Cafe" both normalize to "cafe".
        var galleries = new[] { Gallery("Café"), Gallery("Cafe") };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.GalleryCollision, conflict.Kind);
        Assert.AreEqual("cafe", conflict.Slug);
    }

    [TestMethod]
    public void FindConflicts_NestedGalleriesColliding_ComparesFullSlugPath()
    {
        // Distinct parents, so the leaf name alone is not enough — the full path collides.
        var galleries = new[]
        {
            Gallery("Trips"),
            Gallery("Trips/01 Italy"),
            Gallery("Trips/02 Italy")
        };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.GalleryCollision, conflict.Kind);
        Assert.AreEqual("trips/italy", conflict.Slug);
        string[] expected = ["Trips/01 Italy", "Trips/02 Italy"];
        CollectionAssert.AreEquivalent(expected, conflict.Sources.ToList());
    }

    [TestMethod]
    public void FindConflicts_SameLeafUnderDistinctParents_DoesNotCollide()
    {
        // Nested leaves share a name but sit under different, distinct parent slugs.
        var galleries = new[]
        {
            Gallery("Europe/Rome"),
            Gallery("Asia/Rome")
        };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        Assert.IsEmpty(conflicts);
    }

    #endregion

    #region Empty slugs

    [TestMethod]
    public void FindConflicts_RootGalleryEmptySlug_IsAllowed()
    {
        // The site root legitimately has an empty slug.
        var galleries = new[] { Gallery(string.Empty), Gallery("Portraits") };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void FindConflicts_NonRootGalleryNormalizesToEmpty_ReportsEmptyConflict()
    {
        // "!!!" consists only of removed characters → empty slug.
        var galleries = new[] { Gallery("!!!") };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.GalleryEmpty, conflict.Kind);
        Assert.Contains("!!!", conflict.Sources);
    }

    [TestMethod]
    public void FindConflicts_EmptyGalleryAlongsideRoot_ReportsOnlyNonRootSource()
    {
        var galleries = new[] { Gallery(string.Empty), Gallery("???") };

        var conflicts = SlugValidator.FindConflicts(galleries, []);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.GalleryEmpty, conflict.Kind);
        string[] expected = ["???"];
        CollectionAssert.AreEqual(expected, conflict.Sources.ToList());
    }

    #endregion

    #region Image collisions

    [TestMethod]
    public void FindConflicts_IdenticalFilenamesUnderDistinctGallerySlugs_DoesNotCollide()
    {
        // #51: identical filenames under genuinely distinct gallery slugs stay valid.
        var images = new[]
        {
            Image("Landscapes/mountain.jpg"),
            Image("Portraits/mountain.jpg")
        };

        var conflicts = SlugValidator.FindConflicts([], images);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void FindConflicts_SameFilenameUnderCollidingGallerySlugs_ReportsImageCollision()
    {
        // "01 Events" and "02 Events" both fold to "events", so the images collide too.
        var images = new[]
        {
            Image("01 Events/photo.jpg"),
            Image("02 Events/photo.jpg")
        };

        var conflicts = SlugValidator.FindConflicts([], images);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.ImageCollision, conflict.Kind);
        Assert.AreEqual("events/photo", conflict.Slug);
        string[] expected = ["01 Events/photo.jpg", "02 Events/photo.jpg"];
        CollectionAssert.AreEquivalent(expected, conflict.Sources.ToList());
    }

    [TestMethod]
    public void FindConflicts_ImageNormalizesToEmptyOutputPath_ReportsImageEmpty()
    {
        var images = new[] { Image("!!!.jpg") };

        var conflicts = SlugValidator.FindConflicts([], images);

        var conflict = conflicts.Single();
        Assert.AreEqual(SlugConflictKind.ImageEmpty, conflict.Kind);
        Assert.Contains("!!!.jpg", conflict.Sources);
    }

    #endregion

    #region Aggregation & formatting

    [TestMethod]
    public void FindConflicts_MultipleProblems_AreAllAggregated()
    {
        var galleries = new[] { Gallery("01 Events"), Gallery("02 Events") };
        var images = new[] { Image("!!!.jpg") };

        var conflicts = SlugValidator.FindConflicts(galleries, images);

        Assert.HasCount(2, conflicts);
    }

    [TestMethod]
    public void FormatScanError_ListsSlugAndEveryConflictingSource()
    {
        var galleries = new[] { Gallery("01 Events"), Gallery("02 Events") };
        var conflicts = SlugValidator.FindConflicts(galleries, []);

        var message = SlugValidator.FormatScanError(conflicts);

        Assert.Contains("events", message);
        Assert.Contains("01 Events", message);
        Assert.Contains("02 Events", message);
    }

    #endregion

    private static Gallery Gallery(string path) => new()
    {
        Name = path.Length == 0 ? "Home" : path,
        Path = path,
        Slug = path.Length == 0
            ? UrlBuilder.BuildPath()
            : UrlBuilder.BuildPath(path.Split('/'))
    };

    private static SourceImage Image(string relativePath) => new()
    {
        SourcePath = "/src/" + relativePath,
        RelativePath = relativePath,
        FileName = Path.GetFileName(relativePath),
        FileSize = 4,
        LastModified = DateTime.UnixEpoch,
        Gallery = string.Empty
    };
}
