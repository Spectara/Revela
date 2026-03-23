using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Spectara.Revela.Core.Services;
using Spectara.Revela.Plugins.Core.Theme.Commands;
using Spectara.Revela.Plugins.Core.Theme.Wizard;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Core.Theme;

/// <summary>
/// Theme management plugin — list, install, extract, and inspect themes.
/// </summary>
public sealed class ThemePlugin : IPlugin
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Core.Theme",
        Name = "Theme",
        Version = "1.0.0",
        Description = "Theme management (list, install, extract, inspect)",
        Author = "Spectara",
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Theme infrastructure (TryAdd for idempotent registration)
        services.TryAddSingleton<IThemeResolver, ThemeResolver>();

        // Commands
        services.TryAddTransient<ThemeCommand>();
        services.TryAddTransient<ThemeListCommand>();
        services.TryAddTransient<ThemeFilesCommand>();
        services.TryAddTransient<ThemeExtractCommand>();
        services.TryAddTransient<ThemeInstallCommand>();
        services.TryAddTransient<ThemeUninstallCommand>();
        services.TryAddTransient<ConfigThemeCommand>();

        // Wizard steps (project setup)
        services.TryAddEnumerable(ServiceDescriptor.Transient<IWizardStep, ThemeWizardStep>());
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        var themeCommand = services.GetRequiredService<ThemeCommand>();
        yield return new CommandDescriptor(
            themeCommand.Create(),
            Order: 10,
            Group: "Addons",
            RequiresProject: false);

        var configThemeCommand = services.GetRequiredService<ConfigThemeCommand>();
        yield return new CommandDescriptor(
            configThemeCommand.Create(),
            ParentCommand: "config",
            Order: 20,
            Group: "Project");
    }
}
