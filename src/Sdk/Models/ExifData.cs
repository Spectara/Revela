namespace Spectara.Revela.Sdk.Models;

/// <summary>
/// EXIF metadata extracted from an image
/// </summary>
/// <remarks>
/// <para>
/// Contains commonly used EXIF fields as typed properties for convenience.
/// Additional fields are available in the <see cref="Raw"/> dictionary.
/// </para>
/// <para>
/// Only fields with actual values are stored - null/empty fields are omitted
/// to keep the manifest compact.
/// </para>
/// </remarks>
public sealed class ExifData
{
    /// <summary>Camera manufacturer (e.g., "Canon", "Nikon", "Sony")</summary>
    public string? Make { get; init; }

    /// <summary>Camera model (e.g., "EOS 5D Mark IV", "α 7 IV")</summary>
    public string? Model { get; init; }

    /// <summary>Lens model (e.g., "Sony FE 50mm F1.8")</summary>
    public string? LensModel { get; init; }

    /// <summary>Date and time the photo was taken</summary>
    public DateTime? DateTaken { get; init; }

    /// <summary>F-number (aperture, e.g., f/2.8)</summary>
    public double? FNumber { get; init; }

    /// <summary>Exposure time in seconds (e.g., 1/500)</summary>
    public double? ExposureTime { get; init; }

    /// <summary>ISO speed (e.g., 100, 400, 1600)</summary>
    public int? Iso { get; init; }

    /// <summary>Focal length in mm (e.g., 50mm, 85mm)</summary>
    public double? FocalLength { get; init; }

    /// <summary>GPS latitude (decimal degrees)</summary>
    public double? GpsLatitude { get; init; }

    /// <summary>GPS longitude (decimal degrees)</summary>
    public double? GpsLongitude { get; init; }

    /// <summary>
    /// Additional EXIF fields not covered by typed properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains useful photographer-relevant fields like:
    /// Rating, Copyright, Artist, ExposureProgram, MeteringMode, Flash, etc.
    /// </para>
    /// <para>
    /// Only fields with non-empty values are included.
    /// Keys are the EXIF field names without the "exif-ifd" prefix.
    /// </para>
    /// </remarks>
    /// <example>
    /// Accessing additional fields:
    /// <code>
    /// if (exif.Raw.TryGetValue("Rating", out var rating))
    ///     Console.WriteLine($"Rating: {rating}");
    /// </code>
    /// </example>
    public IReadOnlyDictionary<string, string>? Raw { get; init; }
}
