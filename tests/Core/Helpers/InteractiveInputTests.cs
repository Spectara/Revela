using System.Globalization;

using NSubstitute;

using Spectara.Revela.Core.Helpers;
using Spectara.Revela.Sdk.Hosting;

using Spectre.Console;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class InteractiveInputTests
{
    [TestMethod]
    public void EnsureInteractive_InteractiveConsole_ReturnsTrueWithoutOutput()
    {
        var (result, output) = Run(interactive: true, "revela config image --formats jpg");

        Assert.IsTrue(result);
        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void EnsureInteractive_NonInteractiveConsole_ReturnsFalseAndNamesEveryUsage()
    {
        var (result, output) = Run(
            interactive: false,
            "revela plugin install <name>",
            "revela plugin install --all");

        Assert.IsFalse(result);
        Assert.Contains("Interactive Input Required", output);
        Assert.Contains(InteractiveInput.NoOptionsGiven, output);
        Assert.Contains("revela plugin install <name>", output);
        Assert.Contains("revela plugin install --all", output);
    }

    [TestMethod]
    public void EnsureInteractive_UsageWithMarkupCharacters_IsPrintedLiterally()
    {
        var (_, output) = Run(interactive: false, "revela config paths --source <dir> [--output <dir>]");

        Assert.Contains("revela config paths --source <dir> [--output <dir>]", output);
    }

    private static (bool Result, string Output) Run(bool interactive, params string[] usages)
    {
        var capabilities = Substitute.For<IConsoleCapabilities>();
        capabilities.IsInteractive.Returns(interactive);

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
            var result = capabilities.EnsureInteractive(InteractiveInput.NoOptionsGiven, usages);
            return (result, writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }
}
