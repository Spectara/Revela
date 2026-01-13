using System.CommandLine;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Paths;

/// <summary>
/// Command to configure source and output directory paths.
/// </summary>
/// <remarks>
/// <para>
/// Allows users to configure custom locations for source images
/// and generated output. Useful for:
/// </para>
/// <list type="bullet">
/// <item>OneDrive/Dropbox: Source images in cloud-synced folder</item>
/// <item>Direct deployment: Output directly to webserver directory</item>
/// </list>
/// </remarks>
public sealed partial class ConfigPathsCommand(
    ILogger<ConfigPathsCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<PathsConfig> pathsConfig,
    IPathResolver pathResolver,
    IConfigService configService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("paths", "Configure source and output directory paths");

        var sourceOption = new Option<string?>("--source", "-s")
        {
            Description = "Source directory for images (relative or absolute path)"
        };
        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output directory for generated site (relative or absolute path)"
        };

        command.Options.Add(sourceOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceOption);
            var output = parseResult.GetValue(outputOption);

            return await ExecuteAsync(source, output, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the paths configuration in interactive mode.
    /// </summary>
    /// <remarks>
    /// Used by the config interactive menu.
    /// </remarks>
    public Task<int> ExecuteInteractiveAsync(CancellationToken cancellationToken)
        => ExecuteAsync(null, null, cancellationToken);

    /// <summary>
    /// Executes the paths configuration.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? sourceArg,
        string? outputArg,
        CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        // Get current config
        var current = pathsConfig.CurrentValue;

        // Determine if interactive mode (no arguments provided)
        var isInteractive = sourceArg is null && outputArg is null;

        string source, output;

        if (isInteractive)
        {
            AnsiConsole.MarkupLine("[cyan]Configure source and output paths[/]\n");

            // Show current values
            AnsiConsole.MarkupLine($"[dim]Current source:[/] {current.Source}");
            AnsiConsole.MarkupLine($"[dim]  → Resolves to:[/] {pathResolver.SourcePath}");
            AnsiConsole.MarkupLine($"[dim]Current output:[/] {current.Output}");
            AnsiConsole.MarkupLine($"[dim]  → Resolves to:[/] {pathResolver.OutputPath}");
            AnsiConsole.WriteLine();

            // Prompt for source path
            source = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Source directory[/]:")
                    .DefaultValue(current.Source)
                    .AllowEmpty());

            // Prompt for output path
            output = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Output directory[/]:")
                    .DefaultValue(current.Output)
                    .AllowEmpty());

            // Use defaults if empty
            if (string.IsNullOrWhiteSpace(source))
            {
                source = current.Source;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                output = current.Output;
            }
        }
        else
        {
            // CLI mode: use provided values or keep current
            source = sourceArg ?? current.Source;
            output = outputArg ?? current.Output;
        }

        // Check if anything changed
        var hasChanges = source != current.Source || output != current.Output;
        var defaults = new PathsConfig();

        if (!hasChanges)
        {
            // Create default directories to help new users get started
            EnsureDefaultDirectoriesExist(source, output, defaults);

            AnsiConsole.MarkupLine($"{OutputMarkers.Info} Using default paths");
            AnsiConsole.MarkupLine($"  [dim]Source:[/] {source}");
            AnsiConsole.MarkupLine($"  [dim]Output:[/] {output}");
            return 0;
        }

        // Update config using deep merge
        var updates = new JsonObject
        {
            ["paths"] = new JsonObject
            {
                ["source"] = source,
                ["output"] = output
            }
        };

        await configService.UpdateProjectConfigAsync(updates, cancellationToken).ConfigureAwait(false);

        // Create default directories (only if using defaults, not custom paths)
        EnsureDefaultDirectoriesExist(source, output, defaults);

        LogPathsConfigured(logger, source, output);

        // Show result with resolved paths
        var projectPath = projectEnvironment.Value.Path;
        var resolvedSource = Path.IsPathRooted(source)
            ? Path.GetFullPath(source)
            : Path.GetFullPath(Path.Combine(projectPath, source));
        var resolvedOutput = Path.IsPathRooted(output)
            ? Path.GetFullPath(output)
            : Path.GetFullPath(Path.Combine(projectPath, output));

        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Paths configured");
        AnsiConsole.MarkupLine($"  [dim]Source:[/] {source} → [cyan]{resolvedSource}[/]");
        AnsiConsole.MarkupLine($"  [dim]Output:[/] {output} → [cyan]{resolvedOutput}[/]");

        // Warn if source directory doesn't exist (for custom paths that user provided)
        if (!Directory.Exists(resolvedSource))
        {
            AnsiConsole.MarkupLine($"\n{OutputMarkers.Warning} Source directory does not exist yet: [dim]{resolvedSource}[/]");
        }

        return 0;
    }

    /// <summary>
    /// Creates directories only if they match the default paths.
    /// </summary>
    /// <remarks>
    /// This helps new users get started by showing them where to put images.
    /// Custom paths (like "D:\OneDrive\Photos") are not created - the user
    /// is expected to have set them up already.
    /// </remarks>
    private static void EnsureDefaultDirectoriesExist(string source, string output, PathsConfig defaults)
    {
        if (source == defaults.Source)
        {
            Directory.CreateDirectory(source);
        }

        if (output == defaults.Output)
        {
            Directory.CreateDirectory(output);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Paths configured: source='{Source}', output='{Output}'")]
    private static partial void LogPathsConfigured(ILogger logger, string source, string output);
}
