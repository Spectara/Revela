using Spectara.Revela.Features.Generate.Filtering;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Tests.Commands.Generate.Filtering;

/// <summary>
/// Tests for the <see cref="FilterService"/> class.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class FilterServiceTests
{
    private static ImageContent CreateTestImage(
        string filename = "test.jpg",
        DateTime? dateTaken = null,
        string? make = null,
        int? iso = null)
    {
        var exif = new ExifData
        {
            Make = make,
            Iso = iso,
            Raw = new Dictionary<string, string>()
        };

        return new ImageContent
        {
            Filename = filename,
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            DateTaken = dateTaken ?? DateTime.Now,
            Exif = exif
        };
    }

    [TestMethod]
    public void Validate_ValidExpression_ReturnsTrue()
    {
        // Act
        var result = FilterService.Validate("filename == 'test.jpg'");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_InvalidExpression_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsExactly<FilterParseException>(() =>
            FilterService.Validate("filename =="));
    }

    [TestMethod]
    public void Validate_EmptyExpression_ReturnsFalse()
    {
        // Act
        var result = FilterService.Validate("");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Validate_WhitespaceExpression_ReturnsFalse()
    {
        // Act
        var result = FilterService.Validate("   ");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryValidate_ValidExpression_ReturnsTrueNoError()
    {
        // Act
        var result = FilterService.TryValidate("filename == 'test.jpg'", out var error);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryValidate_InvalidExpression_ReturnsFalseWithError()
    {
        // Act
        var result = FilterService.TryValidate("filename ==", out var error);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.IsNotEmpty(error);
    }

    [TestMethod]
    public void TryValidate_EmptyExpression_ReturnsFalseWithError()
    {
        // Act
        var result = FilterService.TryValidate("", out var error);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Apply_FiltersImagesCorrectly()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "photo1.jpg", make: "Canon"),
            CreateTestImage(filename: "photo2.jpg", make: "Sony"),
            CreateTestImage(filename: "photo3.jpg", make: "Canon"),
            CreateTestImage(filename: "photo4.jpg", make: "Nikon")
        };

        // Act
        var result = FilterService.Apply(images, "exif.make == 'Canon'").ToList();

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(i => i.Exif?.Make == "Canon"));
    }

    [TestMethod]
    public void Apply_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "photo1.jpg", make: "Canon"),
            CreateTestImage(filename: "photo2.jpg", make: "Sony")
        };

        // Act
        var result = FilterService.Apply(images, "exif.make == 'Nikon'").ToList();

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Apply_AllMatch_ReturnsAll()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "photo1.jpg", dateTaken: new DateTime(2024, 6, 15)),
            CreateTestImage(filename: "photo2.jpg", dateTaken: new DateTime(2024, 3, 10)),
            CreateTestImage(filename: "photo3.jpg", dateTaken: new DateTime(2024, 9, 20))
        };

        // Act
        var result = FilterService.Apply(images, "year(dateTaken) == 2024").ToList();

        // Assert
        Assert.HasCount(3, result);
    }

    [TestMethod]
    public void Apply_ComplexFilter_WorksCorrectly()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "IMG_001.jpg", make: "Canon", dateTaken: new DateTime(2024, 6, 15)),
            CreateTestImage(filename: "IMG_002.jpg", make: "Canon", dateTaken: new DateTime(2023, 6, 15)),
            CreateTestImage(filename: "DSC_001.jpg", make: "Sony", dateTaken: new DateTime(2024, 6, 15)),
            CreateTestImage(filename: "IMG_003.jpg", make: "Nikon", dateTaken: new DateTime(2024, 6, 15))
        };

        // Act - Canon images from 2024 OR Sony images
        var result = FilterService.Apply(images,
            "(exif.make == 'Canon' and year(dateTaken) == 2024) or exif.make == 'Sony'").ToList();

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(i => i.Filename == "IMG_001.jpg"));
        Assert.IsTrue(result.Any(i => i.Filename == "DSC_001.jpg"));
    }

    [TestMethod]
    public void Apply_PreservesOrder()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "c.jpg"),
            CreateTestImage(filename: "a.jpg"),
            CreateTestImage(filename: "b.jpg")
        };

        // Act
        var result = FilterService.Apply(images, "contains(filename, '.jpg')").ToList();

        // Assert
        Assert.AreEqual("c.jpg", result[0].Filename);
        Assert.AreEqual("a.jpg", result[1].Filename);
        Assert.AreEqual("b.jpg", result[2].Filename);
    }

    [TestMethod]
    public void Compile_ThrowsOnNullOrEmpty()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => FilterService.Compile(null!));
        Assert.ThrowsExactly<ArgumentException>(() => FilterService.Compile(""));
        Assert.ThrowsExactly<ArgumentException>(() => FilterService.Compile("   "));
    }

    [TestMethod]
    public void Apply_ThrowsOnNullImages()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            FilterService.Apply(null!, "filename == 'test.jpg'"));
    }

    [TestMethod]
    public void Apply_ThrowsOnNullOrEmptyFilter()
    {
        // Arrange
        var images = new List<ImageContent>();

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            FilterService.Apply(images, null!));
        Assert.ThrowsExactly<ArgumentException>(() =>
            FilterService.Apply(images, ""));
    }

    [TestMethod]
    public void CompileToExpression_ReturnsLinqExpression()
    {
        // Act
        var expression = FilterService.CompileToExpression("filename == 'test.jpg'");

        // Assert
        Assert.IsNotNull(expression);
        Assert.IsInstanceOfType<System.Linq.Expressions.LambdaExpression>(expression);
    }

    [TestMethod]
    public void FilterParseException_ContainsDetailedMessage()
    {
        // Act
        FilterService.TryValidate("filename == ", out var error);

        // Assert
        Assert.IsNotNull(error);
        // Should contain the original filter and point to the error position
        Assert.Contains("filename", error);
    }

    [TestMethod]
    public void ApplyQuery_ExplicitSort_OverridesPageAndGlobalSort()
    {
        // Arrange
        var images = new[]
        {
            CreateTestImage("alpha.jpg", new DateTime(2024, 1, 1), iso: 100),
            CreateTestImage("bravo.jpg", new DateTime(2025, 1, 1), iso: 400),
            CreateTestImage("charlie.jpg", new DateTime(2026, 1, 1), iso: 200)
        };
        var globalSort = new ImageSortConfig
        {
            Field = "dateTaken",
            Direction = SortDirection.Desc,
            Fallback = "filename"
        };

        // Act
        var result = FilterService.ApplyQuery(
            images,
            "all | sort exif.iso asc",
            "filename:desc",
            globalSort).ToList();

        // Assert
        Assert.AreEqual("alpha.jpg", result[0].Filename);
        Assert.AreEqual("charlie.jpg", result[1].Filename);
        Assert.AreEqual("bravo.jpg", result[2].Filename);
    }

    [TestMethod]
    public void ApplyQuery_PageSort_OverridesGlobalSortAndUsesGlobalDirectionForInvalidDirection()
    {
        // Arrange
        var images = new[]
        {
            CreateTestImage("alpha.jpg", new DateTime(2024, 1, 1), iso: 100),
            CreateTestImage("bravo.jpg", new DateTime(2026, 1, 1), iso: 400),
            CreateTestImage("charlie.jpg", new DateTime(2025, 1, 1), iso: 200)
        };
        var globalSort = new ImageSortConfig
        {
            Field = "dateTaken",
            Direction = SortDirection.Desc,
            Fallback = "filename"
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all", "exif.iso:sideways", globalSort).ToList();

        // Assert
        Assert.AreEqual("bravo.jpg", result[0].Filename);
        Assert.AreEqual("charlie.jpg", result[1].Filename);
        Assert.AreEqual("alpha.jpg", result[2].Filename);
    }

    [TestMethod]
    public void ApplyQuery_GlobalSort_UsesConfiguredFallback()
    {
        // Arrange
        var images = new[]
        {
            CreateTestImage("charlie.jpg", iso: null),
            CreateTestImage("alpha.jpg", iso: null),
            CreateTestImage("bravo.jpg", iso: 200)
        };
        var globalSort = new ImageSortConfig
        {
            Field = "exif.iso",
            Direction = SortDirection.Asc,
            Fallback = "filename"
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all", null, globalSort).ToList();

        // Assert
        Assert.AreEqual("bravo.jpg", result[0].Filename);
        Assert.AreEqual("alpha.jpg", result[1].Filename);
        Assert.AreEqual("charlie.jpg", result[2].Filename);
    }

    [TestMethod]
    public void ApplyQuery_EqualSortKeys_UsesFilenameTieBreaker()
    {
        // Arrange
        var images = new[]
        {
            CreateTestImage("charlie.jpg", iso: 200),
            CreateTestImage("alpha.jpg", iso: 200),
            CreateTestImage("bravo.jpg", iso: 200)
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all | sort exif.iso desc").ToList();

        // Assert
        Assert.AreEqual("alpha.jpg", result[0].Filename);
        Assert.AreEqual("bravo.jpg", result[1].Filename);
        Assert.AreEqual("charlie.jpg", result[2].Filename);
    }

    [TestMethod]
    public void ApplyQuery_Limit_AppliesAfterEffectiveSort()
    {
        // Arrange
        var images = new[]
        {
            CreateTestImage("alpha.jpg", new DateTime(2024, 1, 1)),
            CreateTestImage("bravo.jpg", new DateTime(2026, 1, 1)),
            CreateTestImage("charlie.jpg", new DateTime(2025, 1, 1))
        };
        var globalSort = new ImageSortConfig
        {
            Field = "dateTaken",
            Direction = SortDirection.Desc,
            Fallback = "filename"
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all | limit 2", null, globalSort).ToList();

        // Assert
        Assert.AreEqual("bravo.jpg", result[0].Filename);
        Assert.AreEqual("charlie.jpg", result[1].Filename);
    }

    [TestMethod]
    public void ApplyQuery_SortRandom_ReturnsShuffledPermutationOfMatches()
    {
        // Arrange
        var images = Enumerable.Range(0, 40)
            .Select(i => CreateTestImage($"img-{i:D2}.jpg", make: i % 2 == 0 ? "Canon" : "Sony"))
            .ToArray();
        var canonInFilenameOrder = images
            .Where(image => image.Exif!.Make == "Canon")
            .Select(image => image.Filename)
            .ToList();

        // Act: 20! orders, so five runs all in filename order would mean no shuffle happened
        var runs = Enumerable.Range(0, 5)
            .Select(_ => FilterService.ApplyQuery(images, "exif.make == 'Canon' | sort random").Select(i => i.Filename).ToList())
            .ToList();

        // Assert
        foreach (var run in runs)
        {
            CollectionAssert.AreEquivalent(canonInFilenameOrder, run);
        }

        Assert.IsTrue(runs.Any(run => !run.SequenceEqual(canonInFilenameOrder, StringComparer.Ordinal)));
    }

    [TestMethod]
    public void ApplyQuery_SortRandomWithLimit_ReturnsLimitedSubsetOfMatches()
    {
        // Arrange
        var images = Enumerable.Range(0, 30)
            .Select(i => CreateTestImage($"img-{i:D2}.jpg", make: i < 20 ? "Canon" : "Sony"))
            .ToArray();
        var globalSort = new ImageSortConfig
        {
            Field = "dateTaken",
            Direction = SortDirection.Desc,
            Fallback = "filename"
        };

        // Act
        var result = FilterService.ApplyQuery(images, "exif.make == 'Canon' | sort random | limit 5", null, globalSort).ToList();

        // Assert
        Assert.HasCount(5, result);
        Assert.IsTrue(result.All(image => image.Exif!.Make == "Canon"));
        Assert.HasCount(5, result.Select(image => image.Filename).Distinct(StringComparer.Ordinal));
    }

    [TestMethod]
    public void ApplyQuery_ExplicitSortWithNullValues_PutsNullsLastWithoutConfiguredFallback()
    {
        // Arrange
        var images = new[]
        {
            new ImageContent
            {
                Filename = "undated.jpg",
                Width = 1920,
                Height = 1080,
                Sizes = [1920]
            },
            CreateTestImage("dated.jpg", new DateTime(2024, 1, 1))
        };
        var globalSort = new ImageSortConfig
        {
            Field = "filename",
            Direction = SortDirection.Desc,
            Fallback = "filename"
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all | sort dateTaken desc", null, globalSort).ToList();

        // Assert
        Assert.AreEqual("dated.jpg", result[0].Filename);
        Assert.AreEqual("undated.jpg", result[1].Filename);
    }

    [TestMethod]
    public void ApplyQuery_SortDateTakenAsc_PutsUndatedImagesLast()
    {
        var images = new[] { CreateUndatedImage("undated.jpg"), CreateTestImage("dated.jpg", new DateTime(2024, 1, 1)) };

        var result = FilterService.ApplyQuery(images, "all | sort dateTaken asc").ToList();

        Assert.AreEqual("dated.jpg", result[0].Filename);
        Assert.AreEqual("undated.jpg", result[1].Filename);
    }

    [TestMethod]
    [DataRow(SortDirection.Asc, "2024.jpg,2025.jpg,a-undated.jpg,b-undated.jpg")]
    [DataRow(SortDirection.Desc, "2025.jpg,2024.jpg,b-undated.jpg,a-undated.jpg")]
    public void Sort_ImagesWithoutSortField_ComeLastOrderedByFallbackInBothDirections(SortDirection direction, string expected)
    {
        var images = new[]
        {
            CreateUndatedImage("a-undated.jpg"),
            CreateTestImage("2024.jpg", new DateTime(2024, 1, 1)),
            CreateUndatedImage("b-undated.jpg"),
            CreateTestImage("2025.jpg", new DateTime(2025, 1, 1))
        };
        var globalSort = new ImageSortConfig { Field = "dateTaken", Direction = direction, Fallback = "filename" };

        var result = FilterService.Sort(images, null, globalSort);

        Assert.AreEqual(expected, string.Join(',', result.Select(image => image.Filename)));
    }

    [TestMethod]
    public void Sort_PageSortOverride_UsesAnyImageProperty()
    {
        var images = new[] { CreateTestImage("small.jpg"), CreateTestImage("large.jpg") with { Width = 4000 } };
        var globalSort = new ImageSortConfig { Field = "filename", Direction = SortDirection.Asc, Fallback = "filename" };

        var result = FilterService.Sort(images, "width:desc", globalSort);

        Assert.AreEqual("large.jpg,small.jpg", string.Join(',', result.Select(image => image.Filename)));
    }

    private static ImageContent CreateUndatedImage(string filename) => new()
    {
        Filename = filename,
        Width = 1920,
        Height = 1080,
        Sizes = [1920]
    };

    private static ImageContent CreateDescribedImage(
        string filename,
        int? rating = null,
        IReadOnlyList<string>? keywords = null,
        string? title = null,
        string? description = null) => new()
        {
            Filename = filename,
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            Rating = rating,
            Keywords = keywords ?? [],
            Title = title,
            Description = description
        };

    [TestMethod]
    public void ApplyQuery_RatingFilterWithRandomSortAndLimit_ReturnsOnlyHighlyRatedImages()
    {
        // Arrange
        var images = Enumerable.Range(0, 40)
            .Select(i => CreateDescribedImage($"img-{i:D2}.jpg", rating: i % 2 == 0 ? (i % 4 == 0 ? 5 : 4) : (i % 3 == 0 ? null : 3)))
            .ToArray();

        // Act
        var result = FilterService.ApplyQuery(images, "rating >= 4 | sort random | limit 15").ToList();

        // Assert
        Assert.HasCount(15, result);
        Assert.IsTrue(result.All(image => image.Rating >= 4));
    }

    [TestMethod]
    public void ApplyQuery_SortRatingDesc_PutsUnratedImagesLast()
    {
        // Arrange
        var images = new[]
        {
            CreateDescribedImage("unrated.jpg"),
            CreateDescribedImage("three.jpg", rating: 3),
            CreateDescribedImage("rejected.jpg", rating: -1),
            CreateDescribedImage("five.jpg", rating: 5)
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all | sort rating desc").Select(image => image.Filename).ToArray();

        // Assert
        Assert.AreEqual("five.jpg,three.jpg,rejected.jpg,unrated.jpg", string.Join(',', result));
    }

    [TestMethod]
    public void ApplyQuery_SortRatingAsc_PutsUnratedImagesLast()
    {
        // Arrange
        var images = new[]
        {
            CreateDescribedImage("unrated.jpg"),
            CreateDescribedImage("five.jpg", rating: 5),
            CreateDescribedImage("one.jpg", rating: 1)
        };

        // Act
        var result = FilterService.ApplyQuery(images, "all | sort rating asc").Select(image => image.Filename).ToArray();

        // Assert
        Assert.AreEqual("one.jpg,five.jpg,unrated.jpg", string.Join(',', result));
    }

    [TestMethod]
    public void Compile_ContainsKeywords_MatchesWholeKeywordIgnoringCase()
    {
        // Arrange
        var predicate = FilterService.Compile("contains(keywords, 'startseite')");

        // Act & Assert
        Assert.IsTrue(predicate(CreateDescribedImage("a.jpg", keywords: ["Selected", "Startseite"])));
        Assert.IsFalse(predicate(CreateDescribedImage("b.jpg", keywords: ["Startseite-Archiv"])));
        Assert.IsFalse(predicate(CreateDescribedImage("c.jpg")));
    }

    [TestMethod]
    public void Compile_NotContainsKeywordsCombinedWithRating_FiltersByBoth()
    {
        // Arrange
        var predicate = FilterService.Compile("rating >= 4 and not contains(keywords, 'Privat')");

        // Act & Assert
        Assert.IsTrue(predicate(CreateDescribedImage("a.jpg", rating: 4, keywords: ["Selected"])));
        Assert.IsFalse(predicate(CreateDescribedImage("b.jpg", rating: 5, keywords: ["privat"])));
        Assert.IsFalse(predicate(CreateDescribedImage("c.jpg", keywords: ["Selected"])));
    }

    [TestMethod]
    public void Compile_TitleAndDescription_AreFilterable()
    {
        // Arrange
        var predicate = FilterService.Compile("contains(title, 'sunset') or description == 'Evening light'");

        // Act & Assert
        Assert.IsTrue(predicate(CreateDescribedImage("a.jpg", title: "Sunset at the lake")));
        Assert.IsTrue(predicate(CreateDescribedImage("b.jpg", description: "Evening light")));
        Assert.IsFalse(predicate(CreateDescribedImage("c.jpg")));
    }

    [TestMethod]
    public void Compile_StartsWithOnKeywords_ThrowsFilterParseException() =>
        // Act & Assert
        Assert.ThrowsExactly<FilterParseException>(() => FilterService.Compile("starts_with(keywords, 'Start')"));
}

