using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Logging configuration with sensible defaults for CLI tools
/// </summary>
/// <remarks>
/// <para>
/// Default log level is <c>Warning</c> to keep console output clean.
/// This prevents INFO messages from interfering with Spectre.Console progress bars and spinners.
/// </para>
/// <para>
/// Configuration sources (in priority order, highest wins):
/// </para>
/// <list type="number">
/// <item>Environment variables: <c>REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug</c></item>
/// <item>User config file: <c>logging.json</c> in working directory</item>
/// <item>C# defaults in this class (Warning for all categories)</item>
/// </list>
/// <para>
/// To enable verbose output for debugging, create <c>logging.json</c>:
/// </para>
/// <code>
/// {
///   "Logging": {
///     "LogLevel": {
///       "Default": "Information",
///       "Spectara.Revela": "Debug"
///     }
///   }
/// }
/// </code>
/// </remarks>
[RevelaConfig("Logging", ValidateDataAnnotations = false)]
public sealed class LoggingConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "Logging";
    /// <summary>
    /// Log levels per category
    /// </summary>
    /// <remarks>
    /// Keys are category names (namespace prefixes), values are log level names.
    /// "Default" is the fallback for categories not explicitly configured.
    /// </remarks>
    public Dictionary<string, string> LogLevel { get; } = new()
    {
        ["Default"] = "Warning",
        ["Spectara.Revela"] = "Warning",
        ["Microsoft"] = "Warning",
        ["System"] = "Warning"
    };
}
