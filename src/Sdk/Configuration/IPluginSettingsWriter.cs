using System.Text.Json.Nodes;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Persists a plugin's own settings — the only way a plugin or theme writes configuration.
/// </summary>
/// <typeparam name="TConfig">
/// The plugin's options type, marked with <see cref="Abstractions.RevelaConfigAttribute"/>
/// (for example <c>[RevelaConfig("plugins:serve")]</c>). Its section decides where the
/// settings are written; there is no way to name another section.
/// </typeparam>
/// <remarks>
/// <para>
/// Settings are deep-merged below <c>plugins:&lt;key&gt;</c> in the project's <c>project.json</c>:
/// only the given properties change, a <see langword="null"/> value removes a property, and
/// the configuration reloads afterwards so <c>IOptionsMonitor&lt;TConfig&gt;</c> sees the new
/// values. Global defaults in <c>revela.json</c> and environment variables are not touched.
/// </para>
/// <para>
/// Inject it like any service. Before writing, the host checks that the package whose
/// assembly declares <typeparamref name="TConfig"/> claims that <c>plugins:&lt;key&gt;</c>;
/// otherwise <see cref="WriteAsync"/> throws <see cref="InvalidOperationException"/>.
/// </para>
/// <code>
/// internal sealed class ConfigMyPluginCommand(IPluginSettingsWriter&lt;MyPluginConfig&gt; settings)
/// {
///     private Task SaveAsync(int timeout, CancellationToken cancellationToken) =>
///         settings.WriteAsync(new JsonObject { [MyPluginConfigKeys.Timeout] = timeout }, cancellationToken);
/// }
/// </code>
/// </remarks>
public interface IPluginSettingsWriter<TConfig>
    where TConfig : class
{
    /// <summary>
    /// Merges <paramref name="settings"/> into this plugin's section of <c>project.json</c>.
    /// </summary>
    /// <param name="settings">Properties of <typeparamref name="TConfig"/> to set; <see langword="null"/> values remove a property.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TConfig"/> has no <c>plugins:&lt;key&gt;</c> section, or the key is not
    /// claimed by the package that declares <typeparamref name="TConfig"/>.
    /// </exception>
    Task WriteAsync(JsonObject settings, CancellationToken cancellationToken = default);
}
