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
    /// Adds Revela configuration files with proper layering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration sources are loaded in order (later sources override earlier):
    /// </para>
    /// <list type="number">
    /// <item><b>revela.json</b> (global): User-wide defaults from %APPDATA%/Revela/</item>
    /// <item><b>project.json</b> (local): Project-specific settings</item>
    /// <item><b>logging.json</b> (local): Logging configuration</item>
    /// </list>
    /// <para>
    /// Note: site.json's identity core is loaded via <c>AddSiteJson</c> (re-keyed under
    /// the "site" section); its theme-specific tail is still read dynamically by RenderService.
    /// </para>
    /// <para>
    /// This allows global defaults (themes, plugins, feeds) to be overridden per-project.
    /// Similar to NuGet.Config hierarchical loading.
    /// </para>
    /// <para>
    /// The project directory is the host's ContentRootPath, which <c>HostBootstrap</c>
    /// sets to the current working directory (tests may pass an explicit path).
    /// </para>
    /// <para>
    /// No source watches for changes: a reload-on-change file source watches its whole
    /// directory recursively, which on Linux costs one inotify watch per sub-directory of
    /// the project (photos, output, cache) and stalled startup on large or network-mounted
    /// projects. A CLI run is short-lived; code that writes a config file in-process reloads
    /// <see cref="IConfigurationRoot"/> explicitly afterwards.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder AddRevelaConfiguration(this HostApplicationBuilder builder)
    {
        // Use ContentRootPath instead of GetCurrentDirectory() so callers (e.g. tests)
        // can supply the project directory explicitly before the host is built
        var projectDirectory = builder.Environment.ContentRootPath;

        // 1. Load revela.json (global config - user-wide defaults)
        // This provides default themes, plugins, feeds that apply to all projects
        builder.Configuration.AddJsonFile(
            ConfigPathResolver.ConfigFilePath,
            optional: true,
            reloadOnChange: false
        );

        // 2. Load project.json (local config - overrides global)
        // Project-specific settings override global defaults
        builder.Configuration.AddJsonFile(
            Path.Combine(projectDirectory, "project.json"),
            optional: true,
            reloadOnChange: false
        );

        // Note: site.json is loaded via a dedicated source (AddSiteJson) that re-keys
        // its identity core under the "site" section for SiteCoreConfig. Its
        // theme-specific tail is still read dynamically by RenderService.
        builder.Configuration.AddSiteJson(
            Path.Combine(projectDirectory, "site.json"),
            optional: true,
            reloadOnChange: false
        );

        // 3. Load logging.json (logging config - can override global logging settings)
        builder.Configuration.AddJsonFile(
            Path.Combine(projectDirectory, "logging.json"),
            optional: true,
            reloadOnChange: false
        );

        // Apply logging configuration with sensible defaults
        // Defaults are Warning to keep console clean (Spectre.Console progress bars)
        // Users can override via logging.json for debugging
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
