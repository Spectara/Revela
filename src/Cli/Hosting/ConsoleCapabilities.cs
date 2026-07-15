using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Default <see cref="IConsoleCapabilities"/> implementation backed by
/// <see cref="Console"/> redirection flags and <see cref="Environment.UserInteractive"/>.
/// </summary>
/// <remarks>
/// Properties are evaluated on access (not cached) because redirection state is
/// fixed for the lifetime of the process — the cost is a couple of cheap
/// runtime-cached reads, so there is no benefit to caching them here.
/// </remarks>
internal sealed class ConsoleCapabilities : IConsoleCapabilities
{
    /// <inheritdoc />
    public bool IsInteractive =>
        !Console.IsInputRedirected
        && !Console.IsOutputRedirected
        && Environment.UserInteractive;

    /// <inheritdoc />
    public bool CanRenderLive =>
        !Console.IsOutputRedirected
        && Environment.UserInteractive;
}
