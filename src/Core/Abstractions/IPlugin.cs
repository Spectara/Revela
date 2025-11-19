using System.CommandLine;

namespace Spectara.Revela.Core.Abstractions;

/// <summary>
/// Plugin interface - all plugins must implement this
/// </summary>
public interface IPlugin
{
    IPluginMetadata Metadata { get; }
    void Initialize(IServiceProvider services);
    IEnumerable<Command> GetCommands();
}

/// <summary>
/// Plugin metadata
/// </summary>
public interface IPluginMetadata
{
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }
}

