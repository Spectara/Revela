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
    [DataRow("plugin")]
    [DataRow("theme")]
    public async Task ExecuteAsync_PackageUninstall_IsBlockedInInteractiveMenu(string parentName)
    {
        var invoked = false;
        var uninstall = new Command("uninstall");
        uninstall.SetAction(_ =>
        {
            invoked = true;
            return 0;
        });
        var root = new RootCommand { new Command(parentName) { uninstall } };
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);

        var (exitCode, output) = await RunQuietAsync(
            () => executor.ExecuteAsync(root, uninstall, [parentName, "uninstall"], CancellationToken.None));

        Assert.IsFalse(invoked, "Uninstall deletes loaded assemblies and must not run inside the menu.");
        Assert.AreEqual(0, exitCode);
        Assert.Contains($"revela {parentName} uninstall", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ThemeInstall_RunsCommand()
    {
        var invoked = false;
        var install = new Command("install");
        install.SetAction(_ =>
        {
            invoked = true;
            return 0;
        });
        var root = new RootCommand { new Command("theme") { install } };
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);

        var (exitCode, _) = await RunQuietAsync(
            () => executor.ExecuteAsync(root, install, ["theme", "install"], CancellationToken.None));

        Assert.IsTrue(invoked);
        Assert.AreEqual(0, exitCode);
    }

    private static async Task<(int ExitCode, string Output)> RunQuietAsync(Func<Task<int>> action)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
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
