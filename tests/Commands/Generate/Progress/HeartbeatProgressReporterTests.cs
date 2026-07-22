using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Features.Generate.Models.Results;

namespace Spectara.Revela.Tests.Commands.Generate.Progress;

/// <summary>
/// Tests for <see cref="HeartbeatProgressReporter"/> — the non-interactive
/// (no-TTY) heartbeat path that must never leave a run silent.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class HeartbeatProgressReporterTests
{
    private const char Escape = '\u001b';

    private static ImageProgress Progress(int processed) => new()
    {
        Processed = processed,
        Total = 20,
        DoneByFormat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["jpg"] = processed }
    };

    [TestMethod]
    public void Report_FirstCall_EmitsLine()
    {
        using var writer = new StringWriter();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var reporter = new HeartbeatProgressReporter(writer, time, TimeSpan.FromSeconds(30));

        reporter.Report(Progress(1));

        Assert.IsGreaterThan(0, Lines(writer).Length);
    }

    [TestMethod]
    public void Report_WithinInterval_DoesNotEmitAgain()
    {
        using var writer = new StringWriter();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var reporter = new HeartbeatProgressReporter(writer, time, TimeSpan.FromSeconds(30), everyImages: int.MaxValue);

        reporter.Report(Progress(1));
        time.Advance(TimeSpan.FromSeconds(10));
        reporter.Report(Progress(2));

        Assert.HasCount(1, Lines(writer));
    }

    [TestMethod]
    public void Report_AfterInterval_EmitsAgain()
    {
        using var writer = new StringWriter();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var reporter = new HeartbeatProgressReporter(writer, time, TimeSpan.FromSeconds(30), everyImages: int.MaxValue);

        reporter.Report(Progress(1));
        time.Advance(TimeSpan.FromSeconds(31));
        reporter.Report(Progress(2));

        Assert.HasCount(2, Lines(writer));
    }

    [TestMethod]
    public void Report_EmitsEveryNImagesRegardlessOfTime()
    {
        using var writer = new StringWriter();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var reporter = new HeartbeatProgressReporter(writer, time, TimeSpan.FromHours(1), everyImages: 5);

        reporter.Report(Progress(1));  // first → emit
        reporter.Report(Progress(3));  // +2 → no
        reporter.Report(Progress(6));  // +5 since last emit → emit

        Assert.HasCount(2, Lines(writer));
    }

    [TestMethod]
    public void Report_MultiImageRun_EmitsPlainGreppableLines()
    {
        using var writer = new StringWriter();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var reporter = new HeartbeatProgressReporter(writer, time, TimeSpan.FromSeconds(30), everyImages: 5);

        for (var i = 1; i <= 20; i++)
        {
            reporter.Report(Progress(i));
        }

        var lines = Lines(writer);
        Assert.IsGreaterThan(0, lines.Length, "A multi-image run must not be silent.");
        foreach (var line in lines)
        {
            Assert.IsFalse(line.Contains(Escape, StringComparison.Ordinal), "Heartbeat lines must be plain (no ANSI).");
            Assert.IsTrue(line.StartsWith("[generate images]", StringComparison.Ordinal));
        }
    }

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan delta) => now += delta;
    }
}
