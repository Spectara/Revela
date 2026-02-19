namespace Spectara.Revela.Plugin.Source.OneDrive.Models;

/// <summary>
/// Represents a file or folder item from OneDrive
/// </summary>
internal sealed class OneDriveItem
{
    /// <summary>
    /// The item ID in OneDrive
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The item name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this item is a folder
    /// </summary>
    public bool IsFolder { get; init; }

    /// <summary>
    /// File size in bytes (0 for folders)
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Download URL for the file
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "OneDrive API returns strings")]
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Parent folder path
    /// </summary>
    public string ParentPath { get; init; } = string.Empty;
}
