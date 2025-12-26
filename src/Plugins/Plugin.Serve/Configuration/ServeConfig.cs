using System.ComponentModel.DataAnnotations;

namespace Spectara.Revela.Plugin.Serve.Configuration;

/// <summary>
/// Serve plugin configuration
/// </summary>
/// <remarks>
/// <para>
/// Default values are defined in the property initializers.
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// </para>
/// <list type="number">
/// <item>Command-line arguments (--port, --verbose)</item>
/// <item>Environment variables (SPECTARA__REVELA__PLUGIN__SERVE__*)</item>
/// <item>User config file (config/Spectara.Revela.Plugin.Serve.json)</item>
/// </list>
/// <para>
/// Example config/Spectara.Revela.Plugin.Serve.json:
/// </para>
/// <code>
/// {
///   "Spectara.Revela.Plugin.Serve": {
///     "Port": 3000,
///     "Verbose": true
///   }
/// }
/// </code>
/// <para>
/// Example Environment Variables:
/// </para>
/// <code>
/// SPECTARA__REVELA__PLUGIN__SERVE__PORT=3000
/// SPECTARA__REVELA__PLUGIN__SERVE__VERBOSE=true
/// </code>
/// </remarks>
public sealed class ServeConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Spectara.Revela.Plugin.Serve";

    /// <summary>
    /// Port number for the HTTP server
    /// </summary>
    /// <remarks>
    /// Default is 8080. If the port is in use, an error will be shown
    /// with a suggestion to use the --port option.
    /// </remarks>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; init; } = 8080;

    /// <summary>
    /// Enable verbose logging (all HTTP requests)
    /// </summary>
    /// <remarks>
    /// When false (default), only 404 errors are shown.
    /// When true, all requests are logged with their status codes.
    /// </remarks>
    public bool Verbose { get; init; }
}
