using System.CommandLine;
using System.Globalization;
using System.Net;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugin.Serve.Configuration;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Command to start a local HTTP server for previewing generated sites
/// </summary>
public sealed partial class ServeCommand(
    ILogger<ServeCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ServeConfig> serveConfig)
{
    /// <summary>
    /// Create the serve command
    /// </summary>
    public Command Create()
    {
        var command = new Command("serve", "Preview generated site with local HTTP server");

        var portOption = new Option<int?>("--port", "-p")
        {
            Description = "Port number (default: 8080)"
        };

        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Log all requests (default: only 404 errors)"
        };

        var pathOption = new Option<string?>("--path")
        {
            Description = "Output directory to serve (default: output)"
        };

        command.Options.Add(portOption);
        command.Options.Add(verboseOption);
        command.Options.Add(pathOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var port = parseResult.GetValue(portOption);
            var verbose = parseResult.GetValue(verboseOption);
            var path = parseResult.GetValue(pathOption);

            return await ServeAsync(port, verbose, path, cancellationToken);
        });

        return command;
    }

    private async Task<int> ServeAsync(int? portOverride, bool verboseOverride, string? pathOverride, CancellationToken cancellationToken)
    {
        // Resolve configuration: CLI > Config > Default
        var config = serveConfig.CurrentValue;
        var port = portOverride ?? config.Port;
        var verbose = verboseOverride || config.Verbose;

        // Get output directory: CLI override or default "output"
        var outputPath = pathOverride ?? "output";
        var fullOutputPath = Path.GetFullPath(Path.Combine(projectEnvironment.Value.Path, outputPath));

        // Validate output directory exists
        if (!Directory.Exists(fullOutputPath))
        {
            ErrorPanels.ShowDirectoryNotFoundError($"{outputPath}/", "generate all");
            return 1;
        }

        // Check if index.html exists
        var indexPath = Path.Combine(fullOutputPath, "index.html");
        if (!File.Exists(indexPath))
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] No index.html found in [cyan]{outputPath}/[/]");
        }

        // Setup request logging callback
        void RequestCallback(string path, int statusCode)
        {
            if (verbose)
            {
                // Log all requests in verbose mode
                var color = statusCode switch
                {
                    200 => "green",
                    304 => "blue",
                    404 => "yellow",
                    _ => "red"
                };
                AnsiConsole.MarkupLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "[dim]GET[/] {0} [{1}]{2}[/]",
                    EscapeMarkup(path),
                    color,
                    statusCode));
            }
            else if (statusCode == 404)
            {
                // Only log 404s in normal mode
                AnsiConsole.MarkupLine($"[yellow]⚠ 404:[/] {EscapeMarkup(path)}");
            }
        }

        // Track if we should keep running
        var running = true;

        // Use CancellationToken for graceful shutdown (works in interactive menu)
        using var registration = cancellationToken.Register(() =>
        {
            running = false;
            AnsiConsole.MarkupLine("\n[yellow]Stopping server...[/]");
        });

        // Also handle Ctrl+C directly for non-interactive mode
        Console.CancelKeyPress += OnCancelKeyPress;

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            running = false;
            AnsiConsole.MarkupLine("\n[yellow]Stopping server...[/]");
        }

        // Create and start server
        using var server = new StaticFileServer(fullOutputPath, port, RequestCallback);
        try
        {
            server.Start();
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
        {
            ErrorPanels.ShowPortError(port, "access denied", "Try running as administrator or use a port above 1024.");
            return 1;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode is 183 or 32) // Port in use
        {
            ErrorPanels.ShowPortError(port, "is already in use", "Try a different port: revela serve --port 3000");
            return 1;
        }
        catch (HttpListenerException ex)
        {
            ErrorPanels.ShowException(ex, "Failed to start the HTTP server.");
            LogServerError(logger, port, ex);
            return 1;
        }

        // Display server info
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"🌐 Serving [blue]{outputPath}/[/] at [link]http://localhost:{port}[/]");
        AnsiConsole.MarkupLine("[dim]   Press Ctrl+C to stop[/]");
        AnsiConsole.WriteLine();

        LogServerStarted(logger, fullOutputPath, port);

        // Wait for cancellation
        while (running && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, CancellationToken.None);
        }

        // Cleanup event handler
        Console.CancelKeyPress -= OnCancelKeyPress;

        // Server disposed automatically by using statement
        LogServerStopped(logger);
        return 0;
    }

    /// <summary>
    /// Escape Spectre.Console markup characters in user data
    /// </summary>
    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Server started: {RootPath} on port {Port}")]
    private static partial void LogServerStarted(ILogger logger, string rootPath, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server stopped")]
    private static partial void LogServerStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start server on port {Port}")]
    private static partial void LogServerError(ILogger logger, int port, Exception ex);
}
