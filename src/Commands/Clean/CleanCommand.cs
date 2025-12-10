using System.CommandLine;
using System.Globalization;
using Spectre.Console;

namespace Spectara.Revela.Commands.Clean;

/// <summary>
/// Cleans generated files from the project directory.
/// </summary>
/// <remarks>
/// <para>
/// Removes generated artifacts to enable fresh builds:
/// </para>
/// <list type="bullet">
///   <item><description>output/ - Generated HTML, CSS, and processed images (default)</description></item>
///   <item><description>.cache/ - Manifest and cached data (optional)</description></item>
/// </list>
/// <para>
/// Safety: NEVER deletes source files (source/, plugins/, *.json configs).
/// </para>
/// </remarks>
public sealed partial class CleanCommand(ILogger<CleanCommand> logger)
{
    /// <summary>Output directory for generated site</summary>
    private const string OutputDirectory = "output";

    /// <summary>Cache directory name</summary>
    private const string CacheDirectory = ".cache";

    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("clean", "Clean generated files");

        var cacheOption = new Option<bool>("--cache")
        {
            Description = "Also clean cache directory (.cache)"
        };

        var allOption = new Option<bool>("--all", "-a")
        {
            Description = "Clean both output and cache (full clean)"
        };

        var dryRunOption = new Option<bool>("--dry-run", "-n")
        {
            Description = "Show what would be deleted without deleting"
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Skip confirmation prompt for --all"
        };

        command.Options.Add(cacheOption);
        command.Options.Add(allOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(forceOption);

        command.SetAction(parseResult =>
        {
            var options = new CleanOptions
            {
                CleanCache = parseResult.GetValue(cacheOption),
                CleanAll = parseResult.GetValue(allOption),
                DryRun = parseResult.GetValue(dryRunOption),
                Force = parseResult.GetValue(forceOption)
            };

            return Execute(options);
        });

        return command;
    }

    private int Execute(CleanOptions options)
    {
        var targets = new List<CleanTarget>();

        // Output is always cleaned (unless only --cache is specified)
        if (!options.CleanCache || options.CleanAll)
        {
            if (Directory.Exists(OutputDirectory))
            {
                targets.Add(AnalyzeDirectory(OutputDirectory));
            }
        }

        // Cache is cleaned with --cache or --all
        if (options.CleanCache || options.CleanAll)
        {
            if (Directory.Exists(CacheDirectory))
            {
                targets.Add(AnalyzeDirectory(CacheDirectory));
            }
        }

        if (targets.Count == 0)
        {
            var emptyPanel = new Panel("[green]Nothing to clean.[/]")
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(emptyPanel);
            return 0;
        }

        // Calculate totals
        var totalFiles = targets.Sum(t => t.FileCount);
        var totalSize = targets.Sum(t => t.TotalSize);

        // Display what will be deleted
        if (options.DryRun)
        {
            var dryRunContent = BuildCleanSummary(targets, totalFiles, totalSize);
            dryRunContent += "\n\n[dim]Run without --dry-run to delete these files.[/]";

            var dryRunPanel = new Panel(dryRunContent)
            {
                Header = new PanelHeader("[bold yellow]Dry Run[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(dryRunPanel);
            return 0;
        }

        // Confirm for --all unless --force
        if (options.CleanAll && !options.Force)
        {
            var confirmContent = BuildCleanSummary(targets, totalFiles, totalSize);
            var confirmPanel = new Panel(confirmContent)
            {
                Header = new PanelHeader("[bold yellow]Confirm[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(confirmPanel);
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("Delete all generated files and cache?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
                return 0;
            }
        }

        // Execute deletion
        var deletedTargets = new List<CleanTarget>();
        foreach (var target in targets)
        {
            try
            {
                Directory.Delete(target.Path, recursive: true);
                LogDirectoryDeleted(logger, target.Path, target.FileCount);
                deletedTargets.Add(target);
            }
            catch (IOException ex)
            {
                LogDeleteFailed(logger, target.Path, ex);
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to delete {target.Path}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogDeleteFailed(logger, target.Path, ex);
                AnsiConsole.MarkupLine($"[red]✗[/] Access denied: {target.Path}");
            }
        }

        // Success panel
        if (deletedTargets.Count > 0)
        {
            var successContent = BuildCleanSummary(deletedTargets, totalFiles, totalSize);
            successContent += "\n\n[dim]Next steps:[/]\n";
            successContent += "1. Run [cyan]revela generate[/] to rebuild the site";

            var successPanel = new Panel(successContent)
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(successPanel);
        }

        return 0;
    }

    /// <summary>
    /// Build summary text for the panel.
    /// </summary>
    private static string BuildCleanSummary(
        IReadOnlyList<CleanTarget> targets,
        int totalFiles,
        long totalSize)
    {
        var lines = new List<string>
        {
            "[green]Clean summary[/]\n",
            "[dim]Directories:[/]"
        };

        foreach (var target in targets)
        {
            lines.Add($"  {target.Path}/  [dim]({target.FileCount} files, {FormatSize(target.TotalSize)})[/]");
        }

        lines.Add($"\n[dim]Total:[/] {totalFiles} files, {FormatSize(totalSize)}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Analyzes a directory to count files and calculate total size.
    /// </summary>
    private static CleanTarget AnalyzeDirectory(string path)
    {
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        return new CleanTarget
        {
            Path = path,
            FileCount = files.Length,
            TotalSize = totalSize
        };
    }

    /// <summary>
    /// Formats a file size in human-readable format.
    /// </summary>
    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => string.Format(CultureInfo.InvariantCulture, "{0} B", bytes),
            < 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} KB", bytes / 1024.0),
            < 1024 * 1024 * 1024 => string.Format(CultureInfo.InvariantCulture, "{0:0.#} MB", bytes / (1024.0 * 1024.0)),
            _ => string.Format(CultureInfo.InvariantCulture, "{0:0.##} GB", bytes / (1024.0 * 1024.0 * 1024.0))
        };
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Path} ({FileCount} files)")]
    private static partial void LogDirectoryDeleted(ILogger logger, string path, int fileCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete {Path}")]
    private static partial void LogDeleteFailed(ILogger logger, string path, Exception exception);

    #endregion
}

/// <summary>
/// Options for the clean command.
/// </summary>
internal sealed class CleanOptions
{
    /// <summary>Also clean cache directory.</summary>
    public bool CleanCache { get; init; }

    /// <summary>Clean both output and cache.</summary>
    public bool CleanAll { get; init; }

    /// <summary>Show what would be deleted without deleting.</summary>
    public bool DryRun { get; init; }

    /// <summary>Skip confirmation prompt.</summary>
    public bool Force { get; init; }
}

/// <summary>
/// Information about a directory to be cleaned.
/// </summary>
internal sealed class CleanTarget
{
    /// <summary>Path to the directory.</summary>
    public required string Path { get; init; }

    /// <summary>Number of files in the directory.</summary>
    public int FileCount { get; init; }

    /// <summary>Total size of all files in bytes.</summary>
    public long TotalSize { get; init; }
}
