using System.Collections.Concurrent;
using System.Security.Cryptography;
using NetVips;
using Image = NetVips.Image;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Decides whether converting a frame from its embedded ICC profile to sRGB would change any pixel.
/// </summary>
/// <remarks>
/// <para>
/// Most photos are exported as sRGB, and the conversion then costs about a third of a JPEG-only
/// build while changing almost nothing. "Almost" matters: the widespread "sRGB IEC61966-2.1"
/// profile is not an exact identity against libvips' sRGB, so for some vivid greens and cyans
/// the red channel moves by one. Skipping by profile name would change published pixels.
/// </para>
/// <para>
/// The conversion is a per-pixel function of the color, so it is evaluated once per profile for
/// all 16.7 million 8-bit colors. The colors it changes are recorded as a bounding box, and a
/// frame skips the conversion only when none of its pixels lies in that box. The output is
/// therefore byte-identical by construction; a frame that might be affected is converted.
/// </para>
/// </remarks>
internal static class IccTransformShortcut
{
    /// <summary>libvips metadata field holding the embedded ICC profile.</summary>
    internal const string ProfileField = "icc-profile-data";

    /// <summary>
    /// Largest number of colors a changed-color box may span and still be checked per frame.
    /// </summary>
    /// <remarks>
    /// One sixteenth of all colors. Real wide-gamut profiles (Display P3, Adobe RGB) change nearly
    /// every color, and checking their frames would only add a pass before the conversion runs anyway.
    /// </remarks>
    internal const long MaxCheckedColors = 256L * 256 * 256 / 16;

    /// <summary>Colors evaluated per libvips call: 16 values of blue × all red/green pairs.</summary>
    private const int BluePerChunk = 16;

    /// <summary>Changed colors per profile (SHA-256 of the profile bytes); null = the conversion changes no color.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<ColorBox?>> ChangedColorsByProfile = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns whether <paramref name="transform"/> leaves every pixel of <paramref name="frame"/> unchanged.
    /// </summary>
    /// <remarks>
    /// Only 8-bit, three-band sRGB frames with an embedded profile qualify. The frame is read
    /// once when a check is needed, so pass a materialized frame.
    /// </remarks>
    /// <param name="frame">Frame that would be converted.</param>
    /// <param name="transform">The exact conversion that would be applied.</param>
    public static bool IsNoOp(Image frame, Func<Image, Image> transform)
    {
        if (frame.Format != Enums.BandFormat.Uchar
            || frame.Bands != 3
            || frame.Interpretation != Enums.Interpretation.Srgb
            || !frame.Contains(ProfileField)
            || frame.Get(ProfileField) is not byte[] { Length: > 0 } profile)
        {
            return false;
        }

        var key = Convert.ToHexString(SHA256.HashData(profile));
        var changed = ChangedColorsByProfile
            .GetOrAdd(key, _ => new Lazy<ColorBox?>(() => FindChangedColors(profile, transform)))
            .Value;

        return changed switch
        {
            null => true,
            { Count: > MaxCheckedColors } => false,
            { } box => !ContainsAny(frame, box)
        };
    }

    /// <summary>
    /// Finds the 8-bit colors that <paramref name="transform"/> changes for <paramref name="profile"/>.
    /// </summary>
    /// <returns>Bounding box of the changed colors, <c>null</c> if none changes, or <see cref="ColorBox.All"/> if the profile cannot be evaluated.</returns>
    internal static ColorBox? FindChangedColors(byte[] profile, Func<Image, Image> transform)
    {
        var (minRed, maxRed, minGreen, maxGreen, minBlue, maxBlue) = (255, -1, 255, -1, 255, -1);

        try
        {
            for (var firstBlue = 0; firstBlue < 256; firstBlue += BluePerChunk)
            {
                // A fresh buffer per chunk: libvips reads the pinned array directly.
                var pixels = new byte[256 * 256 * BluePerChunk * 3];
                var index = 0;
                for (var blue = firstBlue; blue < firstBlue + BluePerChunk; blue++)
                {
                    for (var green = 0; green < 256; green++)
                    {
                        for (var red = 0; red < 256; red++)
                        {
                            pixels[index++] = (byte)red;
                            pixels[index++] = (byte)green;
                            pixels[index++] = (byte)blue;
                        }
                    }
                }

                byte[] converted;
                using (var raw = Image.NewFromMemory(pixels, 256, 256 * BluePerChunk, 3, Enums.BandFormat.Uchar))
                using (var srgb = raw.Copy(interpretation: Enums.Interpretation.Srgb))
                using (var tagged = srgb.Mutate(image => image.Set(GValue.BlobType, ProfileField, profile)))
                using (var result = transform(tagged))
                {
                    if (result.Format != Enums.BandFormat.Uchar || result.Bands != 3)
                    {
                        return ColorBox.All;
                    }

                    converted = result.WriteToMemory<byte>();
                }

                for (var i = 0; i < pixels.Length; i += 3)
                {
                    if (pixels[i] == converted[i] && pixels[i + 1] == converted[i + 1] && pixels[i + 2] == converted[i + 2])
                    {
                        continue;
                    }

                    (minRed, maxRed) = (Math.Min(minRed, pixels[i]), Math.Max(maxRed, pixels[i]));
                    (minGreen, maxGreen) = (Math.Min(minGreen, pixels[i + 1]), Math.Max(maxGreen, pixels[i + 1]));
                    (minBlue, maxBlue) = (Math.Min(minBlue, pixels[i + 2]), Math.Max(maxBlue, pixels[i + 2]));
                }
            }
        }
        catch (VipsException)
        {
            // An unusable profile is left to the regular conversion and its error handling.
            return ColorBox.All;
        }

        return maxRed < 0 ? null : new ColorBox(minRed, maxRed, minGreen, maxGreen, minBlue, maxBlue);
    }

    /// <summary>
    /// Returns whether any pixel of a three-band frame lies in <paramref name="box"/>.
    /// </summary>
    internal static bool ContainsAny(Image frame, ColorBox box)
    {
        List<Image> intermediates = [];
        try
        {
            Image? inBox = null;
            AddBounds(0, box.MinRed, box.MaxRed);
            AddBounds(1, box.MinGreen, box.MaxGreen);
            AddBounds(2, box.MinBlue, box.MaxBlue);

            // A box spanning every color: any pixel is in it.
            return inBox is null || inBox.Max() > 0;

            void AddBounds(int band, int min, int max)
            {
                if (min <= 0 && max >= 255)
                {
                    return;
                }

                var channel = Keep(frame[band]);
                if (min > 0)
                {
                    And(Keep(channel >= min));
                }

                if (max < 255)
                {
                    And(Keep(channel <= max));
                }
            }

            void And(Image condition) => inBox = inBox is null ? condition : Keep(inBox & condition);

            Image Keep(Image image)
            {
                intermediates.Add(image);
                return image;
            }
        }
        finally
        {
            foreach (var image in intermediates)
            {
                image.Dispose();
            }
        }
    }
}

/// <summary>
/// Inclusive box of 8-bit RGB colors.
/// </summary>
internal readonly record struct ColorBox(int MinRed, int MaxRed, int MinGreen, int MaxGreen, int MinBlue, int MaxBlue)
{
    /// <summary>Every 8-bit color.</summary>
    public static ColorBox All { get; } = new(0, 255, 0, 255, 0, 255);

    /// <summary>Number of colors in the box.</summary>
    public long Count => (long)(MaxRed - MinRed + 1) * (MaxGreen - MinGreen + 1) * (MaxBlue - MinBlue + 1);
}
