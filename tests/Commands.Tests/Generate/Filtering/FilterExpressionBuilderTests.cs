using Spectara.Revela.Commands.Generate.Filtering;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Commands.Tests.Generate.Filtering;

/// <summary>
/// Tests for the <see cref="FilterExpressionBuilder"/> class.
/// </summary>
[TestClass]
public sealed class FilterExpressionBuilderTests
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

    private static Func<ImageContent, bool> Compile(string filter) =>
        FilterService.Compile(filter);

    [TestMethod]
    public void Build_SimpleStringEquality_FiltersCorrectly()
    {
        // Arrange
        var predicate = Compile("filename == 'test.jpg'");
        var matchingImage = CreateTestImage(filename: "test.jpg");
        var nonMatchingImage = CreateTestImage(filename: "other.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(matchingImage));
        Assert.IsFalse(predicate(nonMatchingImage));
    }

    [TestMethod]
    public void Build_CaseInsensitivePropertyAccess_Works()
    {
        // Arrange
        var predicate = Compile("Filename == 'test.jpg'");
        var image = CreateTestImage(filename: "test.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(image));
    }

    [TestMethod]
    public void Build_NotEqual_FiltersCorrectly()
    {
        // Arrange
        var predicate = Compile("filename != 'test.jpg'");
        var matchingImage = CreateTestImage(filename: "other.jpg");
        var nonMatchingImage = CreateTestImage(filename: "test.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(matchingImage));
        Assert.IsFalse(predicate(nonMatchingImage));
    }

    [TestMethod]
    public void Build_NestedProperty_AccessesCorrectly()
    {
        // Arrange
        var predicate = Compile("exif.make == 'Canon'");
        var canonImage = CreateTestImage(make: "Canon");
        var sonyImage = CreateTestImage(make: "Sony");

        // Act & Assert
        Assert.IsTrue(predicate(canonImage));
        Assert.IsFalse(predicate(sonyImage));
    }

    [TestMethod]
    public void Build_NullStringProperty_HandledSafely()
    {
        // Arrange
        var predicate = Compile("exif.make == 'Canon'");
        var imageWithoutMake = CreateTestImage();

        // Act & Assert - should not throw
        Assert.IsFalse(predicate(imageWithoutMake));
    }

    [TestMethod]
    public void Build_AndExpression_BothConditionsRequired()
    {
        // Arrange
        var predicate = Compile("exif.make == 'Canon' and exif.iso >= 800");
        var matching = CreateTestImage(make: "Canon", iso: 1600);
        var wrongMake = CreateTestImage(make: "Sony", iso: 1600);
        var wrongIso = CreateTestImage(make: "Canon", iso: 400);

        // Act & Assert
        Assert.IsTrue(predicate(matching));
        Assert.IsFalse(predicate(wrongMake));
        Assert.IsFalse(predicate(wrongIso));
    }

    [TestMethod]
    public void Build_OrExpression_EitherConditionSufficient()
    {
        // Arrange
        var predicate = Compile("exif.make == 'Canon' or exif.make == 'Sony'");
        var canon = CreateTestImage(make: "Canon");
        var sony = CreateTestImage(make: "Sony");
        var nikon = CreateTestImage(make: "Nikon");

        // Act & Assert
        Assert.IsTrue(predicate(canon));
        Assert.IsTrue(predicate(sony));
        Assert.IsFalse(predicate(nikon));
    }

    [TestMethod]
    public void Build_NotExpression_InvertsResult()
    {
        // Arrange
        var predicate = Compile("not exif.make == 'Canon'");
        var canon = CreateTestImage(make: "Canon");
        var sony = CreateTestImage(make: "Sony");

        // Act & Assert
        Assert.IsFalse(predicate(canon));
        Assert.IsTrue(predicate(sony));
    }

    [TestMethod]
    public void Build_YearFunction_ExtractsYear()
    {
        // Arrange
        var predicate = Compile("year(dateTaken) == 2024");
        var image2024 = CreateTestImage(dateTaken: new DateTime(2024, 6, 15));
        var image2023 = CreateTestImage(dateTaken: new DateTime(2023, 6, 15));

        // Act & Assert
        Assert.IsTrue(predicate(image2024));
        Assert.IsFalse(predicate(image2023));
    }

    [TestMethod]
    public void Build_MonthFunction_ExtractsMonth()
    {
        // Arrange
        var predicate = Compile("month(dateTaken) == 12");
        var december = CreateTestImage(dateTaken: new DateTime(2024, 12, 25));
        var june = CreateTestImage(dateTaken: new DateTime(2024, 6, 15));

        // Act & Assert
        Assert.IsTrue(predicate(december));
        Assert.IsFalse(predicate(june));
    }

    [TestMethod]
    public void Build_DayFunction_ExtractsDay()
    {
        // Arrange
        var predicate = Compile("day(dateTaken) == 25");
        var christmas = CreateTestImage(dateTaken: new DateTime(2024, 12, 25));
        var other = CreateTestImage(dateTaken: new DateTime(2024, 12, 15));

        // Act & Assert
        Assert.IsTrue(predicate(christmas));
        Assert.IsFalse(predicate(other));
    }

    [TestMethod]
    public void Build_ContainsFunction_MatchesSubstring()
    {
        // Arrange
        var predicate = Compile("contains(filename, 'portrait')");
        var matching = CreateTestImage(filename: "my-portrait-2024.jpg");
        var nonMatching = CreateTestImage(filename: "landscape-2024.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(matching));
        Assert.IsFalse(predicate(nonMatching));
    }

    [TestMethod]
    public void Build_ContainsFunction_CaseInsensitive()
    {
        // Arrange
        var predicate = Compile("contains(filename, 'PORTRAIT')");
        var image = CreateTestImage(filename: "my-portrait-2024.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(image));
    }

    [TestMethod]
    public void Build_ContainsFunction_NullSafe()
    {
        // Arrange
        var predicate = Compile("contains(exif.make, 'Canon')");
        var imageWithoutMake = CreateTestImage();

        // Act & Assert - should not throw, just return false
        Assert.IsFalse(predicate(imageWithoutMake));
    }

    [TestMethod]
    public void Build_StartsWithFunction_MatchesPrefix()
    {
        // Arrange
        var predicate = Compile("startswith(filename, 'IMG_')");
        var matching = CreateTestImage(filename: "IMG_1234.jpg");
        var nonMatching = CreateTestImage(filename: "photo_1234.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(matching));
        Assert.IsFalse(predicate(nonMatching));
    }

    [TestMethod]
    public void Build_EndsWithFunction_MatchesSuffix()
    {
        // Arrange
        var predicate = Compile("endswith(filename, '.jpg')");
        var jpg = CreateTestImage(filename: "photo.jpg");
        var png = CreateTestImage(filename: "photo.png");

        // Act & Assert
        Assert.IsTrue(predicate(jpg));
        Assert.IsFalse(predicate(png));
    }

    [TestMethod]
    public void Build_NumericComparison_LessThan()
    {
        // Arrange
        var predicate = Compile("exif.iso < 800");
        var lowIso = CreateTestImage(iso: 400);
        var highIso = CreateTestImage(iso: 1600);

        // Act & Assert
        Assert.IsTrue(predicate(lowIso));
        Assert.IsFalse(predicate(highIso));
    }

    [TestMethod]
    public void Build_NumericComparison_GreaterThan()
    {
        // Arrange
        var predicate = Compile("exif.iso > 800");
        var highIso = CreateTestImage(iso: 1600);
        var lowIso = CreateTestImage(iso: 400);

        // Act & Assert
        Assert.IsTrue(predicate(highIso));
        Assert.IsFalse(predicate(lowIso));
    }

    [TestMethod]
    public void Build_NumericComparison_NullSafe()
    {
        // Arrange
        var predicate = Compile("exif.iso >= 800");
        var imageWithoutIso = CreateTestImage();

        // Act & Assert - should not throw
        Assert.IsFalse(predicate(imageWithoutIso));
    }

    [TestMethod]
    public void Build_ComplexExpression_EvaluatesCorrectly()
    {
        // Arrange
        var predicate = Compile("(exif.make == 'Canon' or exif.make == 'Sony') and year(dateTaken) == 2024");
        var canon2024 = CreateTestImage(make: "Canon", dateTaken: new DateTime(2024, 6, 15));
        var sony2024 = CreateTestImage(make: "Sony", dateTaken: new DateTime(2024, 6, 15));
        var nikon2024 = CreateTestImage(make: "Nikon", dateTaken: new DateTime(2024, 6, 15));
        var canon2023 = CreateTestImage(make: "Canon", dateTaken: new DateTime(2023, 6, 15));

        // Act & Assert
        Assert.IsTrue(predicate(canon2024));
        Assert.IsTrue(predicate(sony2024));
        Assert.IsFalse(predicate(nikon2024));
        Assert.IsFalse(predicate(canon2023));
    }

    [TestMethod]
    public void Build_ToLowerFunction_ConvertsToLowerCase()
    {
        // Arrange
        var predicate = Compile("tolower(filename) == 'test.jpg'");
        var uppercase = CreateTestImage(filename: "TEST.JPG");
        var mixedcase = CreateTestImage(filename: "Test.Jpg");

        // Act & Assert
        Assert.IsTrue(predicate(uppercase));
        Assert.IsTrue(predicate(mixedcase));
    }

    [TestMethod]
    public void Build_ToUpperFunction_ConvertsToUpperCase()
    {
        // Arrange
        var predicate = Compile("toupper(filename) == 'TEST.JPG'");
        var lowercase = CreateTestImage(filename: "test.jpg");

        // Act & Assert
        Assert.IsTrue(predicate(lowercase));
    }

    [TestMethod]
    public void Build_UnknownProperty_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => Compile("unknownProperty == 'value'"));
        Assert.Contains("Unknown property", ex.Message);
    }

    [TestMethod]
    public void Build_UnknownFunction_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => Compile("unknownFunction(filename)"));
        Assert.Contains("Unknown function", ex.Message);
    }

    [TestMethod]
    public void Build_YearFunctionWrongArgumentType_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => Compile("year(filename) == 2024"));
        Assert.Contains("date", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Build_ContainsFunctionWrongArgumentCount_ThrowsException()
    {
        // Act & Assert
        var ex = Assert.ThrowsExactly<FilterParseException>(() => Compile("contains(filename)"));
        Assert.Contains("2 arguments", ex.Message);
    }
}
