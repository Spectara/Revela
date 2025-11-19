namespace Spectara.Revela.Core.Models;

/// <summary>
/// EXIF metadata extracted from an image
/// </summary>
public sealed class ExifData
{
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public string? Lens { get; init; }
    public string? Aperture { get; init; }
    public string? ShutterSpeed { get; init; }
    public int? ISO { get; init; }
    public string? FocalLength { get; init; }
    public DateTime? DateTaken { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

