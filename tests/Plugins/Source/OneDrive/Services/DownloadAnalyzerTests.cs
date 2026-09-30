using Spectara.Revela.Plugins.Source.OneDrive.Models;
using Spectara.Revela.Plugins.Source.OneDrive.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class DownloadAnalyzerTests : IDisposable
{
    private readonly string tempDirectory;

    public DownloadAnalyzerTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"OneDriveTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
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

        // Act
        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory);

        // Assert
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(FileStatus.New, result.Items[0].Status);
        Assert.AreEqual("New file", result.Items[0].Reason);
        Assert.AreEqual(1, result.Statistics.NewFiles);
        Assert.AreEqual(1, result.Statistics.TotalFilesToDownload);
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

        // Act
        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory);

        // Assert
        Assert.HasCount(3, result.Items);
        Assert.IsTrue(result.Items.All(i => i.Status == FileStatus.New));
        Assert.AreEqual(3, result.Statistics.NewFiles);
        Assert.AreEqual(1024 + 2048 + 4096, result.Statistics.TotalDownloadSize);
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

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory);

        // Assert
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(FileStatus.Unchanged, result.Items[0].Status);
        Assert.AreEqual("Up to date", result.Items[0].Reason);
        Assert.AreEqual(1, result.Statistics.UnchangedFiles);
        Assert.AreEqual(0, result.Statistics.TotalFilesToDownload);
    }

    [TestMethod]
    public void Analyze_WhenDateWithinTolerance_ReturnsUnchanged()
    {
        // Arrange - 5 second tolerance
        var remoteTime = DateTime.UtcNow.AddHours(-1);
        var localTime = remoteTime.AddSeconds(3); // Within 5 second tolerance

        var remoteItem = CreateRemoteItem("photo.jpg", 1024, remoteTime);
        CreateLocalFile("photo.jpg", 1024, localTime);

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory);

        // Assert
        Assert.AreEqual(FileStatus.Unchanged, result.Items[0].Status);
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

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory);

        // Assert
        Assert.AreEqual(FileStatus.Modified, result.Items[0].Status);
        Assert.IsTrue(result.Items[0].Reason.Contains("Size changed", StringComparison.Ordinal));
        Assert.IsTrue(result.Items[0].Reason.Contains("1 KB", StringComparison.Ordinal));
        Assert.IsTrue(result.Items[0].Reason.Contains("2 KB", StringComparison.Ordinal));
        Assert.AreEqual(1, result.Statistics.ModifiedFiles);
    }

    [TestMethod]
    public void Analyze_WhenDateDiffersOutsideTolerance_ReturnsModified()
    {
        // Arrange
        var remoteTime = DateTime.UtcNow.AddHours(-1);
        var localTime = remoteTime.AddSeconds(10); // Outside 5 second tolerance

        var remoteItem = CreateRemoteItem("photo.jpg", 1024, remoteTime);
        CreateLocalFile("photo.jpg", 1024, localTime); // Same size, different date

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory);

        // Assert
        Assert.AreEqual(FileStatus.Modified, result.Items[0].Status);
        Assert.IsTrue(result.Items[0].Reason.Contains("Modified:", StringComparison.Ordinal));
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

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory, forceRefresh: true);

        // Assert
        Assert.AreEqual(FileStatus.Modified, result.Items[0].Status);
        Assert.AreEqual("Forced refresh", result.Items[0].Reason);
    }

    [TestMethod]
    public void Analyze_WithForceRefresh_NewFilesStayNew()
    {
        // Arrange
        var remoteItem = CreateRemoteItem("new-photo.jpg", 1024);

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory, forceRefresh: true);

        // Assert
        Assert.AreEqual(FileStatus.New, result.Items[0].Status); // Still new, not modified
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

        // Act
        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory, includeOrphans: true);

        // Assert
        Assert.HasCount(1, result.OrphanedFiles);
        Assert.AreEqual("orphan.jpg", result.OrphanedFiles[0].Name);
        Assert.AreEqual(1, result.Statistics.OrphanedFiles);
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

        // Act
        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory, includeOrphans: false);

        // Assert
        Assert.IsEmpty(result.OrphanedFiles);
    }

    [TestMethod]
    public void Analyze_OrphanedFiles_OnlyIncludesFilteredExtensions()
    {
        // Arrange
        CreateLocalFile("image.jpg", 1024);     // Should be orphan
        CreateLocalFile("document.pdf", 2048);  // Should NOT be orphan (not filtered)

        // Act (no remote files, so all local files are potentially orphans)
        var result = DownloadAnalyzer.Analyze([], tempDirectory, includeOrphans: true);

        // Assert
        Assert.HasCount(1, result.OrphanedFiles);
        Assert.AreEqual("image.jpg", result.OrphanedFiles[0].Name);
    }

    [TestMethod]
    public void Analyze_IncludeAllOrphans_IncludesAllFileTypes()
    {
        // Arrange
        CreateLocalFile("image.jpg", 1024);
        CreateLocalFile("document.pdf", 2048);

        // Act
        var result = DownloadAnalyzer.Analyze([], tempDirectory, includeOrphans: true, includeAllOrphans: true);

        // Assert
        Assert.HasCount(2, result.OrphanedFiles);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Analyze_LinkedChildDirectory_ExcludesExternalOrphans(bool includeAllOrphans)
    {
        var sourceDirectory = Path.Combine(tempDirectory, "chosen-source");
        var externalDirectory = Path.Combine(tempDirectory, "external");
        CreateLocalFile(Path.Combine("chosen-source", "nested", "orphan.jpg"), 1024);
        CreateLocalFile(Path.Combine("external", "external.jpg"), 2048);
        CreateLocalFile(Path.Combine("external", "external.pdf"), 4096);
        var linkPath = Path.Combine(sourceDirectory, "linked");

        try
        {
            DirectoryLinkTestHelper.Create(linkPath, externalDirectory);

            var result = DownloadAnalyzer.Analyze([], sourceDirectory,
                includeOrphans: true, includeAllOrphans: includeAllOrphans);

            Assert.HasCount(1, result.OrphanedFiles);
            Assert.AreEqual(Path.Combine(sourceDirectory, "nested", "orphan.jpg"), result.OrphanedFiles[0].FullName);
            Assert.AreEqual(1024L, result.Statistics.TotalOrphanedSize);
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(linkPath);
        }
    }

    [TestMethod]
    public void Analyze_HiddenAndSystemOrphans_StillIncludesFiles()
    {
        CreateLocalFile(".hidden.jpg", 1024);
        var filePath = Path.Combine(tempDirectory, ".hidden.jpg");
        var originalAttributes = File.GetAttributes(filePath);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(filePath, originalAttributes | FileAttributes.Hidden | FileAttributes.System);
            }

            var result = DownloadAnalyzer.Analyze([], tempDirectory, includeOrphans: true);

            Assert.HasCount(1, result.OrphanedFiles);
            Assert.AreEqual(filePath, result.OrphanedFiles[0].FullName);
        }
        finally
        {
            File.SetAttributes(filePath, originalAttributes);
        }
    }

    #endregion

    #region Pattern Filtering

    [TestMethod]
    public void Analyze_RemoteItemMatchesExcludePattern_IsNotDownloaded()
    {
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("photo.jpg", 1024),
            CreateRemoteItem("photo.CR2", 4096),
            CreateRemoteItem("photo.xmp", 128)
        };

        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory, excludePatterns: ["*.cr2", "*.xmp"]);

        Assert.HasCount(1, result.Items);
        Assert.AreEqual("photo.jpg", result.Items[0].RemoteItem.Name);
        Assert.AreEqual(1, result.Statistics.NewFiles);
        Assert.AreEqual(1024L, result.Statistics.TotalDownloadSize);
    }

    [TestMethod]
    public void Analyze_RemoteItemNotMatchingIncludePattern_IsNotDownloaded()
    {
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("photo.jpg", 1024),
            CreateRemoteItem("notes.md", 64),
            CreateRemoteItem("scan.tiff", 8192)
        };

        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory, includePatterns: ["*.JPG", "*.md"]);

        var names = result.Items.Select(i => i.RemoteItem.Name).ToList();
        Assert.HasCount(2, names);
        Assert.Contains("photo.jpg", names);
        Assert.Contains("notes.md", names);
        Assert.AreEqual(1024L + 64L, result.Statistics.TotalDownloadSize);
    }

    [TestMethod]
    public void Analyze_RemoteItemMatchingIncludeAndExclude_ExcludeWins()
    {
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("keep.jpg", 1024),
            CreateRemoteItem("private-01.jpg", 2048)
        };

        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory,
            includePatterns: ["*.jpg"], excludePatterns: ["private-*"]);

        Assert.HasCount(1, result.Items);
        Assert.AreEqual("keep.jpg", result.Items[0].RemoteItem.Name);
    }

    [TestMethod]
    public void Analyze_NestedRemoteItemMatchingExcludePattern_IsNotDownloaded()
    {
        var remoteItems = new List<OneDriveItem>
        {
            new() { Id = "1", Name = "photo.jpg", ParentPath = "Gallery", Size = 1024, LastModified = DateTime.UtcNow },
            new() { Id = "2", Name = "photo.tmp", ParentPath = "Gallery", Size = 512, LastModified = DateTime.UtcNow }
        };

        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory, excludePatterns: ["*.tmp"]);

        Assert.HasCount(1, result.Items);
        Assert.AreEqual("Gallery/photo.jpg", result.Items[0].RelativePath);
    }

    [TestMethod]
    public void Analyze_NoIncludePatterns_DownloadsAllNonExcludedRemoteFiles()
    {
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("photo.jpg", 1024),
            CreateRemoteItem("_index.revela", 256),
            CreateRemoteItem("animation.gif", 2048)
        };

        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory);

        Assert.HasCount(3, result.Items);
    }

    [TestMethod]
    public void Analyze_ExcludedRemoteFileWithLocalCopy_IsNotReportedAsOrphan()
    {
        CreateLocalFile("photo.xmp", 128);
        var remoteItems = new List<OneDriveItem>
        {
            CreateRemoteItem("photo.xmp", 128)
        };

        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory,
            excludePatterns: ["*.xmp"], includeOrphans: true, includeAllOrphans: true);

        Assert.IsEmpty(result.Items);
        Assert.IsEmpty(result.OrphanedFiles);
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

        // Act
        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory);

        // Assert
        Assert.HasCount(1, result.Items);
        Assert.AreEqual("photo.jpg", result.Items[0].RemoteItem.Name);
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

        // Act
        var result = DownloadAnalyzer.Analyze([remoteItem], tempDirectory);

        // Assert
        Assert.AreEqual(FileStatus.Unchanged, result.Items[0].Status);
        Assert.AreEqual("Gallery/2024/photo.jpg", result.Items[0].RelativePath);
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

        // Act
        var result = DownloadAnalyzer.Analyze(remoteItems, tempDirectory);

        // Assert
        Assert.AreEqual(1, result.Statistics.NewFiles);
        Assert.AreEqual(1, result.Statistics.ModifiedFiles);
        Assert.AreEqual(1, result.Statistics.UnchangedFiles);
        Assert.AreEqual(2, result.Statistics.TotalFilesToDownload); // new + modified
        Assert.AreEqual(3000L, result.Statistics.TotalDownloadSize); // 1000 + 2000
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

    #endregion
}
