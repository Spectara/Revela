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
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns(project.OutputPath);
        var lifecycle = Substitute.For<IArtifactLifecycle>();
        lifecycle.PrepareToReplaceAsync(Arg.Any<ArtifactId>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok());
        var command = new CompressCommand(
            NullLogger<CompressCommand>.Instance,
            pathResolver,
            project.Environment(),
            new CompressionService(NullLogger<CompressionService>.Instance),
            lifecycle,
            Substitute.For<IConsoleCapabilities>());

        var originalConsole = AnsiConsole.Console;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        AnsiConsole.Console.Profile.Width = 240;

        int exitCode;
        try
        {
            exitCode = await command.ExecuteAsync();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Compressed 1/1", writer.ToString(), StringComparison.Ordinal);
    }
}
