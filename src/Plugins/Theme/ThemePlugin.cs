using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Theme;

/// <summary>
/// Theme management plugin — list, install, extract, and inspect themes.
/// </summary>
public sealed class ThemePlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new()
    {
        Name = "Theme",
        Version = "1.0.0",
        Description = "Theme management (list, install, extract, inspect)",
        Author = "Spectara",
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Theme infrastructure
        services.AddSingleton<IThemeResolver, ThemeResolver>();

        // Commands
        services.AddTransient<ThemeCommand>();
        services.AddTransient<ThemeListCommand>();
        services.AddTransient<ThemeFilesCommand>();
        services.AddTransient<ThemeExtractCommand>();
        services.AddTransient<ThemeInstallCommand>();
        services.AddTransient<ThemeUninstallCommand>();
        services.AddTransient<ConfigThemeCommand>();
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        yield break;
    }
}
