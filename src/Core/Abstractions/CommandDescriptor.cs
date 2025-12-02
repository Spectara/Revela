using System.CommandLine;

namespace Spectara.Revela.Core.Abstractions;

/// <summary>
/// Describes a command with its optional parent command.
/// Used by plugins to register commands at different locations in the command tree.
/// </summary>
/// <param name="Command">The command to register.</param>
/// <param name="ParentCommand">
/// Optional parent command name (e.g., "init", "source", "deploy").
/// If null or empty, the command is registered directly under root.
/// </param>
/// <example>
/// <code>
/// // Register under "init" parent: revela init onedrive
/// new CommandDescriptor(initCommand, "init")
///
/// // Register at root level: revela onedrive sync
/// new CommandDescriptor(onedriveCommand, null)
/// </code>
/// </example>
public sealed record CommandDescriptor(
    Command Command,
    string? ParentCommand = null);
