using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Default <see cref="IBuildInfo"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Detects <see cref="HostKind"/> by reading the <c>Revela.HostKind</c>
/// <see cref="AssemblyMetadataAttribute"/> from the entry assembly. Default
/// when the attribute is absent is <see cref="HostKind.Standalone"/>.
/// </para>
/// <para>
/// Both Cli and Cli.Embedded produce an executable named <c>revela</c>, so
/// assembly-name-based detection is impossible. The metadata attribute is the
/// single source of truth, set in <c>Cli.Embedded.csproj</c> via:
/// </para>
/// <code>
/// &lt;ItemGroup&gt;
///   &lt;AssemblyMetadata Include="Revela.HostKind" Value="Embedded" /&gt;
/// &lt;/ItemGroup&gt;
/// </code>
/// </remarks>
internal sealed class BuildInfo : IBuildInfo
{
    private const string HostKindMetadataKey = "Revela.HostKind";

    private const string BuildConfiguration =
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    public BuildInfo()
        : this(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
    {
    }

    /// <summary>
    /// Test seam: construct from a specific assembly, bypassing entry-assembly
    /// auto-detection. Used by BuildInfoTests.
    /// </summary>
    internal BuildInfo(Assembly entry)
    {
        Kind = DetectHostKind(entry);
        InformationalVersion = DetectInformationalVersion(entry);
        Version = StripBuildMetadata(InformationalVersion);
        Framework = RuntimeInformation.FrameworkDescription;
        Configuration = BuildConfiguration;
        RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;
    }

    /// <summary>
    /// Test seam: construct directly from raw values, fully bypassing assembly
    /// inspection. Used by BuildInfoTests for FormatVersionLine assertions
    /// across both <see cref="HostKind"/> values.
    /// </summary>
    internal BuildInfo(
        HostKind kind,
        string version,
        string informationalVersion,
        string framework,
        string configuration,
        string runtimeIdentifier)
    {
        Kind = kind;
        Version = version;
        InformationalVersion = informationalVersion;
        Framework = framework;
        Configuration = configuration;
        RuntimeIdentifier = runtimeIdentifier;
    }

    public HostKind Kind { get; }

    public string Version { get; }

    public string InformationalVersion { get; }

    public string Framework { get; }

    public string Configuration { get; }

    public string RuntimeIdentifier { get; }

    public string FormatVersionLine()
    {
        var suffix = Kind switch
        {
            HostKind.Embedded => " \u2014 embedded build",
            HostKind.Standalone => string.Empty,
            _ => string.Empty,
        };
        return string.Create(
            CultureInfo.InvariantCulture,
            $"revela {Version} ({Framework}){suffix}");
    }

    private static HostKind DetectHostKind(Assembly entry)
    {
        var value = entry
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, HostKindMetadataKey, StringComparison.Ordinal))
            ?.Value;

        return string.Equals(value, nameof(HostKind.Embedded), StringComparison.Ordinal)
            ? HostKind.Embedded
            : HostKind.Standalone;
    }

    /// <summary>Test seam — exposes <see cref="DetectHostKind"/>.</summary>
    internal static HostKind DetectHostKindForTesting(Assembly entry) => DetectHostKind(entry);

    private static string DetectInformationalVersion(Assembly entry)
    {
        var info = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            return info;
        }

        var version = entry.GetName().Version;
        return version is null
            ? "0.0.0"
            : version.ToString(3);
    }

    private static string StripBuildMetadata(string informational)
    {
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }

    /// <summary>Test seam — exposes <see cref="StripBuildMetadata"/>.</summary>
    internal static string StripBuildMetadataForTesting(string informational) => StripBuildMetadata(informational);
}
