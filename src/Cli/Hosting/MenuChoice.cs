using System.CommandLine;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Represents a menu choice in the interactive CLI mode.
/// </summary>
/// <param name="DisplayName">The text shown in the menu.</param>
/// <param name="Command">The associated command, if any.</param>
/// <param name="Action">The action type for this choice.</param>
/// <param name="CommandPathOverride">
/// When set, replaces the default extended path (current path + selected
/// command name) with this absolute path. Used by inlined parent rendering
/// where a top-level menu entry actually points at a nested subcommand.
/// </param>
internal sealed record MenuChoice(
    string DisplayName,
    Command? Command = null,
    MenuAction Action = MenuAction.Navigate,
    IReadOnlyList<string>? CommandPathOverride = null)
{
    /// <summary>
    /// Creates a "Back" menu choice for navigation.
    /// </summary>
    public static MenuChoice Back => new("← Back", Action: MenuAction.Back);

    /// <summary>
    /// Creates an "Exit" menu choice.
    /// </summary>
    public static MenuChoice Exit => new("Exit", Action: MenuAction.Exit);

    /// <summary>
    /// Creates a "wizard" menu choice for the Addons group.
    /// </summary>
    /// <param name="nameColumnWidth">Width to pad the name column to for alignment.</param>
    public static MenuChoice CreateWizard(int nameColumnWidth = 0)
    {
        var paddedName = nameColumnWidth > 0 ? "wizard".PadRight(nameColumnWidth) : "wizard";
        return new($"{paddedName}   [dim]Install themes and plugins[/]", Action: MenuAction.RunSetupWizard);
    }

    /// <summary>
    /// Creates the virtual default-action menu entry for an inlined parent.
    /// </summary>
    /// <param name="parent">The inlined parent command.</param>
    /// <param name="label">Display label (e.g. "Revela").</param>
    /// <param name="nameColumnWidth">Width to pad the name column to for alignment.</param>
    /// <returns>
    /// A choice that, when selected, invokes the parent command with no
    /// subcommand arguments — triggering its default action.
    /// </returns>
    public static MenuChoice CreateInlinedDefaultAction(Command parent, string label, int nameColumnWidth = 0)
    {
        var paddedName = nameColumnWidth > 0 ? label.PadRight(nameColumnWidth) : label;
        var description = string.IsNullOrWhiteSpace(parent.Description)
            ? string.Empty
            : $"   [dim]{parent.Description}[/]";

        // Path override = just the parent name, so CommandExecutor invokes
        // the parent without any subcommand → default action runs.
        return new MenuChoice(
            DisplayName: $"{paddedName}{description}",
            Command: parent,
            Action: MenuAction.Execute,
            CommandPathOverride: [parent.Name]);
    }

    /// <summary>
    /// Creates a menu choice from a command.
    /// </summary>
    /// <param name="cmd">The command to create a choice for.</param>
    /// <param name="isPipelineStep">Whether this command is a pipeline step (shown with marker).</param>
    /// <param name="nameColumnWidth">Width to pad the name column to for alignment.</param>
    /// <param name="commandPathOverride">
    /// Optional absolute path to use when this choice is selected. Used for
    /// inlined subcommands where the menu position differs from the CLI tree
    /// position.
    /// </param>
    /// <returns>A menu choice with the command's name and description.</returns>
    public static MenuChoice FromCommand(
        Command cmd,
        bool isPipelineStep = false,
        int nameColumnWidth = 0,
        IReadOnlyList<string>? commandPathOverride = null)
    {
        var hasVisibleSubcommands = cmd.Subcommands.Any(c => !c.Hidden);
        var arrow = hasVisibleSubcommands ? " →" : "";
        var nameWithArrow = $"{cmd.Name}{arrow}";
        var paddedName = nameColumnWidth > 0 ? nameWithArrow.PadRight(nameColumnWidth) : nameWithArrow;
        var marker = isPipelineStep ? "[dim cyan]●[/]  " : "   ";

        var description = string.IsNullOrWhiteSpace(cmd.Description)
            ? string.Empty
            : $"[dim]{cmd.Description}[/]";

        var action = hasVisibleSubcommands
            ? MenuAction.Navigate
            : MenuAction.Execute;

        return new MenuChoice($"{paddedName}{marker}{description}", cmd, action, commandPathOverride);
    }

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

/// <summary>
/// Defines the action type for a menu choice.
/// </summary>
internal enum MenuAction
{
    /// <summary>Navigate to a submenu.</summary>
    Navigate,

    /// <summary>Execute a command.</summary>
    Execute,

    /// <summary>Go back to the parent menu.</summary>
    Back,

    /// <summary>Exit the interactive mode.</summary>
    Exit,

    /// <summary>Run the setup wizard.</summary>
    RunSetupWizard
}
