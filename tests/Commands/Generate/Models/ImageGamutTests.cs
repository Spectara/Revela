using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Models.Manifest;
using Image = Spectara.Revela.Features.Generate.Models.Image;

namespace Spectara.Revela.Tests.Commands.Generate.Models;

/// <summary>
/// Which photos are published in Display P3, and what that changes for caching and templates.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ImageGamutTests
{
    [TestMethod]
    [DataRow("p3", true, "p3")]
    [DataRow("p3", false, "srgb")]
    [DataRow("srgb", true, "srgb")]
    [DataRow(null, true, "srgb")]
    public void Published_ContentGamutAndSwitch_ReturnsVariantGamut(string? contentGamut, bool wideGamut, string expected) =>
        Assert.AreEqual(expected, ImageGamut.Published(contentGamut, wideGamut));

    [TestMethod]
    [DataRow(new[] { 160, 640, 1920, 2560, 4000 }, 1920)]
    [DataRow(new[] { 320, 640, 1280 }, 1280)]
    [DataRow(new[] { 2560, 3000 }, 2560)]
    public void SelectSocialCopySize_Sizes_PicksLargestUpTo1920ElseSmallest(int[] sizes, int expected) =>
        Assert.AreEqual(expected, ImageGamut.SelectSocialCopySize(sizes));

    [TestMethod]
    public void SelectSocialCopySize_NoSizes_ReturnsNull() =>
        Assert.IsNull(ImageGamut.SelectSocialCopySize([]));

    [TestMethod]
    public void ComputeProcessingFingerprint_SrgbGamut_KeepsTheFingerprintOfEarlierVersions()
    {
        // sRGB photos processed before wide-gamut output existed must not be re-encoded.
        var written = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var fingerprint = ImageService.ComputeProcessingFingerprint(1234, written, "longest", gamut: ImageGamut.Srgb);

        Assert.AreEqual($"v{NetVipsImageProcessor.OutputVersion}|size:1234|mtime:{written.Ticks}|resize:longest", fingerprint);
    }

    [TestMethod]
    public void ComputeProcessingFingerprint_P3Gamut_DiffersFromSrgb()
    {
        var written = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var srgb = ImageService.ComputeProcessingFingerprint(1234, written, "longest", 3840, ImageGamut.Srgb);
        var p3 = ImageService.ComputeProcessingFingerprint(1234, written, "longest", 3840, ImageGamut.P3);

        Assert.AreEqual($"{srgb}|gamut:p3", p3);
    }

    [TestMethod]
    [DataRow("p3", true, "p3")]
    [DataRow("p3", false, "srgb")]
    [DataRow(null, true, "srgb")]
    public void FromManifestEntry_ScannedGamut_ExposesThePublishedGamut(string? scanned, bool wideGamut, string expected)
    {
        var entry = new ImageContent { Filename = "photo.jpg", Width = 800, Height = 600, Sizes = [640], Gamut = scanned };

        var image = Image.FromManifestEntry("Photos/photo.jpg", entry, wideGamut);

        Assert.AreEqual(expected, image.Gamut);
    }
}
