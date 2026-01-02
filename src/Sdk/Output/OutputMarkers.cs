namespace Spectara.Revela.Sdk.Output;

/// <summary>
/// Standardized console output markers for consistent user feedback.
/// Use these constants with Spectre.Console markup in string interpolation.
/// </summary>
/// <example>
/// <code>
/// AnsiConsole.MarkupLine($"{OutputMarkers.Success} Operation completed");
/// AnsiConsole.MarkupLine($"{OutputMarkers.Error} Failed to process file");
/// AnsiConsole.MarkupLine($"{OutputMarkers.Warning} File already exists");
/// AnsiConsole.MarkupLine($"{OutputMarkers.Info} Processing 5 items...");
/// </code>
/// </example>
public static class OutputMarkers
{
    /// <summary>
    /// Success indicator: green checkmark (✓)
    /// Use for: completed actions, successful operations, items that passed validation.
    /// </summary>
    public const string Success = "[green]✓[/]";

    /// <summary>
    /// Error indicator: red X (✗)
    /// Use for: failed operations, errors, items that failed validation.
    /// </summary>
    public const string Error = "[red]✗[/]";

    /// <summary>
    /// Warning indicator: yellow warning sign (⚠)
    /// Use for: skipped items, potential issues, non-critical problems.
    /// </summary>
    public const string Warning = "[yellow]⚠[/]";

    /// <summary>
    /// Info indicator: blue information sign (ℹ)
    /// Use for: informational messages, hints, neutral status updates.
    /// </summary>
    public const string Info = "[blue]ℹ[/]";
}
