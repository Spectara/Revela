using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using Polly.Timeout;
using Spectara.Revela.Plugins.Source.OneDrive.Commands;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;
using Spectara.Revela.Plugins.Source.OneDrive.Providers;
using Spectara.Revela.Plugins.Source.OneDrive.Services;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Tests.Shared.Http;
using Spectre.Console;
using static Spectara.Revela.Tests.Plugins.Source.OneDrive.Providers.SharedLinkProviderTests;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Commands;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class OneDriveSourceCommandTests : IDisposable
{
    private readonly MockHttpMessageHandler mockHandler = new();
    private readonly HttpClient httpClient;

    public OneDriveSourceCommandTests() =>
        httpClient = new HttpClient(mockHandler);

    public void Dispose()
    {
        httpClient.Dispose();
        mockHandler.Dispose();
    }

    [TestMethod]
    public void Create_Default_ReturnsSyncCommand()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Create();

        // Assert
        Assert.AreEqual("sync", result.Name);
        Assert.IsNotNull(result.Description);
    }

    [TestMethod]
    public void Create_Default_ExposesOnlyEssentialOptions()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Create();
        var optionNames = result.Options.Select(o => o.Name).ToList();

        // Assert - verify only essential CLI options exist (rest is config-only)
        Assert.Contains("--share-url", optionNames);
        Assert.Contains("--force", optionNames);
        Assert.Contains("--dry-run", optionNames);
        Assert.Contains("--clean", optionNames);

        // Config-only options should NOT be CLI options
        Assert.DoesNotContain("--output", optionNames);
        Assert.DoesNotContain("--include", optionNames);
        Assert.DoesNotContain("--exclude", optionNames);
        Assert.DoesNotContain("--concurrency", optionNames);
        Assert.DoesNotContain("--debug", optionNames);
    }

    [TestMethod]
    [DataRow("nested")]
    [DataRow("..gallery")]
    public void DeleteOrphanedFile_NormalNestedOrphan_DeletesFile(string directoryName)
    {
        using var project = TestProject.Create(builder => builder.AddGallery(directoryName, gallery => gallery.AddImage("orphan.jpg")));
        var analysis = DownloadAnalyzer.Analyze([], project.SourcePath, includeOrphans: true);
        Assert.HasCount(1, analysis.OrphanedFiles);
        var orphan = analysis.OrphanedFiles[0];

        OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, orphan);

        Assert.IsFalse(File.Exists(orphan.FullName));
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(orphan.FullName)));
    }

    [TestMethod]
    public void DeleteOrphanedFile_DirectorySwappedToLinkAfterAnalysis_RejectsAndPreservesExternalFile()
    {
        using var project = TestProject.Create(builder => builder.AddGallery("gallery", gallery => gallery.AddImage("orphan.jpg")));
        var externalDirectory = Path.Combine(project.RootPath, "external");
        Directory.CreateDirectory(externalDirectory);
        var externalFile = Path.Combine(externalDirectory, "orphan.jpg");
        File.WriteAllText(externalFile, "external bytes");
        var analysis = DownloadAnalyzer.Analyze([], project.SourcePath, includeOrphans: true);
        Assert.HasCount(1, analysis.OrphanedFiles);
        var orphan = analysis.OrphanedFiles[0];
        Assert.AreEqual(0, (int)(orphan.Attributes & FileAttributes.ReparsePoint));
        var galleryPath = Path.Combine(project.SourcePath, "gallery");
        var retainedPath = Path.Combine(project.RootPath, "retained");
        Directory.Move(galleryPath, retainedPath);

        try
        {
            DirectoryLinkTestHelper.Create(galleryPath, externalDirectory);

            Assert.ThrowsExactly<InvalidOperationException>(() => OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, orphan));

            Assert.AreEqual("external bytes", File.ReadAllText(externalFile));
            Assert.IsTrue(File.Exists(Path.Combine(retainedPath, "orphan.jpg")));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(galleryPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeleteOrphanedFile_LinkedSourceRootOrAncestor_UsesChosenBoundary(bool linkedAncestor)
    {
        using var project = TestProject.Create(builder => builder.AddGallery("nested", gallery => gallery.AddImage("orphan.jpg")));
        var linkPath = Path.Combine(project.RootPath, "chosen-link");
        var sourcePath = linkedAncestor ? Path.Combine(linkPath, Path.GetFileName(project.SourcePath)) : linkPath;

        try
        {
            DirectoryLinkTestHelper.Create(linkPath, linkedAncestor ? project.RootPath : project.SourcePath);
            var analysis = DownloadAnalyzer.Analyze([], sourcePath, includeOrphans: true);
            Assert.HasCount(1, analysis.OrphanedFiles);

            OneDriveSourceCommand.DeleteOrphanedFile(sourcePath, analysis.OrphanedFiles[0]);

            Assert.IsFalse(File.Exists(Path.Combine(project.SourcePath, "nested", "orphan.jpg")));
            Assert.IsTrue(Directory.Exists(linkPath));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(linkPath);
        }
    }

    [TestMethod]
    public void DeleteOrphanedFile_FileSwappedToLinkAfterAnalysis_RejectsLink()
    {
        using var project = TestProject.Create(builder => builder.AddGallery("nested", gallery => gallery.AddImage("orphan.jpg")));
        var externalFile = Path.Combine(project.RootPath, "external.jpg");
        File.WriteAllText(externalFile, "external bytes");
        var analysis = DownloadAnalyzer.Analyze([], project.SourcePath, includeOrphans: true);
        Assert.HasCount(1, analysis.OrphanedFiles);
        var orphan = analysis.OrphanedFiles[0];
        File.Delete(orphan.FullName);

        try
        {
            try
            {
                File.CreateSymbolicLink(orphan.FullName, externalFile);
            }
            catch (IOException exception) when (OperatingSystem.IsWindows() && (exception.HResult & 0xFFFF) == 1314)
            {
                Assert.Inconclusive("File symbolic links require a privilege unavailable to this test process.");
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, orphan));
            Assert.AreEqual("external bytes", File.ReadAllText(externalFile));
            Assert.IsTrue(File.Exists(orphan.FullName));
            Assert.IsEmpty(DownloadAnalyzer.Analyze([], project.SourcePath, includeOrphans: true, includeAllOrphans: true).OrphanedFiles);
        }
        finally
        {
            File.Delete(orphan.FullName);
        }
    }

    [TestMethod]
    public void DeleteOrphanedFile_LexicallyOutsideSource_RejectsAndPreservesFile()
    {
        using var project = TestProject.CreateMinimal();
        var siblingDirectory = project.SourcePath + "-sibling";
        Directory.CreateDirectory(siblingDirectory);
        var outsideFile = Path.Combine(siblingDirectory, "orphan.jpg");
        File.WriteAllText(outsideFile, "outside bytes");

        Assert.ThrowsExactly<InvalidOperationException>(() => OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, new FileInfo(outsideFile)));

        Assert.AreEqual("outside bytes", File.ReadAllText(outsideFile));
    }

    [TestMethod]
    public void DeleteOrphanedFile_SourceRoot_RejectsNonDescendant()
    {
        using var project = TestProject.CreateMinimal();

        Assert.ThrowsExactly<InvalidOperationException>(() => OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, new FileInfo(project.SourcePath)));

        Assert.IsTrue(Directory.Exists(project.SourcePath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeleteOrphanedFile_WrongComponentType_RejectsPath(bool fileParent)
    {
        using var project = TestProject.CreateMinimal();
        var blockedPath = Path.Combine(project.SourcePath, "blocked");
        if (fileParent)
        {
            File.WriteAllText(blockedPath, "not a directory");
        }
        else
        {
            Directory.CreateDirectory(blockedPath);
        }
        var orphan = new FileInfo(fileParent ? Path.Combine(blockedPath, "orphan.jpg") : blockedPath);

        Assert.ThrowsExactly<InvalidOperationException>(() => OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, orphan));

        Assert.IsTrue(Path.Exists(blockedPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeleteOrphanedFile_MissingComponent_ThrowsInsteadOfSilentlySucceeding(bool missingParent)
    {
        using var project = TestProject.CreateMinimal();
        var orphanPath = missingParent
            ? Path.Combine(project.SourcePath, "missing", "orphan.jpg")
            : Path.Combine(project.SourcePath, "orphan.jpg");

        // Not exact: the IOException subtype for a missing path differs between operating systems.
        Assert.Throws<IOException>(() => OneDriveSourceCommand.DeleteOrphanedFile(project.SourcePath, new FileInfo(orphanPath)));
    }

    [TestMethod]
    [DataRow("--clean")]
    [DataRow("--clean-all")]
    public async Task ExecuteAsync_DryRunCleanup_PreservesLocalAndExternalFiles(string cleanOption)
    {
        using var project = TestProject.CreateMinimal();
        var localFile = Path.Combine(project.SourcePath, "orphan.jpg");
        await File.WriteAllTextAsync(localFile, "local bytes");
        var externalDirectory = Path.Combine(project.RootPath, "external");
        Directory.CreateDirectory(externalDirectory);
        var externalFile = Path.Combine(externalDirectory, "external.jpg");
        await File.WriteAllTextAsync(externalFile, "external bytes");
        var linkPath = Path.Combine(project.SourcePath, "linked");
        SetupEmptyRemoteFolder();
        var command = CreateCommand(project.SourcePath).Create();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var previousConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;

        try
        {
            DirectoryLinkTestHelper.Create(linkPath, externalDirectory);
            AnsiConsole.Console = console;

            var exitCode = await command.Parse(["--share-url", "https://1drv.ms/f/s!example", "--dry-run", cleanOption]).InvokeAsync();

            Assert.AreEqual(0, exitCode);
            Assert.HasCount(3, mockHandler.RecordedRequests);
            Assert.AreEqual("local bytes", await File.ReadAllTextAsync(localFile));
            Assert.AreEqual("external bytes", await File.ReadAllTextAsync(externalFile));
            Assert.Contains("1 orphaned file(s)", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
            DirectoryLinkTestHelper.Delete(linkPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_ShareUrl_ConsoleDoesNotDiscloseCredential(bool dryRun)
    {
        using var project = TestProject.CreateMinimal();
        SetupEmptyRemoteFolder();
        var command = CreateCommand(project.SourcePath).Create();
        const string shareUrl = "https://1drv.ms/f/SYNTHETIC_SHARE_SECRET?authkey=SYNTHETIC_SHARE_QUERY";
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var previousConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;

        try
        {
            AnsiConsole.Console = console;
            var arguments = new List<string> { "--share-url", shareUrl };
            if (dryRun)
            {
                arguments.Add("--dry-run");
            }

            var exitCode = await command.Parse([.. arguments]).InvokeAsync();

            Assert.AreEqual(0, exitCode);
            Assert.HasCount(3, mockHandler.RecordedRequests);
            var output = writer.ToString();
            Assert.IsTrue(output.Contains("Scan complete", StringComparison.Ordinal));
            Assert.IsFalse(output.Contains("SYNTHETIC_SHARE_SECRET", StringComparison.Ordinal), "Console disclosed the synthetic share credential.");
            Assert.IsFalse(output.Contains("SYNTHETIC_SHARE_QUERY", StringComparison.Ordinal), "Console disclosed the synthetic share query.");
            Assert.IsTrue(output.Contains(SharedLinkProvider.RedactShareUrl(shareUrl), StringComparison.Ordinal));
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }
    }

    [TestMethod]
    [DataRow(nameof(HttpRequestException), false)]
    [DataRow(nameof(IOException), false)]
    [DataRow(nameof(UnauthorizedAccessException), false)]
    [DataRow(nameof(InvalidOperationException), false)]
    [DataRow(nameof(ArgumentException), false)]
    [DataRow(nameof(JsonException), false)]
    [DataRow(nameof(FormatException), false)]
    [DataRow(nameof(TimeoutRejectedException), false)]
    [DataRow(nameof(OperationCanceledException), false)]
    [DataRow(nameof(OperationCanceledException), true)]
    public async Task ExecuteAsync_Failure_ReportsOnlySafeCategoryOrPropagatesCancellation(string failureKind, bool callerCancellation)
    {
        using var project = TestProject.CreateMinimal();
        using var cancellationSource = new CancellationTokenSource();
        using var capture = new PrivacyLogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        PrivacyLogCapture.WriteControl(loggerFactory);
        using var handler = new PrivacyHttpMessageHandler("cdn-failure", () =>
        {
            if (callerCancellation)
            {
                cancellationSource.Cancel();
            }
            return failureKind switch
            {
                nameof(HttpRequestException) => new HttpRequestException(PrivacyHttpMessageHandler.FailureMessage, new IOException(PrivacyHttpMessageHandler.FailureMessage)),
                nameof(IOException) => new IOException(PrivacyHttpMessageHandler.FailureMessage),
                nameof(UnauthorizedAccessException) => new UnauthorizedAccessException(PrivacyHttpMessageHandler.FailureMessage),
                nameof(InvalidOperationException) => new InvalidOperationException(PrivacyHttpMessageHandler.FailureMessage),
                nameof(ArgumentException) => new ArgumentException(PrivacyHttpMessageHandler.FailureMessage),
                nameof(JsonException) => new JsonException(PrivacyHttpMessageHandler.FailureMessage),
                nameof(FormatException) => new FormatException(PrivacyHttpMessageHandler.FailureMessage),
                nameof(TimeoutRejectedException) => new TimeoutRejectedException(PrivacyHttpMessageHandler.FailureMessage),
                _ => new OperationCanceledException(PrivacyHttpMessageHandler.FailureMessage, cancellationSource.Token)
            };
        });
        using var client = new HttpClient(handler);
        var testedProvider = new SharedLinkProvider(client, loggerFactory.CreateLogger<SharedLinkProvider>());
        var command = CreateCommand(project.SourcePath, testedProvider, loggerFactory).Create();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var previousConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false, Output = writer, Error = writer };

        try
        {
            AnsiConsole.Console = console;
            var parsed = command.Parse(["--share-url", PrivacyHttpMessageHandler.ShareUrl]);
            if (callerCancellation)
            {
                // HttpClient reports caller cancellation as TaskCanceledException.
                var exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
                    () => parsed.InvokeAsync(invocation, cancellationSource.Token));
                Assert.IsTrue(exception.CancellationToken.IsCancellationRequested);
                Assert.IsFalse(writer.ToString().Contains("Download failed", StringComparison.Ordinal));
            }
            else
            {
                var exitCode = await parsed.InvokeAsync(invocation, cancellationSource.Token);
                Assert.AreEqual(1, exitCode);
                var category = string.Equals(failureKind, nameof(OperationCanceledException), StringComparison.Ordinal) ? "Download timed out" : failureKind;
                Assert.IsTrue(writer.ToString().Contains(category, StringComparison.Ordinal), "The failure category must remain useful without printing the exception message.");
                Assert.IsTrue(capture.Entries.Contains("state:" + category), "The command must log a sanitized error category.");
            }
            Assert.AreEqual(4, handler.Requests);
            Assert.IsFalse(File.Exists(Path.Combine(project.SourcePath, "photo.jpg")));
            capture.Entries.Enqueue("console:" + writer);
            capture.AssertNoCredentials();
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }
    }

    [TestMethod]
    [DataRow("--clean")]
    [DataRow("--clean-all")]
    public async Task ExecuteAsync_CleanOnNonInteractiveConsoleWithoutYes_FailsBeforeAnyChange(string cleanOption)
    {
        using var project = TestProject.CreateMinimal();
        var localFile = Path.Combine(project.SourcePath, "orphan.jpg");
        await File.WriteAllTextAsync(localFile, "local bytes");
        SetupEmptyRemoteFolder();
        var command = CreateCommand(project.SourcePath).Create();

        var (exitCode, output) = await InvokeAsync(command, ["--share-url", "https://1drv.ms/f/s!example", cleanOption]);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("--yes", output, StringComparison.Ordinal);
        Assert.IsEmpty(mockHandler.RecordedRequests);
        Assert.AreEqual("local bytes", await File.ReadAllTextAsync(localFile));
    }

    [TestMethod]
    public async Task ExecuteAsync_CleanWithYes_DeletesOrphansWithoutPrompting()
    {
        using var project = TestProject.CreateMinimal();
        var localFile = Path.Combine(project.SourcePath, "orphan.jpg");
        await File.WriteAllTextAsync(localFile, "local bytes");
        SetupEmptyRemoteFolder();
        var command = CreateCommand(project.SourcePath).Create();

        var (exitCode, output) = await InvokeAsync(command, ["--share-url", "https://1drv.ms/f/s!example", "--clean", "--yes"]);

        Assert.AreEqual(0, exitCode, output);
        Assert.IsFalse(File.Exists(localFile));
    }

    [TestMethod]
    public async Task ExecuteAsync_NonLiveConsole_WritesPlainScanAndDownloadProgress()
    {
        using var project = TestProject.CreateMinimal();
        SetupRemoteFolderWithPhoto("https://contoso.sharepoint.com/download/photo.jpg");
        var command = CreateCommand(project.SourcePath).Create();

        var (exitCode, output) = await InvokeAsync(command, ["--share-url", "https://1drv.ms/f/s!example"]);

        Assert.AreEqual(0, exitCode, output);
        Assert.Contains("Scanning OneDrive folder structure", output, StringComparison.Ordinal);
        Assert.Contains("Downloaded 1/1", output, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(Path.Combine(project.SourcePath, "photo.jpg")));
    }

    private static async Task<(int ExitCode, string Output)> InvokeAsync(Command command, string[] args)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var previousConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 240;

        try
        {
            AnsiConsole.Console = console;
            var exitCode = await command.Parse(args).InvokeAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }
    }

    private void SetupRemoteFolderWithPhoto(string downloadUrl)
    {
        mockHandler.AddResponse(new Uri("https://api-badgerp.svc.ms/v1.0/token"), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { token = "test-token" })
        });
        mockHandler.AddPatternResponse(
            url => url.Contains("/shares/u!", StringComparison.Ordinal) && url.Contains("/driveItem", StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "folder123", parentReference = new { driveId = "drive123" } })
            });
        mockHandler.AddPatternResponse(url => url.Contains("/root/children", StringComparison.Ordinal), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                value = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["id"] = "photo123",
                        ["name"] = "photo.jpg",
                        ["size"] = 5,
                        ["file"] = new { mimeType = "image/jpeg" },
                        ["@content.downloadUrl"] = downloadUrl
                    }
                }
            })
        });
        mockHandler.AddResponse(new Uri(downloadUrl), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("bytes"u8.ToArray())
        });
    }

    private void SetupEmptyRemoteFolder()
    {
        mockHandler.AddResponse(new Uri("https://api-badgerp.svc.ms/v1.0/token"), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { token = "test-token" })
        });
        mockHandler.AddPatternResponse(
            url => url.Contains("/shares/u!", StringComparison.Ordinal) && url.Contains("/driveItem", StringComparison.Ordinal),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "folder123", parentReference = new { driveId = "drive123" } })
            });
        mockHandler.AddPatternResponse(url => url.Contains("/root/children", StringComparison.Ordinal), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { value = (object[])[] })
        });
    }

    private OneDriveSourceCommand CreateCommand(string? sourcePath = null, SharedLinkProvider? testedProvider = null, ILoggerFactory? loggerFactory = null)
    {
        var commandLogger = loggerFactory?.CreateLogger<OneDriveSourceCommand>() ?? Substitute.For<ILogger<OneDriveSourceCommand>>();
        var providerLogger = Substitute.For<ILogger<SharedLinkProvider>>();
        var provider = testedProvider ?? new SharedLinkProvider(httpClient, providerLogger);
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(sourcePath ?? Path.GetTempPath());
        var configMonitor = Substitute.For<Microsoft.Extensions.Options.IOptionsMonitor<OneDrivePluginConfig>>();
        configMonitor.CurrentValue.Returns(new OneDrivePluginConfig());
        var consoleCapabilities = Substitute.For<IConsoleCapabilities>();

        return new OneDriveSourceCommand(commandLogger, provider, pathResolver, configMonitor, consoleCapabilities);
    }
}
