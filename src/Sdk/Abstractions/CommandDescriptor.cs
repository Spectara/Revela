using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Describes a command with its optional parent command, display order, and menu group.
/// Used by plugins to register commands at different locations in the command tree.
/// </summary>
/// <param name="Command">The command to register.</param>
/// <param name="ParentCommand">
/// Optional parent command path (e.g., "source", "generate", "clean", "config",
/// or a multi-level path such as "info plugins"). Missing parents are created
/// automatically. If null or empty, the command is registered directly under root.
/// </param>
/// <param name="Order">
/// Sort order within the parent. Lower values appear first; commands with the
/// same order are sorted alphabetically by name. Default is 50.
/// For menu entries, the host uses small values (0–50). For pipeline steps
/// (<paramref name="IsSequentialStep"/> = true), this is also the execution
/// order — see <see cref="PipelineOrder"/> and <see cref="CleanPipelineOrder"/>
/// for the host slots (100–400) and choose a value relative to them.
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
/// Commands that read or write <c>project.json</c> (including plugin
/// <c>config</c> commands) should keep the default.
/// </param>
/// <param name="HideWhenProjectExists">
/// Whether to hide the command when a project already exists.
/// Useful for one-time setup commands that shouldn't be shown
/// after initial project setup. Default is false.
/// </param>
/// <param name="IsSequentialStep">
/// Whether this command is a sequential step in a pipeline.
/// Used by the interactive menu to display pipeline step markers (●)
/// and by the "all" command to discover steps to run in Order sequence.
/// Default is false.
/// </param>
/// <param name="InlineInMenu">
/// When true, the interactive menu does NOT render this command as a single
/// entry. Instead it renders the command's default action as a virtual entry
/// (labeled by <see cref="InlineDefaultActionLabel"/>) followed by each of
/// the command's visible subcommands directly under the group label. The CLI
/// surface is unchanged. Only meaningful when the command has subcommands and
/// is registered directly under root (with a <see cref="Group"/>).
/// Default is false.
/// </param>
/// <param name="InlineDefaultActionLabel">
/// Display label for the virtual default-action entry generated when
/// <see cref="InlineInMenu"/> is true. Required when <see cref="InlineInMenu"/>
/// is true; ignored otherwise.
/// </param>
/// <example>
/// <code>
/// // Register under "source": revela source onedrive
/// new CommandDescriptor(oneDriveCommand, ParentCommand: "source", Order: 20)
///
/// // Register at root level with custom order and group
/// new CommandDescriptor(serveCommand, Order: 15, Group: "Build")
///
/// // Plugin config command: writes project.json, so it requires a project
/// new CommandDescriptor(configCmd, ParentCommand: "config", Group: "Addons")
///
/// // One-time setup command: show without project, hide when project exists
/// new CommandDescriptor(setupCmd, Order: 5, Group: "Setup",
///     RequiresProject: false, HideWhenProjectExists: true)
///
/// // Sequential step: runs between scan and pages in "generate all"
/// new CommandDescriptor(myStepCmd, ParentCommand: "generate",
///     Order: PipelineOrder.Scan + 50, IsSequentialStep: true)
///
/// // Inline a parent command flat under its group (e.g. info → Revela / Plugins → / Themes →)
/// new CommandDescriptor(infoCmd, Order: 10, Group: "Info",
///     RequiresProject: false,
///     InlineInMenu: true, InlineDefaultActionLabel: "Revela")
/// </code>
/// </example>
public sealed record CommandDescriptor(
    Command Command,
    string? ParentCommand = null,
    int Order = 50,
    string? Group = null,
    bool RequiresProject = true,
    bool HideWhenProjectExists = false,
    bool IsSequentialStep = false,
    bool InlineInMenu = false,
    string? InlineDefaultActionLabel = null);
