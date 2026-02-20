using System.Reflection;
using System.Text.Json;

namespace Spectara.Revela.Sdk.Themes;

/// <summary>
/// Helper for working with embedded resources in theme assemblies.
/// </summary>
/// <remarks>
/// Provides consistent resource access for both <see cref="EmbeddedThemePlugin"/>
/// and <see cref="EmbeddedThemeExtension"/>. Handles:
/// <list type="bullet">
/// <item>Resource prefix calculation from assembly name</item>
/// <item>Cross-platform path resolution (forward/backslash variants)</item>
/// <item>Dot-to-path conversion for standard resource naming</item>
/// <item>File extraction to disk</item>
/// <item>Manifest loading from embedded JSON</item>
/// </list>
/// </remarks>
public sealed class EmbeddedResourceProvider
{
    private const string ManifestFileName = "manifest.json";

    private readonly Assembly assembly;
    private readonly string resourcePrefix;

    /// <summary>
    /// Creates a new embedded resource provider for the specified assembly.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded resources.</param>
    public EmbeddedResourceProvider(Assembly assembly)
    {
        this.assembly = assembly;
        resourcePrefix = assembly.GetName().Name + ".";
    }

    /// <summary>
    /// Gets the assembly name for error messages.
    /// </summary>
    public string AssemblyName => assembly.GetName().Name ?? "Unknown";

    /// <summary>
    /// Gets a file from embedded resources as a stream.
    /// </summary>
    /// <remarks>
    /// Tries multiple path variants to handle cross-platform builds:
    /// LogicalName preserves original separators (backslash on Windows),
    /// so we try both forward-slash and backslash variants with and without prefix.
    /// </remarks>
    /// <param name="relativePath">Relative path within the assembly resources.</param>
    /// <returns>Stream with file contents, or null if not found.</returns>
    public Stream? GetFile(string relativePath)
    {
        var forwardSlash = relativePath.Replace('\\', '/');
        var backSlash = relativePath.Replace('/', '\\');

        return assembly.GetManifestResourceStream(resourcePrefix + forwardSlash)
            ?? assembly.GetManifestResourceStream(forwardSlash)
            ?? assembly.GetManifestResourceStream(resourcePrefix + backSlash)
            ?? assembly.GetManifestResourceStream(backSlash);
    }

    /// <summary>
    /// Gets all file paths in the assembly as relative paths.
    /// </summary>
    /// <remarks>
    /// Handles both standard resource naming (dots as separators) and
    /// LogicalName resources (path separators preserved). Filters out .cs files.
    /// </remarks>
    /// <returns>Enumerable of relative file paths.</returns>
    public IEnumerable<string> GetAllFiles()
    {
        return assembly.GetManifestResourceNames()
            .Where(name => !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                // Strip prefix if present
                var relativeName = name.StartsWith(resourcePrefix, StringComparison.Ordinal)
                    ? name[resourcePrefix.Length..]
                    : name;

                // Check if name uses path separators (LogicalName with RecursiveDir)
                if (relativeName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || relativeName.Contains('/', StringComparison.Ordinal))
                {
                    // Already a path, just normalize separators
                    return relativeName.Replace('/', Path.DirectorySeparatorChar);
                }

                // Standard resource naming with dots — convert to path
                return ConvertResourceNameToPath(relativeName);
            });
    }

    /// <summary>
    /// Extracts all embedded resource files to a directory.
    /// </summary>
    /// <param name="targetDirectory">Directory to extract files to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default)
    {
        foreach (var relativePath in GetAllFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = Path.Combine(targetDirectory, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await using var source = GetFile(relativePath);
            if (source is null)
            {
                continue;
            }

            await using var target = File.Create(targetPath);
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    /// <summary>
    /// Loads and deserializes the manifest.json from embedded resources.
    /// </summary>
    /// <typeparam name="T">Type to deserialize into.</typeparam>
    /// <returns>Deserialized manifest object.</returns>
    /// <exception cref="InvalidOperationException">If manifest.json is missing or invalid.</exception>
    public T LoadManifest<T>() where T : class
    {
        using var stream = GetFile(ManifestFileName)
            ?? throw new InvalidOperationException(
                $"{AssemblyName} is missing embedded resource '{ManifestFileName}'");

        var result = JsonSerializer.Deserialize<T>(stream, ThemeJsonConfig.JsonOptions);

        return result ?? throw new InvalidOperationException(
            $"Failed to parse {ManifestFileName} in {AssemblyName}");
    }

    /// <summary>
    /// Converts a resource name suffix to a file path.
    /// </summary>
    /// <remarks>
    /// Standard .NET embedded resources use dots as separators:
    /// "Assets.fonts.inter.woff2" → "Assets\fonts\inter.woff2"
    /// Last dot is preserved as the file extension.
    /// </remarks>
    private static string ConvertResourceNameToPath(string resourceSuffix)
    {
        var lastDot = resourceSuffix.LastIndexOf('.');
        if (lastDot < 0)
        {
            return resourceSuffix;
        }

        var pathPart = resourceSuffix[..lastDot].Replace('.', Path.DirectorySeparatorChar);
        var extension = resourceSuffix[lastDot..];

        return pathPart + extension;
    }
}
