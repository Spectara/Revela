using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NetVips;
using NSubstitute;

using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;

using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// EXIF <c>DateTimeOriginal</c> is the camera's wall-clock time, not UTC.
/// </summary>
/// <remarks>
/// Without <c>OffsetTimeOriginal</c> the time zone is unknown, so the scan keeps the time as
/// <see cref="DateTimeKind.Unspecified"/> and the manifest writes it without a <c>Z</c>. When the
/// camera recorded the offset, it is kept next to the wall-clock time in <c>Exif.Raw</c>.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public sealed class ExifDateTakenTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "revela-exif-date-tests",
        Guid.NewGuid().ToString("N"));

    public ExifDateTakenTests() => Directory.CreateDirectory(directory);

    [TestMethod]
    public async Task ReadMetadataAsync_DateTimeOriginalWithoutOffset_ReturnsUnspecifiedWallClockTime()
    {
        var path = WriteJpeg("2022:07:31 22:22:22", offset: null);

        var metadata = await CreateProcessor().ReadMetadataAsync(path);

        Assert.AreEqual(new DateTime(2022, 7, 31, 22, 22, 22), metadata.DateTaken);
        Assert.AreEqual(DateTimeKind.Unspecified, metadata.DateTaken!.Value.Kind);
        Assert.AreEqual(DateTimeKind.Unspecified, metadata.Exif!.DateTaken!.Value.Kind);
        Assert.IsFalse(metadata.Exif.Raw?.ContainsKey("OffsetTimeOriginal") ?? false);
    }

    [TestMethod]
    public async Task ReadMetadataAsync_DateTimeOriginalWithOffset_KeepsWallClockTimeAndRecordsOffset()
    {
        var path = WriteJpeg("2022:07:31 22:22:22", offset: "+02:00");

        var metadata = await CreateProcessor().ReadMetadataAsync(path);

        Assert.AreEqual(new DateTime(2022, 7, 31, 22, 22, 22), metadata.DateTaken);
        Assert.AreEqual(DateTimeKind.Unspecified, metadata.DateTaken!.Value.Kind);
        Assert.IsNotNull(metadata.Exif?.Raw);
        Assert.AreEqual("+02:00", metadata.Exif.Raw["OffsetTimeOriginal"]);
    }

    [TestMethod]
    public void ManifestJson_UnspecifiedDateTaken_RoundTripsWithoutUtcDesignator()
    {
        var image = new ImageContent
        {
            Filename = "a.jpg",
            Width = 1,
            Height = 1,
            Sizes = [],
            DateTaken = new DateTime(2022, 7, 31, 22, 22, 22, DateTimeKind.Unspecified),
        };

        var json = JsonSerializer.Serialize(image, ManifestJsonContext.Default.ImageContent);
        var roundTripped = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.ImageContent)!;

        Assert.Contains("\"2022-07-31T22:22:22\"", json, StringComparison.Ordinal);
        var dateTaken = roundTripped.DateTaken!.Value;
        Assert.AreEqual(new DateTime(2022, 7, 31, 22, 22, 22), dateTaken);
        Assert.AreEqual(DateTimeKind.Unspecified, dateTaken.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static NetVipsImageProcessor CreateProcessor()
    {
        var config = Substitute.For<IOptionsMonitor<GenerateConfig>>();
        config.CurrentValue.Returns(new GenerateConfig());
        return new NetVipsImageProcessor(
            NullLogger<NetVipsImageProcessor>.Instance,
            new CameraModelMapper(config));
    }

    private string WriteJpeg(string dateTimeOriginal, string? offset)
    {
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".jpg");
        using var black = Image.Black(32, 32, bands: 3);
        using var pixels = black.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var dated = pixels.Mutate(m =>
        {
            m.Set(GValue.GStrType, "exif-ifd2-DateTimeOriginal", dateTimeOriginal);
            if (offset is not null)
            {
                m.Set(GValue.GStrType, "exif-ifd2-OffsetTimeOriginal", offset);
            }
        });
        dated.Jpegsave(path);
        return path;
    }
}
