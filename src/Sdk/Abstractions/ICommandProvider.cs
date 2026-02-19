namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Provides command descriptors for CLI registration.
/// </summary>
/// <remarks>
/// Shared contract for both core commands and plugin-like command providers.
/// Plugins use <see cref="IPlugin.GetCommands"/> (same signature) instead.
/// </remarks>
public interface ICommandProvider
{
    /// <summary>
    /// Gets command descriptors for CLI registration.
    /// </summary>
    /// <param name="services">Built service provider to resolve commands from DI.</param>
    /// <returns>Sequence of command descriptors.</returns>
    IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services);
}
