namespace Spectara.Revela.Core.Abstractions;

/// <summary>
/// Menu-ordering constants for the host's built-in <c>check</c> sub-commands.
/// </summary>
/// <remarks>
/// The host assigns these by check name to order the <c>check &lt;name&gt;</c> entries
/// (and their <c>●</c> markers) in the interactive menu. They are display order only —
/// checks are not generate pipeline steps. Plugin-contributed checks cannot choose a
/// position; they are always listed at <see cref="Plugin"/>, after the built-in checks.
/// </remarks>
public static class CheckPipelineOrder
{
    /// <summary>Configuration check (100).</summary>
    public const int Config = 100;

    /// <summary>Project-structure check (200).</summary>
    public const int Structure = 200;

    /// <summary>Theme check (300).</summary>
    public const int Theme = 300;

    /// <summary>Content &amp; metadata check (400).</summary>
    public const int Content = 400;

    /// <summary>Gallery-URL (slug) check (500).</summary>
    public const int Slugs = 500;

    /// <summary>Fallback order for plugin-contributed checks (900).</summary>
    public const int Plugin = 900;
}
