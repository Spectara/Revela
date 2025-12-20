using System.CommandLine;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Service for running the interactive CLI mode.
/// </summary>
internal interface IInteractiveMenuService
{
    /// <summary>
    /// Gets or sets the root command for the CLI.
    /// </summary>
    /// <remarks>
    /// This must be set before calling <see cref="RunAsync"/>.
    /// </remarks>
    RootCommand? RootCommand { get; set; }

    /// <summary>
    /// Runs the interactive menu loop.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The exit code (0 for success).</returns>
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
