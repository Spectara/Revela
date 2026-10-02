using System.Globalization;

using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Tests.Core.Output;

[TestClass]
[TestCategory("Unit")]
public sealed class PlainProgressReporterTests
{
    [TestMethod]
    public void Report_EveryItem_WritesOneLinePerTenPercentStep()
    {
        var (reporter, writer) = Create("Compressed");
        IProgress<(int Current, int Total)> progress = reporter;

        for (var current = 1; current <= 25; current++)
        {
            progress.Report((current, 25));
        }

        var lines = Lines(writer);
        Assert.HasCount(11, lines);
        Assert.AreEqual("Compressed 1/25 file(s)", lines[0]);
        Assert.AreEqual("Compressed 25/25 file(s)", lines[^1]);
    }

    [TestMethod]
    public void Report_OutOfOrderLowerCount_IsIgnored()
    {
        var (reporter, writer) = Create("Downloaded");
        IProgress<(int Current, int Total)> progress = reporter;

        progress.Report((10, 10));
        progress.Report((5, 10));

        Assert.AreEqual("Downloaded 10/10 file(s)", Assert.ContainsSingle(Lines(writer)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Report_NoTotal_WritesNothing(int total)
    {
        var (reporter, writer) = Create("Compressed");
        IProgress<(int Current, int Total)> progress = reporter;

        progress.Report((0, total));

        Assert.IsEmpty(Lines(writer));
    }

    [TestMethod]
    public void Report_WithItemName_WritesCountWithoutItem()
    {
        var (reporter, writer) = Create("Compressed");
        IProgress<(int Current, int Total, string Item)> progress = reporter;

        progress.Report((1, 1, "index.html"));

        Assert.AreEqual("Compressed 1/1 file(s)", Assert.ContainsSingle(Lines(writer)));
    }

    [TestMethod]
    public async Task Report_ParallelWorkers_NeverWritesALowerCountAfterAHigherOne()
    {
        var (reporter, writer) = Create("Downloaded");
        IProgress<(int Current, int Total)> progress = reporter;

        await Parallel.ForAsync(1, 1001, (current, _) =>
        {
            progress.Report((current, 1000));
            return ValueTask.CompletedTask;
        });

        var counts = Lines(writer)
            .Select(line => int.Parse(line.Split(' ')[1].Split('/')[0], CultureInfo.InvariantCulture))
            .ToList();
        Assert.IsLessThanOrEqualTo(11, counts.Count);
        CollectionAssert.AreEqual(counts.Order().ToList(), counts);
    }

    private static (PlainProgressReporter Reporter, StringWriter Writer) Create(string verb)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 200;
        return (new PlainProgressReporter(verb, console), writer);
    }

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}
