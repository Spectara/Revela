namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Logging configuration with sensible defaults
/// </summary>
/// <remarks>
/// Default values are defined in property initializers.
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Environment variables (REVELA__LOGGING__LOGLEVEL__*)
/// 2. User config file (logging.json) - optional, in working directory
/// 
/// Example logging.json (optional, create in your project directory):
/// {
///   "Logging": {
///     "LogLevel": {
///       "Default": "Debug",
///       "Spectara.Revela": "Trace",
///       "Microsoft.Extensions.Http": "Information"
///     }
///   }
/// }
/// 
/// Example Environment Variables:
/// REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug
/// REVELA__LOGGING__LOGLEVEL__SPECTARA_REVELA=Trace
/// </remarks>
public sealed class LoggingConfig
{
    /// <summary>
    /// Configuration section name in config files
    /// </summary>
    public const string SectionName = "Logging";

    /// <summary>
    /// Default log levels per namespace
    /// </summary>
    /// <remarks>
    /// Key is the namespace/category, value is the log level.
    /// Valid log levels: Trace, Debug, Information, Warning, Error, Critical, None
    /// </remarks>
    public Dictionary<string, string> LogLevel { get; init; } = new()
    {
        ["Default"] = "Information",
        ["Spectara.Revela"] = "Debug",
        ["Microsoft"] = "Warning",
        ["System"] = "Warning"
    };
}
