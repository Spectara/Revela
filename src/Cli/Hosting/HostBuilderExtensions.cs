using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Extension methods for configuring the Revela host.
/// </summary>
internal static class HostBuilderExtensions
{
    /// <summary>
    /// Prefix of environment variables that override configuration
    /// (<c>SPECTARA__REVELA__PROJECT__NAME</c> → <c>project:name</c>).
    /// </summary>
    private const string EnvironmentVariablePrefix = "SPECTARA__REVELA__";

    /// <summary>
    /// Adds the Revela configuration layers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host is created without defaults (see <c>HostBootstrap.CreateBuilder</c>), so these
    /// are all configuration sources. Later sources override earlier ones:
    /// </para>
    /// <list type="number">
    /// <item>C# property defaults of the bound configuration classes</item>
    /// <item><b>revela.json</b> (global): user-wide settings, feeds and packages</item>
    /// <item><b>project.json</b> (local): project settings</item>
    /// <item><b>site.json</b> (local): re-keyed under the <c>site</c> section via <c>AddSiteJson</c>;
    /// its theme-specific tail is still read dynamically by RenderService</item>
    /// <item><b>logging.json</b> (local): logging configuration</item>
    /// <item><b>Environment variables</b> with the <c>SPECTARA__REVELA__</c> prefix</item>
    /// </list>
    /// <para>
    /// Plugins may append further sources in <c>IPlugin.ConfigureConfiguration</c>.
    /// </para>
    /// <para>
    /// The project directory is the host's ContentRootPath, which <c>HostBootstrap</c>
    /// sets to the current working directory (tests may pass an explicit path).
    /// </para>
    /// <para>
    /// No source watches for changes: a reload-on-change file source watches its whole
    /// directory recursively, which on Linux costs one inotify watch per sub-directory of
    /// the project (photos, output, cache) and stalled startup on large or network-mounted
    /// projects. A CLI run is short-lived; code that writes a config file in-process uses
    /// <see cref="ConfigFileWriter"/>, which reloads <see cref="IConfigurationRoot"/> afterwards.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder AddRevelaConfiguration(this HostApplicationBuilder builder)
    {
        // Use ContentRootPath instead of GetCurrentDirectory() so callers (e.g. tests)
        // can supply the project directory explicitly before the host is built
        var projectDirectory = builder.Environment.ContentRootPath;

        builder.Configuration.AddJsonFile(
            ConfigPathResolver.ConfigFilePath,
            optional: true,
            reloadOnChange: false);

        builder.Configuration.AddJsonFile(
            Path.Combine(projectDirectory, "project.json"),
            optional: true,
            reloadOnChange: false);

        builder.Configuration.AddSiteJson(
            Path.Combine(projectDirectory, "site.json"),
            optional: true);

        builder.Configuration.AddJsonFile(
            Path.Combine(projectDirectory, "logging.json"),
            optional: true,
            reloadOnChange: false);

        builder.Configuration.AddEnvironmentVariables(EnvironmentVariablePrefix);

        return builder;
    }

    /// <summary>
    /// Adds the console and debug logging providers and applies the configured log levels.
    /// </summary>
    /// <remarks>
    /// Must run after <see cref="AddRevelaConfiguration"/>: the levels are read from the
    /// complete configuration, so <c>logging.json</c> and <c>SPECTARA__REVELA__LOGGING__*</c>
    /// override the built-in <c>Warning</c> defaults of <see cref="LoggingConfig"/>
    /// (kept low so log lines don't disturb Spectre.Console progress output).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder AddRevelaLogging(this HostApplicationBuilder builder)
    {
        // Provider-specific settings (e.g. Logging:Console:FormatterName) still apply
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        // Read IConfiguration directly instead of Bind() to avoid IL2026 trimming warning
        var logLevels = new LoggingConfig().LogLevel;

        foreach (var child in builder.Configuration.GetSection("Logging:LogLevel").GetChildren())
        {
            if (child.Value is not null)
            {
                logLevels[child.Key] = child.Value;
            }
        }

        foreach (var (category, level) in logLevels)
        {
            if (Enum.TryParse<LogLevel>(level, ignoreCase: true, out var logLevel))
            {
                if (category == "Default")
                {
                    builder.Logging.SetMinimumLevel(logLevel);
                }
                else
                {
                    builder.Logging.AddFilter(category, logLevel);
                }
            }
        }

        return builder;
    }
}
