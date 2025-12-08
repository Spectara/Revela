using Spectara.Revela.Commands.Generate.Models.Manifest;

namespace Spectara.Revela.Commands.Generate.Abstractions;

/// <summary>
/// Repository for manifest persistence operations.
/// </summary>
/// <remarks>
/// Abstracts the storage mechanism for the manifest file,
/// enabling testability and potential future storage backends.
/// The repository holds manifest state in memory and persists on Save.
/// </remarks>
public interface IManifestRepository
{
    #region Image Entries

    /// <summary>
    /// All image entries (read-only view).
    /// </summary>
    IReadOnlyDictionary<string, ImageManifestEntry> Images { get; }

    /// <summary>
    /// Get image entry by source path.
    /// </summary>
    /// <param name="sourcePath">Relative path to source image (normalized with forward slashes)</param>
    /// <returns>Entry if exists, null otherwise</returns>
    ImageManifestEntry? GetImage(string sourcePath);

    /// <summary>
    /// Add or update image entry.
    /// </summary>
    /// <param name="sourcePath">Relative path to source image (normalized with forward slashes)</param>
    /// <param name="entry">Image manifest entry</param>
    void SetImage(string sourcePath, ImageManifestEntry entry);

    /// <summary>
    /// Remove image entry.
    /// </summary>
    /// <param name="sourcePath">Relative path to source image</param>
    /// <returns>True if entry was removed, false if not found</returns>
    bool RemoveImage(string sourcePath);

    #endregion

    #region Gallery Entries

    /// <summary>
    /// All gallery entries from content scan.
    /// </summary>
    IReadOnlyList<GalleryManifestEntry> Galleries { get; }

    /// <summary>
    /// Set all gallery entries (replaces existing).
    /// </summary>
    void SetGalleries(IEnumerable<GalleryManifestEntry> galleries);

    #endregion

    #region Navigation Entries

    /// <summary>
    /// Navigation tree from content scan.
    /// </summary>
    IReadOnlyList<NavigationManifestEntry> Navigation { get; }

    /// <summary>
    /// Set navigation tree (replaces existing).
    /// </summary>
    void SetNavigation(IEnumerable<NavigationManifestEntry> navigation);

    #endregion

    #region Metadata

    /// <summary>
    /// Hash of image processing configuration.
    /// When this changes, all images need regeneration.
    /// </summary>
    string ConfigHash { get; set; }

    /// <summary>
    /// Timestamp of last content scan.
    /// </summary>
    DateTime? LastScanned { get; set; }

    /// <summary>
    /// Timestamp of last image processing.
    /// </summary>
    DateTime? LastImagesProcessed { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Load manifest from storage (lazy-loaded on first access if not called).
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save manifest to storage.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all entries and reset to empty manifest.
    /// </summary>
    void Clear();

    #endregion

    #region Utilities

    /// <summary>
    /// Remove orphaned entries (source files that no longer exist).
    /// </summary>
    /// <param name="existingSourcePaths">Set of source paths that currently exist</param>
    /// <returns>List of removed entry keys</returns>
    IReadOnlyList<string> RemoveOrphans(IReadOnlySet<string> existingSourcePaths);

    #endregion
}
