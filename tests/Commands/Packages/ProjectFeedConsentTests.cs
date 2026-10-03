using System.Globalization;
using NSubstitute;
using Spectara.Revela.Core.Models;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Spectara.Revela.Tests.Commands.Packages;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class ProjectFeedConsentTests
{
    private static readonly NuGetSource ProjectFeed = new()
    {
        Name = "test",
        Url = "/work/project/my-feed",
        IsProjectFeed = true
    };

    [TestMethod]
    [DataRow('y', true)]
    [DataRow('n', false)]
    public async Task EnsureApprovedAsync_Interactive_AsksAndHonorsAnswer(char answer, bool expected)
    {
        var sourceManager = CreateSourceManager(ProjectFeed);

        var (approved, output) = await RunAsync(sourceManager, interactive: true, allow: false, explicitSource: null, answer);

        Assert.AreEqual(expected, approved);
        Assert.Contains("test", output, StringComparison.Ordinal);
        Assert.Contains("/work/project/my-feed", output, StringComparison.Ordinal);
        Assert.Contains("/work/project/project.json", output, StringComparison.Ordinal);
        Assert.Contains("Use these project feeds for this command?", output, StringComparison.Ordinal);
        if (expected)
        {
            sourceManager.Received(1).ApproveProjectFeeds();
        }
        else
        {
            sourceManager.DidNotReceive().ApproveProjectFeeds();
            Assert.Contains("Project feeds were not used", output, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task EnsureApprovedAsync_NonInteractiveWithoutFlag_FailsWithHint()
    {
        var sourceManager = CreateSourceManager(ProjectFeed);

        var (approved, output) = await RunAsync(sourceManager, interactive: false, allow: false, explicitSource: null);

        Assert.IsFalse(approved);
        Assert.Contains("/work/project/my-feed", output, StringComparison.Ordinal);
        Assert.Contains(ProjectFeedConsent.AllowOptionName, output, StringComparison.Ordinal);
        sourceManager.DidNotReceive().ApproveProjectFeeds();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnsureApprovedAsync_Flag_ApprovesWithoutPrompt(bool interactive)
    {
        var sourceManager = CreateSourceManager(ProjectFeed);

        var (approved, output) = await RunAsync(sourceManager, interactive, allow: true, explicitSource: null);

        Assert.IsTrue(approved);
        Assert.IsEmpty(output);
        sourceManager.Received(1).ApproveProjectFeeds();
    }

    [TestMethod]
    [DataRow("C:/packages")]
    [DataRow("https://example.test/v3/index.json")]
    [DataRow("other")]
    public async Task EnsureApprovedAsync_ExplicitSourceOutsideProjectFeeds_DoesNotAsk(string source)
    {
        var sourceManager = CreateSourceManager(ProjectFeed);

        var (approved, output) = await RunAsync(sourceManager, interactive: false, allow: false, explicitSource: source);

        Assert.IsTrue(approved);
        Assert.IsEmpty(output);
        sourceManager.DidNotReceive().ApproveProjectFeeds();
    }

    [TestMethod]
    public async Task EnsureApprovedAsync_ExplicitSourceNamingProjectFeed_RequiresConsent()
    {
        var sourceManager = CreateSourceManager(ProjectFeed);

        var (approved, _) = await RunAsync(sourceManager, interactive: false, allow: false, explicitSource: "TEST");

        Assert.IsFalse(approved);
    }

    [TestMethod]
    public async Task EnsureApprovedAsync_NoPendingProjectFeeds_ApprovesSilently()
    {
        var sourceManager = CreateSourceManager();

        var (approved, output) = await RunAsync(sourceManager, interactive: false, allow: false, explicitSource: null);

        Assert.IsTrue(approved);
        Assert.IsEmpty(output);
        sourceManager.DidNotReceive().ApproveProjectFeeds();
    }

    private static INuGetSourceManager CreateSourceManager(params NuGetSource[] pending)
    {
        var sourceManager = Substitute.For<INuGetSourceManager>();
        sourceManager.GetPendingProjectFeeds().Returns(pending);
        sourceManager.ProjectConfigPath.Returns("/work/project/project.json");
        return sourceManager;
    }

    private static async Task<(bool Approved, string Output)> RunAsync(
        INuGetSourceManager sourceManager,
        bool interactive,
        bool allow,
        string? explicitSource,
        char? answer = null)
    {
        var capabilities = Substitute.For<IConsoleCapabilities>();
        capabilities.IsInteractive.Returns(interactive);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var inner = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        inner.Profile.Width = 240;
        var keys = answer is { } key
            ? new[] { new ConsoleKeyInfo(key, ConsoleKey.NoName, false, false, false), new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false) }
            : [];
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = new ScriptedConsole(inner, keys);

        try
        {
            var approved = await new ProjectFeedConsent(sourceManager, capabilities)
                .EnsureApprovedAsync(allow, explicitSource, CancellationToken.None);
            return (approved, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    /// <summary>
    /// Console that renders through a real Spectre console but answers prompts from scripted keys.
    /// </summary>
    private sealed class ScriptedConsole(IAnsiConsole inner, IEnumerable<ConsoleKeyInfo> keys) : IAnsiConsole, IAnsiConsoleInput
    {
        private readonly Queue<ConsoleKeyInfo> pending = new(keys);

        public Profile Profile => inner.Profile;

        public IAnsiConsoleCursor Cursor => inner.Cursor;

        public IAnsiConsoleInput Input => this;

        public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;

        public RenderPipeline Pipeline => inner.Pipeline;

        public void Clear(bool home) => inner.Clear(home);

        public void Write(IRenderable renderable) => inner.Write(renderable);

        public void WriteAnsi(Action<AnsiWriter> action) => inner.WriteAnsi(action);

        public bool IsKeyAvailable() => pending.Count > 0;

        public ConsoleKeyInfo? ReadKey(bool intercept) =>
            pending.TryDequeue(out var key) ? key : throw new InvalidOperationException("No scripted key left.");

        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) =>
            Task.FromResult(ReadKey(intercept));
    }
}
