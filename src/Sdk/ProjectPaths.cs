namespace Spectara.Revela.Sdk;

/// <summary>
/// Well-known directory names used in Revela projects.
/// </summary>
/// <remarks>
/// <para>
/// These constants define the standard folder structure for Revela projects.
/// All paths are relative to the project root directory; combine them with
/// <see cref="ProjectEnvironment.Path"/>.
/// </para>
/// <para>
/// Revela core and every package keep their files in their own owner folder
/// (<see cref="GetOwnerDirectory"/>), never in the output: only files of the published site
/// go to <see cref="Services.IPathResolver.OutputPath"/>. How long a file lives is declared per
/// artifact (<see cref="Artifacts.ArtifactKind"/>), not by the folder it is in.
/// </para>
/// </remarks>
public static class ProjectPaths
{
    private const int MaxOwnerLength = 64;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.Ordinal)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };

    /// <summary>
    /// Revela's own folder in the project (<c>.revela</c>). Holds one folder per owner;
    /// nothing in it is ever published.
    /// </summary>
    public const string Revela = ".revela";

    /// <summary>
    /// Gets an owner's folder, <c>.revela/&lt;owner&gt;</c>, relative to the project root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The owner is the owner part of the owner's artifact identifiers
    /// (<see cref="Artifacts.ArtifactId.Owner"/>), for example <c>core</c> or <c>statistics</c>:
    /// <c>Path.Combine(project.Path, ProjectPaths.GetOwnerDirectory(MyArtifacts.Data.Owner))</c>.
    /// </para>
    /// <para>
    /// Every file in the folder must belong to one of the owner's registered artifacts, so the
    /// clean commands can remove it according to the artifact's kind. The folder may hold
    /// artifacts of different kinds.
    /// </para>
    /// </remarks>
    /// <param name="owner">A valid owner name (<see cref="IsValidOwner"/>).</param>
    /// <exception cref="ArgumentException">The owner name is not valid.</exception>
    public static string GetOwnerDirectory(string owner)
    {
        if (!IsValidOwner(owner))
        {
            throw new ArgumentException(
                $"'{owner}' is not a valid owner name: use lowercase letters and digits, separated by single '.' or '-'.",
                nameof(owner));
        }

        return Path.Combine(Revela, owner);
    }

    /// <summary>
    /// Whether a name can be used as an owner (artifact owner and folder name).
    /// </summary>
    /// <remarks>
    /// Lowercase ASCII letters and digits, starting with a letter, optionally separated by single
    /// <c>.</c> or <c>-</c> (for example <c>statistics</c>, <c>acme.captions</c>), at most 64
    /// characters and not a reserved Windows device name. These names are the same folder on
    /// every file system and cannot point outside <c>.revela</c>.
    /// </remarks>
    public static bool IsValidOwner(string? owner)
    {
        if (string.IsNullOrEmpty(owner) || owner.Length > MaxOwnerLength || owner[0] is < 'a' or > 'z')
        {
            return false;
        }

        var previousWasSeparator = false;
        foreach (var character in owner)
        {
            var isSeparator = character is '.' or '-';
            if (isSeparator ? previousWasSeparator : character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }

            previousWasSeparator = isSeparator;
        }

        return !previousWasSeparator && !ReservedDeviceNames.Contains(owner.Split('.')[0]);
    }

    /// <summary>
    /// Themes directory for local/extracted themes.
    /// </summary>
    public const string Themes = "themes";

    /// <summary>
    /// Plugins configuration directory.
    /// </summary>
    public const string Plugins = "plugins";

    /// <summary>
    /// Shared images directory for images available to filter galleries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Images in <c>source/_images/</c> (including subdirectories) are scanned
    /// and available for filter expressions, but do not create their own galleries.
    /// </para>
    /// <para>
    /// Use this for images that should be accessible via filters but not displayed
    /// in a dedicated gallery (e.g., curated collections, featured images).
    /// </para>
    /// </remarks>
    public const string SharedImages = "_images";

    /// <summary>
    /// Static files directory for files copied directly to output root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Files in <c>source/_static/</c> are copied 1:1 to the output root.
    /// Use this for favicon files, robots.txt, CNAME, or other static assets.
    /// </para>
    /// <para>
    /// Example: <c>source/_static/favicon/favicon.ico</c> → <c>output/favicon/favicon.ico</c>
    /// </para>
    /// </remarks>
    public const string Static = "_static";
}
