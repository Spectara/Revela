namespace Spectara.Revela.Sdk.Hosting;

/// <summary>
/// Describes what the current console can do, so commands and plugins can
/// adapt their output instead of assuming an interactive terminal.
/// </summary>
/// <remarks>
/// <para>
/// Single source of truth for "is this an interactive terminal?". Replaces
/// scattered <c>Console.IsOutputRedirected</c> / <c>Environment.UserInteractive</c>
/// checks so the policy lives in one place, stays consistent across the host
/// and plugins, and can be faked in tests.
/// </para>
/// <para>
/// Plugins that render Spectre.Console live output (progress bars, spinners,
/// <c>AnsiConsole.Live</c>) should gate it on <see cref="CanRenderLive"/>. The
/// low-level <c>AnsiConsole.Live</c> primitive hides the terminal cursor
/// unconditionally and throws on a non-interactive console (redirected output,
/// CI, no TTY). <c>AnsiConsole.Status</c> / <c>AnsiConsole.Progress</c> already
/// fall back on their own, so they don't need the guard.
/// </para>
/// </remarks>
public interface IConsoleCapabilities
{
    /// <summary>
    /// <c>true</c> when both stdin and stdout are attached to an interactive
    /// terminal — safe to show prompts or launch the interactive menu.
    /// </summary>
    bool IsInteractive { get; }

    /// <summary>
    /// <c>true</c> when stdout can render a live, animated display (progress
    /// bars, spinners, <c>AnsiConsole.Live</c>). Requires a non-redirected
    /// output stream in an interactive session; independent of stdin.
    /// </summary>
    bool CanRenderLive { get; }
}
