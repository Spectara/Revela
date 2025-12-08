namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Options for site generation
/// </summary>
public sealed class GenerateOptions
{
    /// <summary>
    /// Skip image processing (HTML only mode)
    /// </summary>
    /// <remarks>
    /// Useful for theme development - generates HTML without
    /// time-consuming image processing.
    /// </remarks>
    public bool SkipImages { get; init; }

    /// <summary>
    /// Clean output directory before generation
    /// </summary>
    public bool Clean { get; init; }
}
