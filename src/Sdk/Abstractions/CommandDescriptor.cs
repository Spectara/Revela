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
/// Well-known groups: "Setup", "Content", "Build", "Customize".
/// Unknown group names are created automatically with default order.
/// If null, the command appears in an ungrouped section at the end.
/// </param>
/// <example>
/// <code>
/// // Register under "init" parent: revela init onedrive
/// new CommandDescriptor(initCommand, "init")
///
/// // Register at root level with custom order and group
/// new CommandDescriptor(onedriveCommand, null, Order: 10, Group: "Content")
/// </code>
/// </example>
public sealed record CommandDescriptor(
    Command Command,
    string? ParentCommand = null,
    int Order = 50,
    string? Group = null);
