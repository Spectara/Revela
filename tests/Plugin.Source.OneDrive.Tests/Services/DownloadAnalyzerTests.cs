using FluentAssertions;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Plugin.Source.OneDrive.Services;

namespace Spectara.Revela.Plugin.Source.OneDrive.Tests.Services;

[TestClass]
public sealed class DownloadAnalyzerTests : IDisposable
{
    private readonly string tempDirectory;
    private readonly DownloadAnalyzer analyzer;

    public DownloadAnalyzerTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"OneDriveTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        analyzer = new DownloadAnalyzer();
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    #region New Files

    [TestMethod]
    public void Analyze_WhenLocalFileDoesNotExist_ReturnsNew()
    {
        // Arrange
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("photo.jpg", 1024)
        };
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze(remoteItems, tempDirectory, config);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(FileStatus.New);
        result.Items[0].Reason.Should().Be("New file");
        result.Statistics.NewFiles.Should().Be(1);
        result.Statistics.TotalFilesToDownload.Should().Be(1);
    }

    [TestMethod]
    public void Analyze_MultipleNewFiles_CountsAllAsNew()
    {
        // Arrange
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("photo1.jpg", 1024),
            CreateRemoteItem("photo2.jpg", 2048),
            CreateRemoteItem("photo3.jpg", 4096)
        };
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze(remoteItems, tempDirectory, config);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(i => i.Status == FileStatus.New);
        result.Statistics.NewFiles.Should().Be(3);
        result.Statistics.TotalDownloadSize.Should().Be(1024 + 2048 + 4096);
    }

    #endregion

    #region Unchanged Files

    [TestMethod]
    public void Analyze_WhenSizeAndDateMatch_ReturnsUnchanged()
    {
        // Arrange
        var lastModified = DateTime.UtcNow.AddHours(-1);
        var remoteItem = CreateRemoteItem("photo.jpg", 1024, lastModified);
        CreateLocalFile("photo.jpg", 1024, lastModified);

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(FileStatus.Unchanged);
        result.Items[0].Reason.Should().Be("Up to date");
        result.Statistics.UnchangedFiles.Should().Be(1);
        result.Statistics.TotalFilesToDownload.Should().Be(0);
    }

    [TestMethod]
    public void Analyze_WhenDateWithinTolerance_ReturnsUnchanged()
    {
        // Arrange - 5 second tolerance
        var remoteTime = DateTime.UtcNow.AddHours(-1);
        var localTime = remoteTime.AddSeconds(3); // Within 5 second tolerance

        var remoteItem = CreateRemoteItem("photo.jpg", 1024, remoteTime);
        CreateLocalFile("photo.jpg", 1024, localTime);

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config);

        // Assert
        result.Items[0].Status.Should().Be(FileStatus.Unchanged);
    }

    #endregion

    #region Modified Files

    [TestMethod]
    public void Analyze_WhenSizeDiffers_ReturnsModified()
    {
        // Arrange
        var lastModified = DateTime.UtcNow.AddHours(-1);
        var remoteItem = CreateRemoteItem("photo.jpg", 2048, lastModified);
        CreateLocalFile("photo.jpg", 1024, lastModified); // Different size

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config);

        // Assert
        result.Items[0].Status.Should().Be(FileStatus.Modified);
        result.Items[0].Reason.Should().Contain("Size changed");
        result.Items[0].Reason.Should().Contain("1 KB");
        result.Items[0].Reason.Should().Contain("2 KB");
        result.Statistics.ModifiedFiles.Should().Be(1);
    }

    [TestMethod]
    public void Analyze_WhenDateDiffersOutsideTolerance_ReturnsModified()
    {
        // Arrange
        var remoteTime = DateTime.UtcNow.AddHours(-1);
        var localTime = remoteTime.AddSeconds(10); // Outside 5 second tolerance

        var remoteItem = CreateRemoteItem("photo.jpg", 1024, remoteTime);
        CreateLocalFile("photo.jpg", 1024, localTime); // Same size, different date

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config);

        // Assert
        result.Items[0].Status.Should().Be(FileStatus.Modified);
        result.Items[0].Reason.Should().Contain("Modified:");
    }

    #endregion

    #region Force Refresh

    [TestMethod]
    public void Analyze_WithForceRefresh_AllExistingFilesAreModified()
    {
        // Arrange
        var lastModified = DateTime.UtcNow.AddHours(-1);
        var remoteItem = CreateRemoteItem("photo.jpg", 1024, lastModified);
        CreateLocalFile("photo.jpg", 1024, lastModified); // Would normally be unchanged

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config, forceRefresh: true);

        // Assert
        result.Items[0].Status.Should().Be(FileStatus.Modified);
        result.Items[0].Reason.Should().Be("Forced refresh");
    }

    [TestMethod]
    public void Analyze_WithForceRefresh_NewFilesStayNew()
    {
        // Arrange
        var remoteItem = CreateRemoteItem("new-photo.jpg", 1024);
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config, forceRefresh: true);

        // Assert
        result.Items[0].Status.Should().Be(FileStatus.New); // Still new, not modified
    }

    #endregion

    #region Orphaned Files

    [TestMethod]
    public void Analyze_WithIncludeOrphans_FindsOrphanedFiles()
    {
        // Arrange
        CreateLocalFile("orphan.jpg", 1024);
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("existing.jpg", 1024)
        };
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze(remoteItems, tempDirectory, config, includeOrphans: true);

        // Assert
        result.OrphanedFiles.Should().HaveCount(1);
        result.OrphanedFiles[0].Name.Should().Be("orphan.jpg");
        result.Statistics.OrphanedFiles.Should().Be(1);
    }

    [TestMethod]
    public void Analyze_WithoutIncludeOrphans_DoesNotFindOrphans()
    {
        // Arrange
        CreateLocalFile("orphan.jpg", 1024);
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("existing.jpg", 1024)
        };
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze(remoteItems, tempDirectory, config, includeOrphans: false);

        // Assert
        result.OrphanedFiles.Should().BeEmpty();
    }

    [TestMethod]
    public void Analyze_OrphanedFiles_OnlyIncludesFilteredExtensions()
    {
        // Arrange
        CreateLocalFile("image.jpg", 1024);     // Should be orphan
        CreateLocalFile("document.pdf", 2048);  // Should NOT be orphan (not filtered)
        var config = CreateConfig();

        // Act (no remote files, so all local files are potentially orphans)
        var result = analyzer.Analyze([], tempDirectory, config, includeOrphans: true);

        // Assert
        result.OrphanedFiles.Should().HaveCount(1);
        result.OrphanedFiles[0].Name.Should().Be("image.jpg");
    }

    [TestMethod]
    public void Analyze_IncludeAllOrphans_IncludesAllFileTypes()
    {
        // Arrange
        CreateLocalFile("image.jpg", 1024);
        CreateLocalFile("document.pdf", 2048);
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([], tempDirectory, config, includeOrphans: true, includeAllOrphans: true);

        // Assert
        result.OrphanedFiles.Should().HaveCount(2);
    }

    #endregion

    #region Folders

    [TestMethod]
    public void Analyze_IgnoresFolders_OnlyProcessesFiles()
    {
        // Arrange
        var remoteItems = new List<OneDriveItem>
        {
            new() { Id = "1", Name = "Gallery", IsFolder = true, Size = 0, LastModified = DateTime.UtcNow },
            CreateRemoteItem("photo.jpg", 1024)
        };
        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze(remoteItems, tempDirectory, config);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].RemoteItem.Name.Should().Be("photo.jpg");
    }

    #endregion

    #region Nested Paths

    [TestMethod]
    public void Analyze_NestedFile_CreatesCorrectLocalPath()
    {
        // Arrange
        var lastModified = DateTime.UtcNow.AddHours(-1);
        var remoteItem = new OneDriveItem
        {
            Id = "1",
            Name = "photo.jpg",
            ParentPath = "Gallery/2024",
            IsFolder = false,
            Size = 1024,
            LastModified = lastModified
        };

        // Create nested local file
        var nestedDir = Path.Combine(tempDirectory, "Gallery", "2024");
        Directory.CreateDirectory(nestedDir);
        CreateLocalFile(Path.Combine("Gallery", "2024", "photo.jpg"), 1024, lastModified);

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze([remoteItem], tempDirectory, config);

        // Assert
        result.Items[0].Status.Should().Be(FileStatus.Unchanged);
        result.Items[0].RelativePath.Should().Be("Gallery/2024/photo.jpg");
    }

    #endregion

    #region Statistics

    [TestMethod]
    public void Analyze_CalculatesCorrectStatistics()
    {
        // Arrange
        var now = DateTime.UtcNow.AddHours(-1);
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("new.jpg", 1000),
            CreateRemoteItem("modified.jpg", 2000, now),
            CreateRemoteItem("unchanged.jpg", 3000, now)
        };

        // Create local files for modified and unchanged
        CreateLocalFile("modified.jpg", 1500, now);  // Different size = modified
        CreateLocalFile("unchanged.jpg", 3000, now); // Same = unchanged

        var config = CreateConfig();

        // Act
        var result = analyzer.Analyze(remoteItems, tempDirectory, config);

        // Assert
        result.Statistics.NewFiles.Should().Be(1);
        result.Statistics.ModifiedFiles.Should().Be(1);
        result.Statistics.UnchangedFiles.Should().Be(1);
        result.Statistics.TotalFilesToDownload.Should().Be(2); // new + modified
        result.Statistics.TotalDownloadSize.Should().Be(3000); // 1000 + 2000
    }

    #endregion

    #region Helper Methods

    private static OneDriveItem CreateRemoteItem(string name, long size, DateTime? lastModified = null)
    {
        return new OneDriveItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            ParentPath = string.Empty,
            IsFolder = false,
            Size = size,
            LastModified = lastModified ?? DateTime.UtcNow
        };
    }

    private void CreateLocalFile(string relativePath, int size, DateTime? lastModified = null)
    {
        var fullPath = Path.Combine(tempDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create file with specific size
        using var stream = File.Create(fullPath);
        stream.SetLength(size);

        // Set last modified time
        if (lastModified.HasValue)
        {
            File.SetLastWriteTimeUtc(fullPath, lastModified.Value);
        }
    }

    private static OneDriveConfig CreateConfig()
    {
        return new OneDriveConfig
        {
            ShareUrl = "https://1drv.ms/f/s!example"
        };
    }

    #endregion
}
