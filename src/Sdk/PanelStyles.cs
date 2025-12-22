using Spectre.Console;

namespace Spectara.Revela.Sdk;

/// <summary>
/// Consistent panel styling for CLI output.
/// </summary>
public static class PanelStyles
{
    /// <summary>
    /// Applies standard styling to a panel.
    /// </summary>
    /// <param name="panel">The panel to style.</param>
    /// <returns>The styled panel for chaining.</returns>
    public static Panel WithStandardStyle(this Panel panel)
    {
        panel.Border = BoxBorder.Rounded;
        panel.BorderColor(Color.Cyan);
        return panel;
    }

    /// <summary>
    /// Applies success styling to a panel (green border).
    /// </summary>
    /// <param name="panel">The panel to style.</param>
    /// <returns>The styled panel for chaining.</returns>
    public static Panel WithSuccessStyle(this Panel panel)
    {
        panel.Border = BoxBorder.Rounded;
        panel.BorderColor(Color.Green);
        return panel;
    }

    /// <summary>
    /// Applies error styling to a panel (red border).
    /// </summary>
    /// <param name="panel">The panel to style.</param>
    /// <returns>The styled panel for chaining.</returns>
    public static Panel WithErrorStyle(this Panel panel)
    {
        panel.Border = BoxBorder.Rounded;
        panel.BorderColor(Color.Red);
        return panel;
    }

    /// <summary>
    /// Applies warning styling to a panel (yellow border).
    /// </summary>
    /// <param name="panel">The panel to style.</param>
    /// <returns>The styled panel for chaining.</returns>
    public static Panel WithWarningStyle(this Panel panel)
    {
        panel.Border = BoxBorder.Rounded;
        panel.BorderColor(Color.Yellow);
        return panel;
    }

    /// <summary>
    /// Applies info styling to a panel (cyan border).
    /// </summary>
    /// <param name="panel">The panel to style.</param>
    /// <returns>The styled panel for chaining.</returns>
    public static Panel WithInfoStyle(this Panel panel)
    {
        panel.Border = BoxBorder.Rounded;
        panel.BorderColor(Color.Cyan);
        return panel;
    }

    /// <summary>
    /// Sets the header with standard left alignment.
    /// </summary>
    /// <param name="panel">The panel to configure.</param>
    /// <param name="header">The header text (can include markup).</param>
    /// <returns>The panel for chaining.</returns>
    public static Panel WithHeader(this Panel panel, string header)
    {
        panel.Header = new PanelHeader(header, Justify.Left);
        return panel;
    }
}
