using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;

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
    /// Note: site.json is NOT loaded via IConfiguration — it is loaded dynamically by RenderService.
    /// </para>
    /// <para>
    /// This allows global defaults (themes, plugins, feeds) to be overridden per-project.
    /// Similar to NuGet.Config hierarchical loading.
    /// </para>
    /// <para>
    /// The project directory is determined by ContentRootPath, which is set in Program.cs
    /// based on standalone mode detection and --project argument parsing.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder AddRevelaConfiguration(this HostApplicationBuilder builder)
    {
        // Use ContentRootPath instead of GetCurrentDirectory()
        // This allows standalone mode to set the project path before host build
        var projectDirectory = builder.Environment.ContentRootPath;

        // 1. Load revela.json (global config - user-wide defaults)
        // This provides default themes, plugins, feeds that apply to all projects
        builder.Configuration.AddJsonFile(
            ConfigPathResolver.ConfigFilePath,
            optional: true,
            reloadOnChange: true
        );

        // 2. Load project.json (local config - overrides global)
        // Project-specific settings override global defaults
        builder.Configuration.AddJsonFile(
            Path.Combine(projectDirectory, "project.json"),
            optional: true,
            reloadOnChange: true
        );

        // Note: site.json is NOT loaded via IConfiguration.
        // It's loaded dynamically by RenderService to support theme-specific schemas.

        // 3. Load logging.json (logging config - can override global logging settings)
        builder.Configuration.AddJsonFile(
            Path.Combine(projectDirectory, "logging.json"),
            optional: true,
            reloadOnChange: true
        );

        // Apply logging configuration with sensible defaults
        // Defaults are Warning to keep console clean (Spectre.Console progress bars)
        // Users can override via logging.json for debugging
        var loggingConfig = new LoggingConfig();
        builder.Configuration.GetSection(LoggingConfig.SectionName).Bind(loggingConfig);

        foreach (var (category, level) in loggingConfig.LogLevel)
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
