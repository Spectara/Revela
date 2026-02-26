namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Options for configuring plugin configuration registration behavior.
/// </summary>
/// <remarks>
/// <para>
/// Used with <c>AddPluginConfig&lt;T&gt;()</c> to customize validation behavior.
/// </para>
/// <para>
/// By default, NO validation is enabled because plugins may be installed but not yet
/// configured. The <c>IOptionsMonitor&lt;T&gt;</c> change tracking fires validation on
/// every config file reload — if a plugin's required properties are empty,
/// this would crash the entire application.
/// </para>
/// <para>
/// Plugins should validate configuration in their commands when it's actually needed,
/// not during registration.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default behavior (no validation — recommended for most plugins):
/// services.AddPluginConfig&lt;MyConfig&gt;();
///
/// // With DataAnnotation validation (only for plugins with safe defaults):
/// services.AddPluginConfig&lt;MyConfig&gt;(options =&gt;
/// {
///     options.ValidateDataAnnotations = true;
/// });
/// </code>
/// </example>
public sealed class PluginConfigOptions
{
    /// <summary>
    /// Whether to enable <see cref="System.ComponentModel.DataAnnotations"/> validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>false</c> (default), no validation is applied. Configuration values
    /// are bound as-is from project.json and environment variables.
    /// Plugins should validate in their commands when config is actually needed.
    /// </para>
    /// <para>
    /// When <c>true</c>, validation runs on every <c>IOptionsMonitor&lt;T&gt;</c> change
    /// notification (including config file reloads). Only enable this when ALL
    /// properties have safe defaults — otherwise, an unconfigured plugin will crash
    /// the application when any config file is saved.
    /// </para>
    /// </remarks>
    public bool ValidateDataAnnotations { get; set; }

    /// <summary>
    /// Whether to validate configuration at application startup.
    /// </summary>
    /// <remarks>
    /// Only effective when <see cref="ValidateDataAnnotations"/> is also <c>true</c>.
    /// When enabled, validation runs immediately after the host is built.
    /// Only use when the plugin configuration is always expected to be fully configured.
    /// </remarks>
    public bool ValidateOnStart { get; set; }
}
