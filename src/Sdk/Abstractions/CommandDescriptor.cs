using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Describes a command with its optional parent command, display order, and menu group.
/// Used by plugins to register commands at different locations in the command tree.
/// </summary>
/// <param name="Command">The command to register.</param>
/// <param name="ParentCommand">
/// Optional parent command name (e.g., "init", "source", "deploy").
/// If null or empty, the command is registered directly under root.
/// </param>
/// <param name="Order">
/// Display order for interactive menu (1-100). Lower values appear first.
/// Commands with the same order are sorted alphabetically by name.
/// Default is 50, giving plugins room to insert before or after.
/// </param>
/// <param name="Group">
/// Optional group name for visual organization in the interactive menu.
/// Well-known groups: "Build", "Content", "Setup", "Addons".
/// Unknown group names are created automatically with default order.
/// If null, the command appears in an ungrouped section at the end.
/// </param>
/// <param name="RequiresProject">
/// Whether the command requires an active project context (project.json).
/// When true (default), the command is only shown in the interactive menu
/// when a project is loaded. When false, the command is always available.
/// Examples: generate/clean require project, config project/theme install don't.
/// </param>
/// <param name="HideWhenProjectExists">
/// Whether to hide the command when a project already exists.
/// Useful for one-time setup commands like "init" that shouldn't be shown
/// after initial project setup. Default is false.
/// </param>
/// <example>
/// <code>
/// // Register under "init" parent: revela init onedrive
/// new CommandDescriptor(initCommand, "init")
///
/// // Register at root level with custom order and group
/// new CommandDescriptor(onedriveCommand, null, Order: 10, Group: "Content")
///
/// // Command that doesn't require a project (setup commands)
/// new CommandDescriptor(configProjectCmd, "config", RequiresProject: false)
///
/// // Init command: show without project, hide when project exists
/// new CommandDescriptor(initCmd, null, Order: 5, Group: "Setup",
///     RequiresProject: false, HideWhenProjectExists: true)
/// </code>
/// </example>
public sealed record CommandDescriptor(
    Command Command,
    string? ParentCommand = null,
    int Order = 50,
    string? Group = null,
    bool RequiresProject = true,
    bool HideWhenProjectExists = false);
