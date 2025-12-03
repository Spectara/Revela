using FluentAssertions;
using Spectara.Revela.Plugin.Source.OneDrive.Formatting;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Formatting;

[TestClass]
public sealed class FileSizeFormatterTests
{
    [TestMethod]
    public void Format_Zero_ReturnsZeroB()
    {
        // Act
        var result = FileSizeFormatter.Format(0);

        // Assert
        result.Should().Be("0 B");
    }

    [TestMethod]
    [DataRow(1L, "1 B")]
    [DataRow(512L, "512 B")]
    [DataRow(1023L, "1023 B")]
    public void Format_Bytes_ReturnsCorrectFormat(long bytes, string expected)
    {
        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(1024L, "1 KB")]
    [DataRow(1536L, "1.5 KB")]
    [DataRow(10240L, "10 KB")]
    [DataRow(1048575L, "1024 KB")] // Just under 1 MB
    public void Format_Kilobytes_ReturnsCorrectFormat(long bytes, string expected)
    {
        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(1_048_576L, "1 MB")]
    [DataRow(5_242_880L, "5 MB")]
    [DataRow(1_572_864L, "1.5 MB")]
    public void Format_Megabytes_ReturnsCorrectFormat(long bytes, string expected)
    {
        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(1_073_741_824L, "1 GB")]
    [DataRow(2_147_483_648L, "2 GB")]
    [DataRow(1_610_612_736L, "1.5 GB")]
    public void Format_Gigabytes_ReturnsCorrectFormat(long bytes, string expected)
    {
        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [TestMethod]
    public void Format_VeryLargeValue_StaysInGigabytes()
    {
        // Arrange - 100 GB
        const long bytes = 107_374_182_400L;

        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().Be("100 GB");
    }

    [TestMethod]
    public void Format_TypicalPhotoSize_ReturnsReadableFormat()
    {
        // Arrange - ~5.2 MB (typical JPEG)
        const long bytes = 5_452_595L;

        // Act
        var result = FileSizeFormatter.Format(bytes);

        // Assert
        result.Should().StartWith("5.");
        result.Should().EndWith(" MB");
    }
}
