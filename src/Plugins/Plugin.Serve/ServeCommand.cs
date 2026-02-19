using System.CommandLine;
using System.Globalization;
using System.Net;

using Microsoft.Extensions.Options;

using Spectara.Revela.Plugin.Serve.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Plugin.Serve;

/// <summary>
/// Command to start a local HTTP server for previewing generated sites
/// </summary>
internal sealed partial class ServeCommand(
    ILogger<ServeCommand> logger,
    IPathResolver pathResolver,
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

        var verboseOption = new Option<bool?>("--verbose", "-v")
        {
            Description = "Log all requests (default: only 404 errors)"
        };

        command.Options.Add(portOption);
        command.Options.Add(verboseOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var port = parseResult.GetValue(portOption);
            var verbose = parseResult.GetValue(verboseOption);

            return await ServeAsync(port, verbose, cancellationToken);
        });

        return command;
    }

    private async Task<int> ServeAsync(int? portOverride, bool? verboseOverride, CancellationToken cancellationToken)
    {
        // Resolve configuration: CLI > Config > Default
        var config = serveConfig.CurrentValue;
        var port = portOverride ?? config.Port;
        var verbose = verboseOverride ?? config.Verbose;

        // Output directory from resolved path (supports absolute paths in config)
        var fullOutputPath = pathResolver.OutputPath;

        // Validate output directory exists
        if (!Directory.Exists(fullOutputPath))
        {
            ErrorPanels.ShowDirectoryNotFoundError(fullOutputPath, "generate all");
            return 1;
        }

        // Check if index.html exists
        var indexPath = Path.Combine(fullOutputPath, "index.html");
        if (!File.Exists(indexPath))
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No index.html found in [cyan]{Markup.Escape(fullOutputPath)}[/]");
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
                    Markup.Escape(path),
                    color,
                    statusCode));
            }
            else if (statusCode == 404)
            {
                // Only log 404s in normal mode
                AnsiConsole.MarkupLine($"{OutputMarkers.Warning} [yellow]404:[/] {Markup.Escape(path)}");
            }
        }

        // Create linked CancellationTokenSource for graceful shutdown
        // Combines external cancellation (interactive menu) with Ctrl+C
        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Handle Ctrl+C directly for non-interactive mode
        Console.CancelKeyPress += OnCancelKeyPress;

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            shutdownCts.Cancel();
        }

        // Create and start server
        await using var server = new StaticFileServer(fullOutputPath, port, RequestCallback);
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
        AnsiConsole.MarkupLine($"🌐 Serving [blue]{Markup.Escape(fullOutputPath)}[/] at [link]http://localhost:{port}[/]");
        AnsiConsole.MarkupLine("[dim]   Press Ctrl+C to stop[/]");
        AnsiConsole.WriteLine();

        LogServerStarted(logger, fullOutputPath, port);

        // Wait for cancellation (zero-CPU, no polling)
        try
        {
            await Task.Delay(Timeout.Infinite, shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — shutdown requested via Ctrl+C or external cancellation
        }

        AnsiConsole.MarkupLine("\n[yellow]Stopping server...[/]");

        // Cleanup event handler
        Console.CancelKeyPress -= OnCancelKeyPress;

        // Server disposed automatically by using statement
        LogServerStopped(logger);
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Server started: {RootPath} on port {Port}")]
    private static partial void LogServerStarted(ILogger logger, string rootPath, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server stopped")]
    private static partial void LogServerStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start server on port {Port}")]
    private static partial void LogServerError(ILogger logger, int port, Exception ex);
}
