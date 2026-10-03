using System.CommandLine;
using System.Globalization;

using Microsoft.Extensions.Logging.Abstractions;

using Spectara.Revela.Cli.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Tests.Cli.Hosting;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class CommandExecutorTests
{
    [TestMethod]
    [DataRow("plugin", "uninstall")]
    [DataRow("theme", "uninstall")]
    [DataRow("plugin", "install")]
    [DataRow("theme", "install")]
    public async Task ExecuteAsync_PackageInstallOrUninstall_IsBlockedInInteractiveMenu(string parentName, string subcommandName)
    {
        var invoked = false;
        var subcommand = new Command(subcommandName);
        subcommand.SetAction(_ =>
        {
            invoked = true;
            return 0;
        });
        var root = new RootCommand { new Command(parentName) { subcommand } };
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);

        var (exitCode, output) = await RunQuietAsync(
            () => executor.ExecuteAsync(root, subcommand, [parentName, subcommandName], CancellationToken.None));

        Assert.IsFalse(invoked, "Install/uninstall replace or delete loaded package files and must not run inside the menu.");
        Assert.AreEqual(0, exitCode);
        Assert.Contains($"revela {parentName} {subcommandName}", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThemeList_RunsCommand()
    {
        var invoked = false;
        var list = new Command("list");
        list.SetAction(_ =>
        {
            invoked = true;
            return 0;
        });
        var root = new RootCommand { new Command("theme") { list } };
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);

        var (exitCode, _) = await RunQuietAsync(
            () => executor.ExecuteAsync(root, list, ["theme", "list"], CancellationToken.None));

        Assert.IsTrue(invoked);
        Assert.AreEqual(0, exitCode);
    }

    private static async Task<(int ExitCode, string Output)> RunQuietAsync(Func<Task<int>> action)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 200;
        AnsiConsole.Console = console;

        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
