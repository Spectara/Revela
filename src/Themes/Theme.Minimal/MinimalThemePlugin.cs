using Spectara.Revela.Core.Themes;

namespace Spectara.Revela.Theme.Minimal;

/// <summary>
/// Minimal theme plugin - clean starting point for custom themes
/// </summary>
/// <remarks>
/// This theme provides:
/// - Basic HTML5 structure
/// - Minimal CSS (~50 lines)
/// - Simple image grid
/// - No JavaScript dependencies
///
/// Perfect for:
/// - Learning how themes work
/// - Starting point for custom themes
/// - Unit testing template rendering
///
/// All configuration is in theme.json (embedded resource).
/// </remarks>
public sealed class MinimalThemePlugin() : EmbeddedThemePlugin(typeof(MinimalThemePlugin).Assembly);

