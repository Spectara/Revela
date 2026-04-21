namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// An <see cref="IProgress{T}"/> implementation that invokes the callback
/// synchronously on the calling thread.
/// </summary>
/// <remarks>
/// Unlike <see cref="Progress{T}"/>, which dispatches via
/// <see cref="SynchronizationContext"/> or ThreadPool, this implementation
/// calls the handler directly. This prevents progress updates from being
/// lost when the consumer (e.g., Spectre.Console Live display) ends before
/// queued callbacks are processed.
/// </remarks>
/// <typeparam name="T">The type of progress value.</typeparam>
internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
