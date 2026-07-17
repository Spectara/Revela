using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Extension methods for adding <c>site.json</c> as a configuration source.
/// </summary>
/// <remarks>
/// <para>
/// <c>site.json</c> stores its content/identity properties (title, description, …)
/// at the document root, but <see cref="SiteCoreConfig"/> binds from the
/// <see cref="SiteCoreConfig.Section"/> (<c>"site"</c>) section. This source therefore
/// re-keys every value under that prefix so <c>BindConfiguration("site")</c> resolves it.
/// </para>
/// <para>
/// The dynamic <see cref="System.Text.Json.JsonElement"/> tail of <c>site.json</c>
/// (theme-specific properties) is still loaded separately by the render pipeline —
/// this source only feeds the typed, validated core.
/// </para>
/// </remarks>
public static class SiteJsonConfigurationExtensions
{
    /// <summary>
    /// Adds <c>site.json</c> to the configuration, re-keyed under the
    /// <see cref="SiteCoreConfig.Section"/> section.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="path">Absolute or relative path to <c>site.json</c>.</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="reloadOnChange">Whether to reload when the file changes.</param>
    /// <returns>The builder for chaining.</returns>
    public static IConfigurationBuilder AddSiteJson(
        this IConfigurationBuilder builder,
        string path,
        bool optional,
        bool reloadOnChange)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);

        return builder.Add<SiteJsonConfigurationSource>(source =>
        {
            source.Path = path;
            source.Optional = optional;
            source.ReloadOnChange = reloadOnChange;
            source.ResolveFileProvider();
        });
    }
}

/// <summary>
/// A JSON configuration source that re-keys <c>site.json</c> values under the
/// <see cref="SiteCoreConfig.Section"/> section.
/// </summary>
internal sealed class SiteJsonConfigurationSource : JsonConfigurationSource
{
    /// <inheritdoc />
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new SiteJsonConfigurationProvider(this);
    }
}

/// <summary>
/// JSON provider that prefixes every loaded key with the
/// <see cref="SiteCoreConfig.Section"/> section so root-level <c>site.json</c>
/// properties bind to <see cref="SiteCoreConfig"/>.
/// </summary>
internal sealed class SiteJsonConfigurationProvider(JsonConfigurationSource source)
    : JsonConfigurationProvider(source)
{
    /// <inheritdoc />
    public override void Load(Stream stream)
    {
        base.Load(stream);

        Data = Data.ToDictionary(
            static pair => ConfigurationPath.Combine(SiteCoreConfig.Section, pair.Key),
            static pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }
}
