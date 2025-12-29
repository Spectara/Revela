using Microsoft.Extensions.Options;
using Spectara.Revela.Plugin.Source.OneDrive.Commands;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugin.Source.OneDrive.Wizard;

/// <summary>
/// Wizard step for configuring OneDrive source plugin.
/// </summary>
/// <remarks>
/// <para>
/// This step is offered during project setup if the OneDrive plugin is installed
/// but not yet configured. It allows users to set up their OneDrive shared folder
/// URL as part of the initial project setup flow.
/// </para>
/// <para>
/// The step delegates to <see cref="ConfigOneDriveCommand"/> for the actual
/// configuration, ensuring consistency with manual configuration.
/// </para>
/// </remarks>
public sealed class OneDriveWizardStep(
    ConfigOneDriveCommand configCommand,
    IOptionsMonitor<OneDrivePluginConfig> configMonitor) : IWizardStep
{
    /// <inheritdoc />
    public string Name => "OneDrive Source";

    /// <inheritdoc />
    public string Description => "Import images from OneDrive shared folder";

    /// <inheritdoc />
    public int Order => 100; // Source providers first

    /// <inheritdoc />
    public bool ShouldPrompt()
    {
        // Only prompt if not already configured (ShareUrl is empty)
        var config = configMonitor.CurrentValue;
        return string.IsNullOrEmpty(config.ShareUrl);
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Delegate to the config command (interactive mode - no arguments)
        // ConfigOneDriveCommand.ExecuteAsync() handles all the prompts
        return configCommand.ExecuteInteractiveAsync(cancellationToken);
    }
}
