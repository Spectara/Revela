using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Cli;

/// <summary>
/// Extension methods for configuring the Revela host.
/// </summary>
internal static class HostBuilderExtensions
{
    /// <summary>
    /// Adds Revela project configuration files from the working directory.
    /// </summary>
    /// <remarks>
    /// Loads the following JSON files (all optional):
    /// - project.json: Project settings (name, url, theme, generate.cameras, etc.)
    /// - site.json: Site metadata (title, author, description, copyright)
    /// - logging.json: Logging configuration (log levels per category)
    ///
    /// Default log level is <c>Warning</c> to keep console output clean.
    /// Override via <c>logging.json</c> or environment variables for debugging.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static HostApplicationBuilder AddRevelaConfiguration(this HostApplicationBuilder builder)
    {
        var workingDirectory = Directory.GetCurrentDirectory();

        // Load project.json (project-specific config)
        builder.Configuration.AddJsonFile(
            Path.Combine(workingDirectory, "project.json"),
            optional: true,
            reloadOnChange: false
        );

        // Load site.json (site metadata)
        builder.Configuration.AddJsonFile(
            Path.Combine(workingDirectory, "site.json"),
            optional: true,
            reloadOnChange: false
        );

        // Load logging.json (optional logging config with hot-reload)
        builder.Configuration.AddJsonFile(
            Path.Combine(workingDirectory, "logging.json"),
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
            if (Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(level, ignoreCase: true, out var logLevel))
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
