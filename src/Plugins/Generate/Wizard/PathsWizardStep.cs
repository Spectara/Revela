using Spectara.Revela.Plugins.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Generate.Wizard;

/// <summary>
/// Required wizard step for configuring source and output directories.
/// </summary>
internal sealed class PathsWizardStep(ConfigPathsCommand configPathsCommand) : IWizardStep
{
    /// <inheritdoc />
    public string Name => "Directory Paths";

    /// <inheritdoc />
    public string Description => "Configure source and output directories (defaults work for most users)";

    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public bool IsRequired => true;

    /// <inheritdoc />
    public bool ShouldPrompt() => true;

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        configPathsCommand.ExecuteAsync(null, null, cancellationToken);
}
