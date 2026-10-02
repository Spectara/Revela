using System.Reflection;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Reads a package version from its compiled assembly.
/// </summary>
/// <remarks>
/// Use it for <see cref="PackageMetadata.Version"/> so the version shown by
/// <c>revela info plugins</c> always matches the built package:
/// <code>
/// public PackageMetadata Metadata { get; } = new()
/// {
///     Id = "MyCompany.Revela.Plugins.Hello",
///     Name = "Hello",
///     Version = PackageVersion.FromAssembly(typeof(HelloPlugin).Assembly),
///     Description = "Says hello"
/// };
/// </code>
/// </remarks>
public static class PackageVersion
{
    /// <summary>
    /// Gets the semantic version of <paramref name="assembly"/> from its
    /// <see cref="AssemblyInformationalVersionAttribute"/>, without build metadata
    /// (e.g. <c>"0.0.1-beta.21"</c> for <c>"0.0.1-beta.21+abc1234"</c>).
    /// Falls back to the three-part assembly version when the attribute is missing.
    /// </summary>
    /// <param name="assembly">The package assembly.</param>
    /// <returns>The package version.</returns>
    public static string FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? informational : informational[..plus];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
