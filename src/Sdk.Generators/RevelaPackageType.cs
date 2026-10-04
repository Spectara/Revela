using Microsoft.CodeAnalysis.Diagnostics;

namespace Spectara.Revela.Sdk.Generators;

/// <summary>
/// Detects Revela plugin and theme assemblies from the MSBuild <c>PackageType</c>, which
/// <c>Spectara.Revela.Sdk.targets</c> exposes to the compiler as a <c>CompilerVisibleProperty</c>.
/// </summary>
internal static class RevelaPackageType
{
    public static bool IsPluginOrTheme(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue("build_property.PackageType", out var value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // NuGet PackageType is a ';'-separated list whose entries may carry a version ("Name, 1.0").
        foreach (var entry in value.Split(';'))
        {
            var name = entry.Split(',')[0].Trim();
            if (string.Equals(name, "RevelaPlugin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "RevelaTheme", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
