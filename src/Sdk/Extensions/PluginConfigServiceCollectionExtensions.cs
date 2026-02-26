using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering plugin configuration with the DI container.
/// </summary>
public static class PluginConfigServiceCollectionExtensions
{
    /// <summary>
    /// Registers a plugin configuration model with automatic section binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the recommended way for plugins to register their configuration.
    /// It automatically binds the configuration section from project.json
    /// using <see cref="IPluginConfig.SectionName"/>.
    /// </para>
    /// <para>
    /// By default, NO validation is applied. Plugins may be installed but not yet
    /// configured — validation via <c>IOptionsMonitor</c> would crash the application
    /// on every config reload when required properties are missing.
    /// </para>
    /// <para>
    /// Plugins should validate configuration in their commands when it's actually needed.
    /// Use the <paramref name="configure"/> callback to opt into DataAnnotation validation
    /// only when all config properties have safe defaults.
    /// </para>
    /// <example>
    /// <code>
    /// // In plugin's ConfigureServices:
    /// public void ConfigureServices(IServiceCollection services)
    /// {
    ///     // Simple registration (recommended for most plugins):
    ///     services.AddPluginConfig&lt;MyPluginConfig&gt;();
    ///
    ///     // With validation (only when all properties have safe defaults):
    ///     services.AddPluginConfig&lt;MyPluginConfig&gt;(options =&gt;
    ///     {
    ///         options.ValidateDataAnnotations = true;
    ///     });
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <typeparam name="TConfig">
    /// Plugin configuration type implementing <see cref="IPluginConfig"/>.
    /// Must have a <c>static abstract string SectionName</c> property.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to customize registration behavior.</param>
    /// <returns>The <see cref="OptionsBuilder{TConfig}"/> for further chaining.</returns>
    public static OptionsBuilder<TConfig> AddPluginConfig<TConfig>(
        this IServiceCollection services,
        Action<PluginConfigOptions>? configure = null)
        where TConfig : class, IPluginConfig
    {
        var options = new PluginConfigOptions();
        configure?.Invoke(options);

        var builder = services
            .AddOptions<TConfig>()
            .BindConfiguration(TConfig.SectionName);

        if (options.ValidateDataAnnotations)
        {
            builder.ValidateDataAnnotations();
        }

        if (options.ValidateOnStart)
        {
            builder.ValidateOnStart();
        }

        return builder;
    }
}
