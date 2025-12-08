namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Options for site generation
/// </summary>
public sealed class GenerateOptions
{
    /// <summary>
    /// Clean output directory before generation
    /// </summary>
    public bool Clean { get; init; }
}
