using Spectara.Revela.Plugins.Core.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Core.Theme.Wizard;

/// <summary>
/// Required wizard step for selecting a theme.
/// </summary>
internal sealed class ThemeWizardStep(ConfigThemeCommand configThemeCommand) : IWizardStep
{
    /// <inheritdoc />
    public string Name => "Select Theme";

    /// <inheritdoc />
    public string Description => "Choose a theme for your site";

    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public bool IsRequired => true;

    /// <inheritdoc />
    public bool ShouldPrompt() => true;

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        configThemeCommand.ExecuteAsync(null, cancellationToken);
}
