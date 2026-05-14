namespace Spectara.Revela.Sdk.Hosting;

/// <summary>
/// Immutable build-time identity of the running Revela host.
/// </summary>
/// <remarks>
/// <para>
/// Single source of truth for version data exposed via <c>revela --version</c>,
/// <c>revela info</c>, and any plugin that needs to branch on host kind.
/// </para>
/// <para>
/// <b>Note:</b> <see cref="IBuildInfo"/> describes the <em>build-time</em>
/// identity of the host (Standalone vs. Embedded), determined at compile time
/// via the <c>Revela.HostKind</c> assembly metadata attribute.
/// It is not user-overridable.
/// </para>
/// <para>
/// This is conceptually distinct from
/// <c>Microsoft.Extensions.Hosting.IHostEnvironment</c>, which describes the
/// runtime <em>deployment</em> environment (Development/Staging/Production)
/// and is overridable via <c>DOTNET_ENVIRONMENT</c>. The two are
/// complementary: <c>IHostEnvironment</c> answers "where am I running?",
/// <c>IBuildInfo</c> answers "what build am I?".
/// </para>
/// </remarks>
public interface IBuildInfo
{
    /// <summary>Build variant of the running host.</summary>
    HostKind Kind { get; }

    /// <summary>Clean semantic version, e.g. <c>"1.0.0"</c>.</summary>
    string Version { get; }

    /// <summary>
    /// Full informational version including build metadata,
    /// e.g. <c>"1.0.0+abc1234"</c>. Use this in bug reports.
    /// </summary>
    string InformationalVersion { get; }

    /// <summary>.NET runtime description, e.g. <c>".NET 10.0.4"</c>.</summary>
    string Framework { get; }

    /// <summary>Build configuration: <c>"Debug"</c> or <c>"Release"</c>.</summary>
    string Configuration { get; }

    /// <summary>Runtime identifier, e.g. <c>"linux-x64"</c>.</summary>
    string RuntimeIdentifier { get; }

    /// <summary>
    /// Single-line human-readable summary used by both <c>--version</c>
    /// and the first line of <c>revela info</c>.
    /// </summary>
    /// <example>
    /// <c>revela 1.0.0 (.NET 10.0.4) — embedded build</c>
    /// </example>
    string FormatVersionLine();
}

/// <summary>
/// Build variant of the running Revela host.
/// </summary>
public enum HostKind
{
    /// <summary>
    /// Standard standalone CLI build with dynamic plugin loading
    /// (the <c>revela</c> dotnet tool).
    /// </summary>
    Standalone,

    /// <summary>
    /// Self-contained build with all plugins and themes statically linked.
    /// No plugin management commands are available.
    /// </summary>
    Embedded,
}
