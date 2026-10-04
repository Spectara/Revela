using System.Reflection;
using System.Text.Json.Nodes;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Default <see cref="IPluginSettingsWriter{TConfig}"/>: writes a plugin's settings below the
/// <c>plugins:&lt;key&gt;</c> its own assembly claims.
/// </summary>
/// <remarks>
/// The section comes from <typeparamref name="TConfig"/>'s <see cref="RevelaConfigAttribute"/>, so a
/// caller cannot name another section. The claim check (key claimed by the assembly that declares
/// <typeparamref name="TConfig"/>) runs on every write; it is a cheap dictionary lookup.
/// </remarks>
/// <typeparam name="TConfig">The plugin's <c>[RevelaConfig]</c> options type.</typeparam>
internal sealed partial class PluginSettingsWriter<TConfig>(
    IConfigService configService,
    PluginConfigOwnership ownership,
    ILogger<PluginSettingsWriter<TConfig>> logger) : IPluginSettingsWriter<TConfig>
    where TConfig : class
{
    /// <inheritdoc />
    public async Task WriteAsync(JsonObject settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var key = ResolveOwnKey();
        var updates = new JsonObject
        {
            [PluginConfigSection.Root] = new JsonObject { [key] = settings.DeepClone() },
        };

        await configService.UpdateProjectConfigAsync(updates, cancellationToken);
        LogSettingsWritten(key, configService.ProjectConfigPath);
    }

    private static string TypeName => typeof(TConfig).FullName ?? typeof(TConfig).Name;

    private string ResolveOwnKey()
    {
        var type = typeof(TConfig);
        var section = type.GetCustomAttribute<RevelaConfigAttribute>()?.SectionName
            ?? throw new InvalidOperationException(
                $"'{TypeName}' has no [RevelaConfig] attribute, so it has no settings section to write.");

        if (!PluginConfigSection.TryGetKey(section, out var key))
        {
            throw new InvalidOperationException(
                $"'{TypeName}' declares the section '{section}'. Plugins can only write their own '{PluginConfigSection.Prefix}<key>' section.");
        }

        var claim = ownership.FindClaim(key)
            ?? throw new InvalidOperationException(
                $"No loaded package claims '{section}', so '{TypeName}' cannot write it.");

        if (claim.Assembly != type.Assembly)
        {
            throw new InvalidOperationException(
                $"'{section}' belongs to package '{claim.PackageId}'; '{TypeName}' from '{type.Assembly.GetName().Name}' cannot write it.");
        }

        return key;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Wrote plugin settings 'plugins:{Key}' to {Path}")]
    private partial void LogSettingsWritten(string key, string path);
}
