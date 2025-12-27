using System.CommandLine;

using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin install' command.
/// </summary>
/// <remarks>
/// Installs plugins from NuGet. Before running, use 'revela packages refresh'
/// to update the package index for better type validation.
/// </remarks>
public sealed partial class PluginInstallCommand(
    ILogger<PluginInstallCommand> logger,
    PluginManager pluginManager,
    IPackageIndexService packageIndexService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("install", "Install a plugin from NuGet");

        var nameArgument = new Argument<string?>("name")
        {
            Description = "Plugin name (e.g., 'onedrive' for Revela.Plugin.OneDrive)",
            Arity = ArgumentArity.ZeroOrOne
        };
        command.Arguments.Add(nameArgument);

        var versionOption = new Option<string?>("--version", "-v")
        {
            Description = "Specific version to install (optional)"
        };
        command.Options.Add(versionOption);

        var globalOption = new Option<bool>("--global", "-g")
        {
            Description = "Install globally to AppData (default: local, next to executable)"
        };
        command.Options.Add(globalOption);

        var sourceOption = new Option<string?>("--source", "-s")
        {
            Description = "NuGet source name (from 'revela plugin source list') or URL"
        };
        command.Options.Add(sourceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameArgument);
            var version = parseResult.GetValue(versionOption);
            var global = parseResult.GetValue(globalOption);
            var source = parseResult.GetValue(sourceOption);

            // No name provided → show interactive selection
            if (string.IsNullOrEmpty(name))
            {
                name = await SelectPluginInteractivelyAsync(cancellationToken);
                if (name is null)
                {
                    return 0; // User cancelled
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await ExecuteFromNuGetAsync(name, version, global, source, cancellationToken);
        });

        return command;
    }

    private async Task<string?> SelectPluginInteractivelyAsync(CancellationToken cancellationToken)
    {
        var plugins = await packageIndexService.SearchByTypeAsync("RevelaPlugin", cancellationToken);

        if (plugins.Count == 0)
        {
            var indexAge = packageIndexService.GetIndexAge();
            if (indexAge is null)
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Package index not found.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] first.");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] No plugins found in package index.");
                AnsiConsole.MarkupLine("  Run [cyan]revela packages refresh[/] to update.");
            }

            return null;
        }

        var choices = plugins
            .Select(p => $"{p.Id} [dim]({p.Version})[/] - {Truncate(p.Description, 40)}")
            .Concat(["[dim]Cancel[/]"])
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select a plugin to install:[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

        if (selection == "[dim]Cancel[/]")
        {
            return null;
        }

        // Extract package ID from selection (before first space)
        return selection.Split(' ')[0];
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    internal async Task<int> ExecuteFromNuGetAsync(string name, string? version, bool global, string? source = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert short name to full package ID
            // Examples: "OneDrive" → "Spectara.Revela.Plugin.OneDrive"
            //           "Source.OneDrive" → "Spectara.Revela.Plugin.Source.OneDrive"
            //           "Spectara.Revela.Plugin.OneDrive" → unchanged
            //           "Spectara.Revela.Theme.Lumina.Statistics" → unchanged
            var packageId = name.StartsWith("Spectara.Revela.", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"Spectara.Revela.Plugin.{name}";

            // Check package type in index (if available)
            var packageEntry = await packageIndexService.FindPackageAsync(packageId, cancellationToken);
            if (packageEntry is not null)
            {
                // Validate it's a plugin, not a theme
                if (packageEntry.Types.Contains("RevelaTheme", StringComparer.OrdinalIgnoreCase) &&
                    !packageEntry.Types.Contains("RevelaPlugin", StringComparer.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Package [cyan]{packageId}[/] is a theme, not a plugin.");
                    AnsiConsole.MarkupLine("  Use [cyan]revela theme install[/] for themes.");
                    return 1;
                }
            }

            var location = global ? "globally" : "locally";
            var sourceInfo = source is not null ? $" from [dim]{source}[/]" : "";
            AnsiConsole.MarkupLine($"[blue]Installing plugin {location}:[/] [cyan]{packageId}[/]{sourceInfo}");
            LogInstallingPlugin(packageId, version, source);

            var success = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing...", async ctx =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ctx.Status($"Downloading {packageId}...");
                    return await pluginManager.InstallAsync(packageId, version, source, global, cancellationToken);
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]Plugin '{packageId}' installed successfully.[/]");
                AnsiConsole.MarkupLine("[dim]The plugin will be available after restarting revela.[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to install plugin '{packageId}'[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
            ErrorPanels.ShowException(ex);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing plugin '{PackageId}' version '{Version}' from source '{Source}'")]
    private partial void LogInstallingPlugin(string packageId, string? version, string? source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to install plugin")]
    private partial void LogError(Exception exception);
}

