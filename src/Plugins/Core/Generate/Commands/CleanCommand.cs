using System.CommandLine;

namespace Spectara.Revela.Plugins.Core.Generate.Commands;

/// <summary>
/// Parent command for clean operations.
/// </summary>
/// <remarks>
/// <para>
/// Parent command for clean operations. Subcommands are registered by the plugin
/// via CommandDescriptor with ParentCommand: "clean".
/// </para>
/// <para>
/// Safety: NEVER deletes source files (source/, config/, *.json configs).
/// </para>
/// </remarks>
internal sealed class CleanCommand
{
    /// <summary>
    /// Creates the CLI command (empty parent — subcommands added by plugin registration)
    /// </summary>
    public static Command Create() => new("clean", "Clean generated files");
}
