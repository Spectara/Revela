using Spectara.Revela.Plugins.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Generate.Wizard;

/// <summary>
/// Required wizard step for configuring image output formats and sizes.
/// </summary>
internal sealed class ImagesWizardStep(ConfigImageCommand configImageCommand) : IWizardStep
{
    /// <inheritdoc />
    public string Name => "Image Settings";

    /// <inheritdoc />
    public string Description => "Configure output formats and sizes for your images";

    /// <inheritdoc />
    public int Order => 40;

    /// <inheritdoc />
    public bool IsRequired => true;

    /// <inheritdoc />
    public bool ShouldPrompt() => true;

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        configImageCommand.ExecuteAsync(null, cancellationToken);
}
