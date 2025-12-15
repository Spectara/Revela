using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Core.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileHashService"/>
/// </summary>
[TestClass]
public sealed class FileHashServiceTests
{
    private FileHashService service = null!;
    private string tempDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        service = new FileHashService();
        tempDirectory = Path.Combine(Path.GetTempPath(), $"FileHashServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ComputeHash_SameFile_ReturnsSameHash()
    {
        // Arrange
        var filePath = CreateTestFile("test.bin", size: 1024);

        // Act
        var hash1 = service.ComputeHash(filePath);
        var hash2 = service.ComputeHash(filePath);

        // Assert
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeHash_DifferentContent_ReturnsDifferentHash()
    {
        // Arrange
        var file1 = CreateTestFile("file1.bin", size: 1024, seed: 1);
        var file2 = CreateTestFile("file2.bin", size: 1024, seed: 2);

        // Act
        var hash1 = service.ComputeHash(file1);
        var hash2 = service.ComputeHash(file2);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeHash_DifferentSizes_ReturnsDifferentHash()
    {
        // Arrange - Same content pattern but different sizes
        var file1 = CreateTestFile("small.bin", size: 1024, seed: 42);
        var file2 = CreateTestFile("large.bin", size: 2048, seed: 42);

        // Act
        var hash1 = service.ComputeHash(file1);
        var hash2 = service.ComputeHash(file2);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeHash_SmallFile_ReturnsValidHash()
    {
        // Arrange - File smaller than 128KB (partial hash threshold)
        var filePath = CreateTestFile("small.bin", size: 100);

        // Act
        var hash = service.ComputeHash(filePath);

        // Assert
        Assert.IsNotNull(hash);
        Assert.AreEqual(12, hash.Length, "Hash should be 12 hex characters");
        Assert.IsTrue(hash.All(c => char.IsAsciiHexDigit(c)), "Hash should be hexadecimal");
    }

    [TestMethod]
    public void ComputeHash_LargeFile_ReturnsValidHash()
    {
        // Arrange - File larger than 128KB (uses partial hashing)
        var filePath = CreateTestFile("large.bin", size: 256 * 1024);

        // Act
        var hash = service.ComputeHash(filePath);

        // Assert
        Assert.IsNotNull(hash);
        Assert.AreEqual(12, hash.Length, "Hash should be 12 hex characters");
        Assert.IsTrue(hash.All(c => char.IsAsciiHexDigit(c)), "Hash should be hexadecimal");
    }

    [TestMethod]
    public void ComputeHash_LargeFile_MiddleContentChange_SameHash()
    {
        // Arrange - For files > 128KB, only first/last 64KB are hashed
        // Changing middle content should NOT change the hash (by design)
        var size = 256 * 1024; // 256KB
        var file1 = CreateTestFile("original.bin", size: size, seed: 1);

        // Create copy with modified middle
        var file2Path = Path.Combine(tempDirectory, "modified.bin");
        File.Copy(file1, file2Path);
        using (var stream = File.OpenWrite(file2Path))
        {
            stream.Seek(100 * 1024, SeekOrigin.Begin); // Middle of file
            stream.Write([0xFF, 0xFF, 0xFF, 0xFF]); // Change 4 bytes
        }

        // Act
        var hash1 = service.ComputeHash(file1);
        var hash2 = service.ComputeHash(file2Path);

        // Assert - Hashes should be SAME (middle not included in partial hash)
        Assert.AreEqual(hash1, hash2, "Partial hash should not detect middle-only changes");
    }

    [TestMethod]
    public void ComputeHash_LargeFile_HeaderChange_DifferentHash()
    {
        // Arrange - Changing first 64KB should change hash
        var size = 256 * 1024;
        var file1 = CreateTestFile("original.bin", size: size, seed: 1);

        var file2Path = Path.Combine(tempDirectory, "header_modified.bin");
        File.Copy(file1, file2Path);
        using (var stream = File.OpenWrite(file2Path))
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write([0xFF, 0xFF, 0xFF, 0xFF]);
        }

        // Act
        var hash1 = service.ComputeHash(file1);
        var hash2 = service.ComputeHash(file2Path);

        // Assert
        Assert.AreNotEqual(hash1, hash2, "Header change should be detected");
    }

    [TestMethod]
    public void ComputeHash_LargeFile_TrailerChange_DifferentHash()
    {
        // Arrange - Changing last 64KB should change hash
        var size = 256 * 1024;
        var file1 = CreateTestFile("original.bin", size: size, seed: 1);

        var file2Path = Path.Combine(tempDirectory, "trailer_modified.bin");
        File.Copy(file1, file2Path);
        using (var stream = File.OpenWrite(file2Path))
        {
            stream.Seek(-4, SeekOrigin.End);
            stream.Write([0xFF, 0xFF, 0xFF, 0xFF]);
        }

        // Act
        var hash1 = service.ComputeHash(file1);
        var hash2 = service.ComputeHash(file2Path);

        // Assert
        Assert.AreNotEqual(hash1, hash2, "Trailer change should be detected");
    }

    [TestMethod]
    public void ComputeHash_FileNotFound_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(tempDirectory, "does_not_exist.bin");

        // Act & Assert
        Assert.ThrowsExactly<FileNotFoundException>(() => service.ComputeHash(nonExistentPath));
    }

    [TestMethod]
    public void ComputeHash_EmptyFile_ReturnsValidHash()
    {
        // Arrange
        var filePath = CreateTestFile("empty.bin", size: 0);

        // Act
        var hash = service.ComputeHash(filePath);

        // Assert
        Assert.IsNotNull(hash);
        Assert.AreEqual(12, hash.Length);
    }

    [TestMethod]
    public void ComputeHash_ExactlyAtThreshold_ReturnsValidHash()
    {
        // Arrange - File exactly at 128KB threshold
        var filePath = CreateTestFile("threshold.bin", size: 128 * 1024);

        // Act
        var hash = service.ComputeHash(filePath);

        // Assert
        Assert.IsNotNull(hash);
        Assert.AreEqual(12, hash.Length);
    }

    /// <summary>
    /// Creates a test file with deterministic content.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Test data generation, not security-sensitive")]
    private string CreateTestFile(string name, int size, int seed = 0)
    {
        var filePath = Path.Combine(tempDirectory, name);
        var random = new Random(seed);
        var content = new byte[size];
        random.NextBytes(content);
        File.WriteAllBytes(filePath, content);
        return filePath;
    }
}
