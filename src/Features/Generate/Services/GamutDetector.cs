using System.Collections.Concurrent;
using System.Security.Cryptography;
using NetVips;
using Image = NetVips.Image;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Measures how much of a photo is visibly outside sRGB.
/// </summary>
/// <remarks>
/// <para>
/// Pixels are converted through the embedded profile into unbounded linear sRGB, where colours
/// outside sRGB have channels below 0 or above 1, and compared with their clipped (sRGB) version
/// in CIEDE2000. Measuring visible differences instead of any channel beyond [0, 1] keeps in-gamut
/// photos with a Display P3 or Adobe RGB profile in sRGB: JPEG noise pushes single pixels of their
/// most saturated colours just outside sRGB.
/// </para>
/// <para>
/// The ICC profile connection space is XYZ: libvips returns it relative to D65, matching its
/// scRGB. (Its Lab connection space is D50 and would need a chromatic adaptation first.)
/// </para>
/// <para>
/// Most photos carry an sRGB profile, which cannot encode any colour outside sRGB. Each 8-bit RGB
/// profile is therefore checked once on a grid of its colours, and photos with a profile that
/// fits inside sRGB skip the per-pixel measurement.
/// </para>
/// </remarks>
internal static class GamutDetector
{
    /// <summary>libvips metadata field holding the embedded ICC profile.</summary>
    internal const string ProfileField = IccTransformShortcut.ProfileField;

    /// <summary>
    /// CIEDE2000 difference between a colour and its sRGB-clipped version above which the
    /// colour counts as visibly outside sRGB.
    /// </summary>
    /// <remarks>
    /// In-gamut photos stored in Display P3 or Adobe RGB pick up JPEG noise just outside sRGB
    /// (up to ΔE00 4.75 for single pixels, measured on 120 real photos); a threshold of 3 with
    /// <see cref="MinOutOfGamutShare"/> keeps all of them sRGB.
    /// </remarks>
    internal const double OutOfGamutDifference = 3.0;

    /// <summary>
    /// Share of the scan thumbnail's pixels that must be visibly outside sRGB for a P3 photo.
    /// </summary>
    /// <remarks>
    /// 0.1% (about 45 pixels of a 256 px thumbnail): ignores noise (at most 0.03% in the
    /// in-gamut samples) but still catches a saturated flower in an otherwise neutral photo.
    /// </remarks>
    internal const double MinOutOfGamutShare = 0.001;

    /// <summary>Grid points per channel when checking whether a profile fits inside sRGB.</summary>
    private const int GridSteps = 33;

    /// <summary>Whether a profile (SHA-256 of its bytes) can only encode colours inside sRGB.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<bool>> FitsInSrgbByProfile = new(StringComparer.Ordinal);

    /// <summary>
    /// Share of <paramref name="thumbnail"/>'s pixels that are visibly outside sRGB (0 to 1).
    /// </summary>
    /// <param name="thumbnail">Materialized image with its embedded profile.</param>
    /// <exception cref="VipsException">The profile cannot be applied.</exception>
    public static double MeasureOutOfGamutShare(Image thumbnail)
    {
        if (thumbnail.Format == Enums.BandFormat.Uchar
            && thumbnail.Bands == 3
            && thumbnail.Contains(ProfileField)
            && thumbnail.Get(ProfileField) is byte[] { Length: > 0 } profile)
        {
            var key = Convert.ToHexString(SHA256.HashData(profile));
            var fitsInSrgb = FitsInSrgbByProfile
                .GetOrAdd(key, _ => new Lazy<bool>(() => FitsInSrgb(profile)))
                .Value;
            if (fitsInSrgb)
            {
                return 0;
            }
        }

        using var difference = DifferenceToSrgb(thumbnail);
        using var visible = difference > OutOfGamutDifference;
        return visible.Avg() / 255.0;
    }

    /// <summary>
    /// Returns whether no 8-bit RGB colour of <paramref name="profile"/> is visibly outside sRGB.
    /// </summary>
    /// <remarks>
    /// The gamut of an RGB profile is spanned by its colour cube, so a grid of
    /// <see cref="GridSteps"/>³ colours (including every corner: the primaries and secondaries)
    /// finds any part of it beyond sRGB. libvips imports a broken profile as sRGB, so it fits; a
    /// profile libvips rejects does not fit and leaves the decision to the per-pixel measurement.
    /// </remarks>
    internal static bool FitsInSrgb(byte[] profile)
    {
        var pixels = new byte[GridSteps * GridSteps * GridSteps * 3];
        var index = 0;
        for (var blue = 0; blue < GridSteps; blue++)
        {
            for (var green = 0; green < GridSteps; green++)
            {
                for (var red = 0; red < GridSteps; red++)
                {
                    pixels[index++] = GridValue(red);
                    pixels[index++] = GridValue(green);
                    pixels[index++] = GridValue(blue);
                }
            }
        }

        try
        {
            using var raw = Image.NewFromMemory(pixels, GridSteps * GridSteps, GridSteps, 3, Enums.BandFormat.Uchar);
            using var srgb = raw.Copy(interpretation: Enums.Interpretation.Srgb);
            using var tagged = srgb.Mutate(image => image.Set(GValue.BlobType, ProfileField, profile));
            using var difference = DifferenceToSrgb(tagged);
            return difference.Max() <= OutOfGamutDifference;
        }
        catch (VipsException)
        {
            return false;
        }

        static byte GridValue(int step) => (byte)Math.Round(step * 255.0 / (GridSteps - 1));
    }

    /// <summary>
    /// Per pixel, the CIEDE2000 difference between the colour and its clipped sRGB version.
    /// </summary>
    private static Image DifferenceToSrgb(Image image)
    {
        using var imported = image.IccImport(embedded: true, intent: Enums.Intent.Relative, pcs: Enums.PCS.Xyz);
        using var xyz = imported.Bands > 3 ? imported.ExtractBand(0, n: 3) : null;
        using var linear = (xyz ?? imported).Colourspace(Enums.Interpretation.Scrgb);
        using var unbounded = linear.CopyMemory();
        using var clipped = unbounded.Clamp(0, 1);
        using var lab = unbounded.Colourspace(Enums.Interpretation.Lab);
        using var clippedLab = clipped.Colourspace(Enums.Interpretation.Lab, sourceSpace: Enums.Interpretation.Scrgb);
        using var difference = lab.DE00(clippedLab);
        return difference.CopyMemory();
    }
}
