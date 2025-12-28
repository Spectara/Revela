using System.CommandLine;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins;

/// <summary>
/// Handles 'revela plugin list' command.
/// </summary>
public sealed partial class PluginListCommand(
    ILogger<PluginListCommand> logger,
    IPluginContext pluginContext)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List loaded plugins");

        command.SetAction(async (_, cancellationToken) =>
        {
            await ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogListingPlugins();

            // Filter out themes and theme extensions (shown in 'theme list' instead)
            var loadedPlugins = pluginContext.Plugins
                .Where(p => p.Plugin is not IThemePlugin and not IThemeExtension)
                .ToList();

            if (loadedPlugins.Count == 0)
            {
                ErrorPanels.ShowNothingInstalledError(
                    "plugins",
                    "plugin install <name>");
                return Task.CompletedTask;
            }

            // Build content for panel
            var content = new List<string>();

            foreach (var pluginInfo in loadedPlugins)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metadata = pluginInfo.Plugin.Metadata;
                var sourceMarkup = GetSourceMarkup(pluginInfo.Source);

                content.Add($"[green]+[/] [bold green]{EscapeMarkup(metadata.Name)}[/] [dim]v{metadata.Version}[/] {sourceMarkup}");
                content.Add($"   [dim]{EscapeMarkup(metadata.Description)}[/]");
                content.Add("");
            }

            // Remove last empty line
            if (content.Count > 0 && string.IsNullOrEmpty(content[^1]))
            {
                content.RemoveAt(content.Count - 1);
            }

            var panel = new Panel(new Markup(string.Join("\n", content)))
                .WithHeader($"[bold]Installed Plugins[/] [dim]({loadedPlugins.Count})[/]")
                .WithInfoStyle();
            panel.Padding = new Padding(1, 0, 1, 0);

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[dim]Tip:[/] Use [cyan]revela plugin install <name>[/] to add more plugins");
        }
        catch (Exception ex)
        {
            LogError(ex);
            ErrorPanels.ShowException(ex);
        }

        return Task.CompletedTask;
    }

    private static string GetSourceMarkup(PluginSource source) => source switch
    {
        PluginSource.Bundled => "[magenta]bundled[/]",
        PluginSource.Local => "[green]installed[/]",
        _ => "[dim]unknown[/]"
    };

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing loaded plugins")]
    private partial void LogListingPlugins();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list plugins")]
    private partial void LogError(Exception exception);
}

