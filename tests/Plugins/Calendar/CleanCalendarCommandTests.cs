using System.Globalization;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugins.Calendar.Commands;
using Spectara.Revela.Plugins.Calendar.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

using Spectre.Console;

namespace Spectara.Revela.Tests.Calendar;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CleanCalendarCommandTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_CalendarFiles_DeletesThemAndKeepsOtherCacheFiles(bool useCli)
    {
        using var project = TestProject.CreateMinimal();
        var calendar = WriteCacheFile(project, Path.Combine("a [b]", "calendar.json"));
        var customNamed = WriteCacheFile(project, Path.Combine("rentals", "rentals.json"));
        var other = WriteCacheFile(project, "notes.txt");
        var command = CreateCommand(project);

        var success = await ExecuteAsync(command, useCli);

        Assert.IsTrue(success);
        Assert.IsFalse(File.Exists(calendar));
        Assert.IsFalse(File.Exists(customNamed));
        Assert.IsTrue(File.Exists(other));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Execute_DirectoryLinkInCache_PreservesExternalCalendar(bool useCli)
    {
        using var project = TestProject.CreateMinimal();
        var cachePath = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("calendar"));
        Directory.CreateDirectory(cachePath);
        var external = project.RootPath + "-external";
        Directory.CreateDirectory(external);
        var externalCalendar = Path.Combine(external, "calendar.json");
        await File.WriteAllTextAsync(externalCalendar, "{}");
        var link = Path.Combine(cachePath, "linked");
        DirectoryLinkTestHelper.Create(link, external);

        try
        {
            await ExecuteAsync(CreateCommand(project), useCli);

            Assert.IsTrue(File.Exists(externalCalendar));
        }
        finally
        {
            DirectoryLinkTestHelper.Delete(link);
            Directory.Delete(external, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_FileCannotBeDeleted_ReportsEscapedPathAndFails()
    {
        using var project = TestProject.CreateMinimal();
        var calendar = WriteCacheFile(project, Path.Combine("availability [summer]", "calendar.json"));
        var command = CreateCommand(project);

        int exitCode;
        string output;
        using (new FileStream(calendar, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            (exitCode, output) = await CaptureAsync(() => command.ExecuteAsync(CancellationToken.None));
        }

        Assert.AreEqual(1, exitCode);
        Assert.Contains("availability [summer]", output, StringComparison.Ordinal);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task PipelineExecuteAsync_FileCannotBeDeleted_Fails()
    {
        using var project = TestProject.CreateMinimal();
        var calendar = WriteCacheFile(project, Path.Combine("availability", "calendar.json"));

        OperationResult result;
        using (new FileStream(calendar, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await ((IPipelineStep)CreateCommand(project)).ExecuteAsync();
        }

        Assert.IsFalse(result.Success);
        Assert.Contains("calendar.json", result.ErrorMessage!, StringComparison.Ordinal);
    }

    private static CleanCalendarCommand CreateCommand(TestProject project) =>
        new(new TestArtifactLifecycle(
            new CalendarDataInvalidator(Options.Create(new ProjectEnvironment { Path = project.RootPath }))));

    private static string WriteCacheFile(TestProject project, string relativePath)
    {
        var path = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("calendar"), relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return path;
    }

    private static async Task<bool> ExecuteAsync(CleanCalendarCommand command, bool useCli)
    {
        if (useCli)
        {
            var (exitCode, _) = await CaptureAsync(() => command.ExecuteAsync(CancellationToken.None));
            return exitCode == 0;
        }

        var result = await ((IPipelineStep)command).ExecuteAsync();
        return result.Success;
    }

    private static async Task<(int ExitCode, string Output)> CaptureAsync(Func<Task<int>> execute)
    {
        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        AnsiConsole.Console.Profile.Width = 400;

        try
        {
            var exitCode = await execute();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
