using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Plugins.Compress.Commands;
using Spectara.Revela.Plugins.Compress.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Services;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;

namespace Spectara.Revela.Tests.Plugins.Compress.Commands;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class CompressCommandConsoleTests
{
    [TestMethod]
    public async Task ExecuteAsync_NonLiveConsole_WritesPlainProgressLines()
    {
        using var project = TestProject.Create();
        Directory.CreateDirectory(project.OutputPath);
        await File.WriteAllTextAsync(Path.Combine(project.OutputPath, "index.html"), new string('x', 512));
        var command = CreateCommand(project);

        var (exitCode, output) = await RunCapturedAsync(command);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Compressed 1/1", output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondRunWithOneChangedFile_ReportsCompressedAndUnchangedCounts()
    {
        using var project = TestProject.Create();
        Directory.CreateDirectory(project.OutputPath);
        await File.WriteAllTextAsync(Path.Combine(project.OutputPath, "index.html"), new string('x', 512));
        await File.WriteAllTextAsync(Path.Combine(project.OutputPath, "about.html"), new string('x', 512));
        var command = CreateCommand(project);
        var (firstExitCode, firstOutput) = await RunCapturedAsync(command);
        await File.WriteAllTextAsync(Path.Combine(project.OutputPath, "about.html"), new string('y', 512));

        var (exitCode, output) = await RunCapturedAsync(command);

        Assert.AreEqual(0, firstExitCode);
        Assert.Contains("Compressed: 2", firstOutput, StringComparison.Ordinal);
        Assert.Contains("Unchanged:  0", firstOutput, StringComparison.Ordinal);
        Assert.AreEqual(0, exitCode);
        Assert.Contains("Files:      2", output, StringComparison.Ordinal);
        Assert.Contains("Compressed: 1", output, StringComparison.Ordinal);
        Assert.Contains("Unchanged:  1", output, StringComparison.Ordinal);
    }

    private static CompressCommand CreateCommand(TestProject project)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(project.OutputPath);
        var lifecycle = Substitute.For<IArtifactLifecycle>();
        lifecycle.PrepareToReplaceAsync(Arg.Any<ArtifactId>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok());
        return new CompressCommand(
            NullLogger<CompressCommand>.Instance,
            pathResolver,
            project.Environment(),
            new CompressionService(NullLogger<CompressionService>.Instance),
            lifecycle,
            Substitute.For<IConsoleCapabilities>());
    }

    private static async Task<(int ExitCode, string Output)> RunCapturedAsync(CompressCommand command)
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
        AnsiConsole.Console.Profile.Width = 240;

        try
        {
            var exitCode = await command.ExecuteAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
