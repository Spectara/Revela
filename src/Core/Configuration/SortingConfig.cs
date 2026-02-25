namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Sorting configuration for galleries and images
/// </summary>
/// <remarks>
/// <para>
/// Controls the sort order of galleries in navigation and images within galleries.
/// Images can be sorted by any property path including EXIF data.
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "generate": {
///     "sorting": {
///       "galleries": "desc",
///       "images": {
///         "field": "dateTaken",
///         "direction": "desc",
///         "fallback": "filename"
///       }
///     }
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class SortingConfig
{
    /// <summary>
    /// Gallery sort direction (always sorted by name with natural ordering).
    /// </summary>
    /// <remarks>
    /// Galleries are always sorted by folder name using natural ordering
    /// (1, 2, 10 instead of 1, 10, 2). Sort prefixes like "01 Events" are
    /// stripped for display but used for ordering.
    /// </remarks>
    public SortDirection Galleries { get; init; } = SortDirection.Asc;

    /// <summary>
    /// Image sort configuration within galleries.
    /// </summary>
    /// <remarks>
    /// Can be overridden per gallery using front matter:
    /// <code>
    /// +++
    /// sort = "exif.rating"
    /// sort_direction = "desc"
    /// +++
    /// </code>
    /// </remarks>
    public ImageSortConfig Images { get; init; } = new();
}

/// <summary>
/// Sort direction
/// </summary>
/// <remarks>
/// JSON values: "asc" or "desc" (case-insensitive).
/// </remarks>
public enum SortDirection
{
    /// <summary>A → Z, 1 → 9, oldest → newest</summary>
    Asc,

    /// <summary>Z → A, 9 → 1, newest → oldest</summary>
    Desc
}

/// <summary>
/// Image sort configuration
/// </summary>
/// <remarks>
/// <para>
/// Allows flexible sorting by any property path. Supports:
/// </para>
/// <list type="bullet">
///   <item><c>filename</c> - File name</item>
///   <item><c>dateTaken</c> - EXIF date taken</item>
///   <item><c>exif.focalLength</c> - Focal length</item>
///   <item><c>exif.iso</c> - ISO sensitivity</item>
///   <item><c>exif.aperture</c> - Aperture (f-number)</item>
///   <item><c>exif.raw.Rating</c> - Star rating (1-5)</item>
///   <item><c>exif.raw.{FieldName}</c> - Any field from EXIF Raw dictionary</item>
/// </list>
/// <para>
/// When the primary field is null/missing, falls back to the fallback field.
/// </para>
/// </remarks>
public sealed class ImageSortConfig
{
    /// <summary>
    /// Property path to sort by.
    /// </summary>
    /// <remarks>
    /// Use dot notation for nested properties: "exif.focalLength", "exif.raw.Rating".
    /// </remarks>
    public string Field { get; init; } = "dateTaken";

    /// <summary>
    /// Sort direction.
    /// </summary>
    public SortDirection Direction { get; init; } = SortDirection.Desc;

    /// <summary>
    /// Fallback field when primary field is null/missing.
    /// </summary>
    /// <remarks>
    /// Uses same direction as primary sort.
    /// </remarks>
    public string Fallback { get; init; } = "filename";
}
