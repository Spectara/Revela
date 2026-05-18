using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Serve.Configuration;

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
/// <item>Project config file (project.json)</item>
/// </list>
/// <para>
/// Example project.json:
/// </para>
/// <code>
/// {
///   "Spectara.Revela.Plugins.Serve": {
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
[RevelaConfig("Spectara.Revela.Plugins.Serve")]
internal sealed class ServePluginConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// Hand-written because the .NET Configuration Binding Source Generator
    /// only intercepts call sites where the section argument is statically
    /// resolvable from user-written source (constants emitted from another
    /// source generator are invisible to it).
    /// </summary>
    public const string Section = "Spectara.Revela.Plugins.Serve";

    /// <summary>
    /// Port number for the HTTP server
    /// </summary>
    /// <remarks>
    /// Default is 8080. If the port is in use, an error will be shown
    /// with a suggestion to use the --port option.
    /// </remarks>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Enable verbose logging (all HTTP requests)
    /// </summary>
    /// <remarks>
    /// When false (default), only 404 errors are shown.
    /// When true, all requests are logged with their status codes.
    /// </remarks>
    public bool Verbose { get; set; }
}

/// <summary>
/// Trim/AOT-safe <see cref="IValidateOptions{TOptions}"/> implementation for
/// <see cref="ServePluginConfig"/>. The body is emitted by the
/// <c>Microsoft.Extensions.Options</c> source generator from the
/// <c>DataAnnotations</c> on the config type.
/// </summary>
[OptionsValidator]
internal sealed partial class ServePluginConfigValidator : IValidateOptions<ServePluginConfig>;
