using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Repository for manifest persistence operations.
/// </summary>
/// <remarks>
/// <para>
/// Abstracts the storage mechanism for the manifest file,
/// enabling testability and potential future storage backends.
/// The repository holds manifest state in memory and persists on Save.
/// </para>
/// <para>
/// The manifest uses a unified tree structure with a single root node.
/// Image lookups are cached internally for performance.
/// </para>
/// </remarks>
public interface IManifestRepository
{
    #region Root Node

    /// <summary>
    /// Root node of the site tree (home page).
    /// </summary>
    /// <remarks>
    /// The entire site structure is represented as a tree starting from this root.
    /// Galleries with images have a non-null slug, branch nodes have null slug.
    /// </remarks>
    ManifestEntry? Root { get; }

    /// <summary>
    /// Set the root node (replaces entire tree).
    /// </summary>
    /// <param name="root">The root manifest entry</param>
    void SetRoot(ManifestEntry root);

    #endregion

    #region Image Entries

    /// <summary>
    /// All image entries (read-only view, built from tree).
    /// </summary>
    /// <remarks>
    /// This is a cached dictionary built from traversing the tree.
    /// Keys are source paths relative to source directory.
    /// </remarks>
    IReadOnlyDictionary<string, ImageContent> Images { get; }

    /// <summary>
    /// Get image entry by source path.
    /// </summary>
    /// <param name="sourcePath">Relative path to source image (normalized with forward slashes)</param>
    /// <returns>Entry if exists, null otherwise</returns>
    ImageContent? GetImage(string sourcePath);

    /// <summary>
    /// Add or update image entry in the appropriate tree node.
    /// </summary>
    /// <param name="sourcePath">Relative path to source image (normalized with forward slashes)</param>
    /// <param name="entry">Image manifest entry</param>
    void SetImage(string sourcePath, ImageContent entry);

    /// <summary>
    /// Remove image entry.
    /// </summary>
    /// <param name="sourcePath">Relative path to source image</param>
    /// <returns>True if entry was removed, false if not found</returns>
    bool RemoveImage(string sourcePath);

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
