using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// Fixed <see cref="IConsoleCapabilities"/> for tests.
/// </summary>
/// <param name="isInteractive">Whether prompts are allowed.</param>
/// <param name="canRenderLive">Whether live progress output is allowed.</param>
public sealed class FakeConsoleCapabilities(bool isInteractive, bool canRenderLive = false) : IConsoleCapabilities
{
    /// <summary>
    /// A console without a terminal (CI, pipes): no prompts, no live output.
    /// </summary>
    public static FakeConsoleCapabilities NonInteractive { get; } = new(isInteractive: false);

    /// <inheritdoc />
    public bool IsInteractive => isInteractive;

    /// <inheritdoc />
    public bool CanRenderLive => canRenderLive;
}
