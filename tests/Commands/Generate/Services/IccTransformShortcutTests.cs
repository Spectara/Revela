using NetVips;
using Spectara.Revela.Features.Generate.Services;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// The ICC conversion to sRGB is skipped only when it provably changes no pixel of a frame.
/// </summary>
/// <remarks>
/// Common "sRGB IEC61966-2.1" profiles are not an exact identity: converting them to libvips'
/// sRGB raises the red channel by one for some vivid greens and cyans. A shortcut based on the
/// profile name alone would therefore change published pixels.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public sealed class IccTransformShortcutTests
{
    /// <summary>
    /// Behaves like converting an "sRGB IEC61966-2.1" profile: red + 1 where red ≤ 22 and green ≥ 208.
    /// </summary>
    private static Image NearIdentityTransform(Image image)
    {
        using var red = image[0];
        using var green = image[1];
        using var lowRed = red <= 22;
        using var highGreen = green >= 208;
        using var affected = lowRed & highGreen;
        using var shifted = image + new double[] { 1, 0, 0 };
        using var chosen = affected.Ifthenelse(shifted, image);
        return chosen.Cast(Enums.BandFormat.Uchar);
    }

    [TestMethod]
    public void FindChangedColors_BuiltInSrgbProfile_ReturnsNull()
    {
        var profile = BuiltInProfile("srgb");

        var changed = IccTransformShortcut.FindChangedColors(profile, NetVipsImageProcessor.TransformToOutputProfile);

        Assert.IsNull(changed, "Converting libvips' own sRGB profile to sRGB must change no color.");
    }

    [TestMethod]
    public void FindChangedColors_NearIdentityTransform_ReturnsTightBox()
    {
        var changed = IccTransformShortcut.FindChangedColors(UniqueProfile(), NearIdentityTransform);

        Assert.AreEqual(new ColorBox(0, 22, 208, 255, 0, 255), changed);
    }

    [TestMethod]
    public void FindChangedColors_DisplayP3Profile_CoversTooManyColorsToCheck()
    {
        var changed = IccTransformShortcut.FindChangedColors(BuiltInProfile("p3"), NetVipsImageProcessor.TransformToOutputProfile);

        Assert.IsNotNull(changed);
        Assert.IsGreaterThan(IccTransformShortcut.MaxCheckedColors, changed.Value.Count);
    }

    [TestMethod]
    [DataRow(10, 220, 0, true, DisplayName = "color inside the box")]
    [DataRow(23, 220, 0, false, DisplayName = "red above the box")]
    [DataRow(10, 207, 0, false, DisplayName = "green below the box")]
    public void ContainsAny_SinglePixelFrame_DetectsColorsInBox(int red, int green, int blue, bool expected)
    {
        using var frame = Frame([40, 40, 40], [red, green, blue], profile: null);

        var contains = IccTransformShortcut.ContainsAny(frame, new ColorBox(0, 22, 208, 255, 0, 255));

        Assert.AreEqual(expected, contains);
    }

    [TestMethod]
    public void IsNoOp_FrameWithoutChangedColors_ReturnsTrue()
    {
        using var frame = Frame([40, 220, 0], [200, 30, 30], UniqueProfile());

        Assert.IsTrue(IccTransformShortcut.IsNoOp(frame, NearIdentityTransform));
    }

    [TestMethod]
    public void IsNoOp_FrameWithOneChangedColor_ReturnsFalse()
    {
        using var frame = Frame([40, 220, 0], [5, 230, 0], UniqueProfile());

        Assert.IsFalse(IccTransformShortcut.IsNoOp(frame, NearIdentityTransform));
    }

    [TestMethod]
    public void IsNoOp_FrameTaggedWithDisplayP3_ReturnsFalse()
    {
        using var frame = Frame([40, 40, 40], [40, 40, 40], BuiltInProfile("p3"));

        Assert.IsFalse(IccTransformShortcut.IsNoOp(frame, NetVipsImageProcessor.TransformToOutputProfile));
    }

    [TestMethod]
    public void IsNoOp_FrameWithAlpha_ReturnsFalse()
    {
        using var frame = Frame([40, 40, 40], [40, 40, 40], BuiltInProfile("srgb"));
        using var withAlpha = frame.Bandjoin(255).Cast(Enums.BandFormat.Uchar);

        Assert.IsFalse(IccTransformShortcut.IsNoOp(withAlpha, NetVipsImageProcessor.TransformToOutputProfile));
    }

    [TestMethod]
    public void IsNoOp_UnreadableProfile_AgreesWithTheConversion()
    {
        // libvips ignores a corrupt embedded profile and converts as if the frame were sRGB.
        using var frame = Frame([5, 230, 0], [250, 10, 200], [1, 2, 3, 4]);

        var noOp = IccTransformShortcut.IsNoOp(frame, NetVipsImageProcessor.TransformToOutputProfile);

        Assert.IsTrue(noOp);
        using var converted = NetVipsImageProcessor.TransformToOutputProfile(frame);
        CollectionAssert.AreEqual(frame.WriteToMemory<byte>(), converted.WriteToMemory<byte>());
    }

    /// <summary>
    /// A 16×16 sRGB frame of <paramref name="fill"/> with one <paramref name="pixel"/> in the corner.
    /// </summary>
    private static Image Frame(double[] fill, int[] pixel, byte[]? profile)
    {
        using var black = Image.Black(16, 16, bands: 3);
        using var filled = black + fill;
        using var drawn = filled.Mutate(m => m.DrawRect([.. pixel.Select(v => (double)v)], 15, 15, 1, 1, fill: true));
        var frame = drawn.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        if (profile is null)
        {
            return frame;
        }

        using (frame)
        {
            return frame.Mutate(m => m.Set(GValue.BlobType, IccTransformShortcut.ProfileField, profile));
        }
    }

    private static byte[] BuiltInProfile(string name)
    {
        using var black = Image.Black(4, 4, bands: 3);
        using var pixels = black.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        using var tagged = pixels.IccTransform(name, inputProfile: "srgb");
        return (byte[])tagged.Get(IccTransformShortcut.ProfileField);
    }

    /// <summary>Distinct profile bytes, so results cached per profile never leak between tests.</summary>
    private static byte[] UniqueProfile() => Guid.NewGuid().ToByteArray();
}
