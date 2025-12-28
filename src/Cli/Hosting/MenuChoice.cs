using System.CommandLine;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Represents a menu choice in the interactive CLI mode.
/// </summary>
/// <param name="DisplayName">The text shown in the menu.</param>
/// <param name="Command">The associated command, if any.</param>
/// <param name="Action">The action type for this choice.</param>
internal sealed record MenuChoice(
    string DisplayName,
    Command? Command = null,
    MenuAction Action = MenuAction.Navigate)
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
    /// Creates a "Setup Wizard" menu choice.
    /// </summary>
    public static MenuChoice SetupWizard => new("🔧 Setup Wizard  [dim]Configure themes and plugins[/]", Action: MenuAction.RunSetupWizard);

    /// <summary>
    /// Creates a menu choice from a command.
    /// </summary>
    /// <param name="cmd">The command to create a choice for.</param>
    /// <returns>A menu choice with the command's name and description.</returns>
    public static MenuChoice FromCommand(Command cmd)
    {
        var hasSubcommands = cmd.Subcommands.Count > 0;
        var arrow = hasSubcommands ? " →" : "";

        var description = string.IsNullOrWhiteSpace(cmd.Description)
            ? string.Empty
            : $"  [dim]{cmd.Description}[/]";

        var action = hasSubcommands
            ? MenuAction.Navigate
            : MenuAction.Execute;

        return new MenuChoice($"{cmd.Name}{arrow}{description}", cmd, action);
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
