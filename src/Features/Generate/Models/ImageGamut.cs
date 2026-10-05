namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Colour gamuts of photos and of their published variants.
/// </summary>
/// <remarks>
/// <para>
/// The scan records the gamut of each photo's content (<see cref="Sdk.Models.Manifest.ImageContent.Gamut"/>).
/// Photos with visible colours outside sRGB are published in Display P3 with the profile embedded,
/// unless <c>generate.images.wideGamut</c> is off; everything else is published as untagged sRGB.
/// </para>
/// <para>
/// Social platforms often drop embedded profiles, which would show P3 pixels desaturated. P3 photos
/// therefore also get one untagged sRGB JPEG, <c>{size}.srgb.jpg</c>, for <c>og:image</c>.
/// </para>
/// </remarks>
internal static class ImageGamut
{
    /// <summary>Content within sRGB; variants are untagged sRGB.</summary>
    public const string Srgb = "srgb";

    /// <summary>Content with visible colours outside sRGB; variants are Display P3 with the profile embedded.</summary>
    public const string P3 = "p3";

    /// <summary>
    /// File "format" of the sRGB JPEG that P3 photos get for social previews (<c>{size}.srgb.jpg</c>).
    /// </summary>
    /// <remarks>
    /// Templates pass it to <c>variant_url</c> like a format: <c>absolute_variant_url image size 'srgb.jpg'</c>.
    /// </remarks>
    public const string SocialCopyFormat = "srgb.jpg";

    /// <summary>
    /// Largest size of the social copy, the common <c>og:image</c> limit (Lumina's OpenGraphImage partial uses the same).
    /// </summary>
    public const int SocialCopyMaxSize = 1920;

    /// <summary>
    /// JPEG quality of the social copy when the project generates no JPEG variants.
    /// </summary>
    public const int SocialCopyDefaultQuality = 90;

    /// <summary>
    /// Gamut of the published variants for a photo with the scanned <paramref name="contentGamut"/>.
    /// </summary>
    public static string Published(string? contentGamut, bool wideGamut) =>
        wideGamut && string.Equals(contentGamut, P3, StringComparison.Ordinal) ? P3 : Srgb;

    /// <summary>
    /// Size of the social copy: the largest of <paramref name="sizes"/> up to
    /// <see cref="SocialCopyMaxSize"/>, else the smallest; <c>null</c> without sizes.
    /// </summary>
    public static int? SelectSocialCopySize(IReadOnlyList<int> sizes)
    {
        if (sizes.Count == 0)
        {
            return null;
        }

        var withinLimit = sizes.Where(size => size <= SocialCopyMaxSize).ToList();
        return withinLimit.Count > 0 ? withinLimit.Max() : sizes.Min();
    }
}
