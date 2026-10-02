using System.Text.Json.Nodes;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Records installed packages in <c>dependencies.packages</c>.
/// </summary>
/// <remarks>
/// One rule decides the target file: inside a project (project.json exists) packages are declared
/// only in project.json, otherwise only in the global revela.json. Uninstalling removes the
/// declaration from that same file.
/// </remarks>
public sealed partial class PackageDeclarations(
    IConfigService configService,
    IGlobalConfigManager globalConfigManager,
    ILogger<PackageDeclarations> logger)
{
    private const string PackagesKey = "packages";

    /// <summary>
    /// Gets the configuration file that declarations are written to.
    /// </summary>
    public string TargetPath => TargetsProject ? configService.ProjectConfigPath : globalConfigManager.ConfigFilePath;

    private bool TargetsProject => configService.IsProjectInitialized();

    /// <summary>
    /// Declares <paramref name="packageId"/> with its exact installed version in the target file.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The exact installed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeclareAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TargetsProject)
        {
            await configService.UpdateProjectConfigAsync(CreatePatch(packageId, version), cancellationToken);
        }
        else
        {
            await globalConfigManager.AddPackageAsync(packageId, version, cancellationToken);
        }

        LogDeclared(logger, packageId, version, TargetPath);
    }

    /// <summary>
    /// Removes the declaration of <paramref name="packageId"/> from the target file.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RemoveAsync(string packageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TargetsProject)
        {
            await configService.UpdateProjectConfigAsync(CreatePatch(packageId, version: null), cancellationToken);
        }
        else
        {
            _ = await globalConfigManager.RemovePackageAsync(packageId, cancellationToken);
        }

        LogRemoved(logger, packageId, TargetPath);
    }

    /// <summary>
    /// Records the exact restored version of a package that the target file already declares.
    /// </summary>
    /// <remarks>
    /// Packages declared only in another file (e.g. a global dependency restored inside a project)
    /// are left alone, so restoring never copies declarations between files.
    /// </remarks>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The exact installed version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the declaration was updated.</returns>
    public async Task<bool> PinAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await IsDeclaredInTargetAsync(packageId, cancellationToken))
        {
            return false;
        }

        await DeclareAsync(packageId, version, cancellationToken);
        return true;
    }

    private async Task<bool> IsDeclaredInTargetAsync(string packageId, CancellationToken cancellationToken)
    {
        if (!TargetsProject)
        {
            var globalPackages = await globalConfigManager.GetPackagesAsync(cancellationToken);
            return globalPackages.Keys.Any(id => id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        }

        var project = await configService.ReadProjectConfigAsync(cancellationToken);
        var dependencies = FindProperty(project, DependenciesConfig.Section) as JsonObject;
        return FindProperty(dependencies, PackagesKey) is JsonObject packages
            && packages.Any(property => property.Key.Equals(packageId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonNode? FindProperty(JsonObject? node, string name) =>
        node?.FirstOrDefault(property => property.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static JsonObject CreatePatch(string packageId, string? version) => new()
    {
        [DependenciesConfig.Section] = new JsonObject
        {
            [PackagesKey] = new JsonObject { [packageId] = version }
        }
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Declared package {PackageId} {Version} in {ConfigPath}")]
    private static partial void LogDeclared(ILogger logger, string packageId, string version, string configPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed package {PackageId} from {ConfigPath}")]
    private static partial void LogRemoved(ILogger logger, string packageId, string configPath);
}
