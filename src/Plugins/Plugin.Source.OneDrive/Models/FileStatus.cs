namespace Spectara.Revela.Plugin.Source.OneDrive.Models;

/// <summary>
/// Status of a file compared to remote OneDrive version
/// </summary>
internal enum FileStatus
{
    /// <summary>
    /// File exists only on OneDrive (will be downloaded)
    /// </summary>
    New,

    /// <summary>
    /// File exists locally but differs from OneDrive (size or last modified)
    /// </summary>
    Modified,

    /// <summary>
    /// File is identical locally and on OneDrive
    /// </summary>
    Unchanged,

    /// <summary>
    /// File exists locally but not on OneDrive anymore
    /// </summary>
    Orphaned
}
