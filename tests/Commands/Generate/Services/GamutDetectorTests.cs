using NetVips;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Tests.Shared.Fixtures;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Photos whose profile can only encode sRGB colours skip the per-pixel gamut measurement.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class GamutDetectorTests
{
    [TestMethod]
    public void FitsInSrgb_SrgbProfile_ReturnsTrue() =>
        Assert.IsTrue(GamutDetector.FitsInSrgb(TestIccProfiles.BuiltIn("srgb")));

    [TestMethod]
    public void FitsInSrgb_DisplayP3Profile_ReturnsFalse() =>
        Assert.IsFalse(GamutDetector.FitsInSrgb(TestIccProfiles.BuiltIn("p3")));

    [TestMethod]
    public void FitsInSrgb_AdobeRgbProfile_ReturnsFalse() =>
        Assert.IsFalse(GamutDetector.FitsInSrgb(TestIccProfiles.AdobeRgbCompatible()));

    [TestMethod]
    public void FitsInSrgb_UnusableProfile_ReturnsTrue() =>
        // libvips imports a broken embedded profile as sRGB, so such photos stay on the sRGB path.
        Assert.IsTrue(GamutDetector.FitsInSrgb([1, 2, 3, 4]));

    [TestMethod]
    public void MeasureOutOfGamutShare_P3PrimariesInHalfTheImage_ReturnsAboutHalf()
    {
        // Left half: Display P3's pure red, far outside sRGB; right half: neutral grey.
        using var red = Solid([255, 0, 0], 32, 32);
        using var grey = Solid([128, 128, 128], 32, 32);
        using var joined = red.Join(grey, Enums.Direction.Horizontal);
        using var tagged = joined.Mutate(m => m.Set(GValue.BlobType, TestIccProfiles.ProfileField, TestIccProfiles.BuiltIn("p3")));

        var share = GamutDetector.MeasureOutOfGamutShare(tagged);

        Assert.AreEqual(0.5, share, 0.001);
    }

    private static Image Solid(double[] color, int width, int height)
    {
        using var black = Image.Black(width, height, bands: 3);
        using var colored = black + color;
        return colored.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
    }
}
