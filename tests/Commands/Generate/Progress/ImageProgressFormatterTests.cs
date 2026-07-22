using Spectara.Revela.Features.Generate.Models.Results;

namespace Spectara.Revela.Tests.Commands.Generate.Progress;

/// <summary>
/// Tests for <see cref="ImageProgressFormatter"/> — pure, ANSI-free formatting
/// used by the non-interactive heartbeat.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ImageProgressFormatterTests
{
    private const char Escape = '\u001b';

    private static ImageProgress Sample() => new()
    {
        Processed = 512,
        Total = 1197,
        Skipped = 3,
        WorkersBusy = 16,
        ImagesPerMinute = 132,
        Elapsed = TimeSpan.FromMinutes(18),
        Eta = TimeSpan.FromMinutes(24),
        DoneByFormat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["avif"] = 512,
            ["webp"] = 998,
            ["jpg"] = 1120
        }
    };

    [TestMethod]
    public void Heartbeat_ContainsAllSegments()
    {
        var line = ImageProgressFormatter.Heartbeat(Sample());

        Assert.Contains("[generate images]", line, StringComparison.Ordinal);
        Assert.Contains("512/1197 (42%)", line, StringComparison.Ordinal);
        Assert.Contains("avif 512", line, StringComparison.Ordinal);
        Assert.Contains("webp 998", line, StringComparison.Ordinal);
        Assert.Contains("jpg 1120", line, StringComparison.Ordinal);
        Assert.Contains("132 img/min", line, StringComparison.Ordinal);
        Assert.Contains("~24m left", line, StringComparison.Ordinal);
        Assert.Contains("3 skipped", line, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Heartbeat_IsFreeOfAnsiEscapeCodes()
    {
        var line = ImageProgressFormatter.Heartbeat(Sample());

        Assert.IsFalse(line.Contains(Escape, StringComparison.Ordinal), "Heartbeat output must be plain text (no ANSI).");
        Assert.IsFalse(line.Contains("[/]", StringComparison.Ordinal), "Heartbeat must not contain Spectre markup.");
    }

    [TestMethod]
    public void Heartbeat_WithoutEta_ShowsEstimating()
    {
        var progress = Sample() with { Eta = null };

        var line = ImageProgressFormatter.Heartbeat(progress);

        Assert.Contains("estimating…", line, StringComparison.Ordinal);
        Assert.IsFalse(line.Contains("left", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Heartbeat_WithoutRate_OmitsImgPerMin()
    {
        var progress = Sample() with { ImagesPerMinute = 0 };

        var line = ImageProgressFormatter.Heartbeat(progress);

        Assert.IsFalse(line.Contains("img/min", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Heartbeat_WithoutSkips_OmitsSkipped()
    {
        var progress = Sample() with { Skipped = 0 };

        var line = ImageProgressFormatter.Heartbeat(progress);

        Assert.IsFalse(line.Contains("skipped", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Percent_ComputesIntegerPercentage()
    {
        Assert.AreEqual(0, ImageProgressFormatter.Percent(0, 0));
        Assert.AreEqual(0, ImageProgressFormatter.Percent(0, 100));
        Assert.AreEqual(50, ImageProgressFormatter.Percent(50, 100));
        Assert.AreEqual(100, ImageProgressFormatter.Percent(100, 100));
    }

    [TestMethod]
    public void Duration_FormatsSecondsMinutesAndHours()
    {
        Assert.AreEqual("45s", ImageProgressFormatter.Duration(TimeSpan.FromSeconds(45)));
        Assert.AreEqual("18m", ImageProgressFormatter.Duration(TimeSpan.FromMinutes(18)));
        Assert.AreEqual("1h 5m", ImageProgressFormatter.Duration(TimeSpan.FromMinutes(65)));
    }

    [TestMethod]
    public void Duration_ClampsNegativeToZero() =>
        Assert.AreEqual("0s", ImageProgressFormatter.Duration(TimeSpan.FromSeconds(-5)));

    [TestMethod]
    public void Bar_FillsProportionally()
    {
        var bar = ImageProgressFormatter.Bar(processed: 5, total: 10, width: 20, filled: '#', empty: '.');

        Assert.HasCount(20, bar);
        Assert.AreEqual(10, bar.Count(c => c == '#'));
        Assert.AreEqual(10, bar.Count(c => c == '.'));
    }

    [TestMethod]
    public void Bar_WithZeroTotal_IsAllEmpty()
    {
        var bar = ImageProgressFormatter.Bar(processed: 0, total: 0, width: 8, filled: '#', empty: '.');

        Assert.AreEqual("........", bar);
    }
}
