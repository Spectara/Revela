using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// The no-arguments entry decides "is this an interactive terminal?" through
/// <see cref="IConsoleCapabilities"/> only — not through Spectre's own detection.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class InteractiveEntryTests
{
    [TestMethod]
    public async Task RunRevelaAsync_NoArgsNonInteractiveConsole_ExplainsAndFailsWithoutPrompting()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Menu" } }));
        var capabilities = Substitute.For<IConsoleCapabilities>();
        capabilities.IsInteractive.Returns(false);

        // Spectre itself would consider this console interactive; any prompt throws.
        var (exitCode, output) = await RunAsync(project.RootPath, capabilities);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("Interactive mode requires a terminal", output);
        Assert.Contains("revela --help", output);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string projectRoot, IConsoleCapabilities capabilities)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var inner = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(writer),
        });
        inner.Profile.Width = 200;
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = new NoInputConsole(inner);

        try
        {
            var builder = HostBootstrap.CreateBuilder([], new EmptyPackageSource(), projectRoot);
            builder.Services.AddSingleton(capabilities);
            using var host = builder.Build();

            var exitCode = await host.RunRevelaAsync([]);
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private sealed class EmptyPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() => [];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }

    /// <summary>
    /// Interactive-looking console whose input fails, so an unexpected prompt cannot hang the test.
    /// </summary>
    private sealed class NoInputConsole(IAnsiConsole inner) : IAnsiConsole, IAnsiConsoleInput
    {
        public Profile Profile => inner.Profile;

        public IAnsiConsoleCursor Cursor => inner.Cursor;

        public IAnsiConsoleInput Input => this;

        public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;

        public RenderPipeline Pipeline => inner.Pipeline;

        public void Clear(bool home) => inner.Clear(home);

        public void Write(IRenderable renderable) => inner.Write(renderable);

        public void WriteAnsi(Action<AnsiWriter> action) => inner.WriteAnsi(action);

        public bool IsKeyAvailable() => false;

        public ConsoleKeyInfo? ReadKey(bool intercept) =>
            throw new InvalidOperationException("The test console has no input; nothing may prompt.");

        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) =>
            Task.FromResult(ReadKey(intercept));
    }
}
