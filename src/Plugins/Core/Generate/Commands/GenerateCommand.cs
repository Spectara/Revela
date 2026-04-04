using System.CommandLine;

namespace Spectara.Revela.Plugins.Core.Generate.Commands;

/// <summary>
/// Command to generate static site from content.
/// </summary>
/// <remarks>
/// <para>
/// Parent command for site generation. Subcommands are registered by the plugin
/// via CommandDescriptor with ParentCommand: "generate".
/// </para>
/// <para>
/// To clean output/cache before generating, use: revela clean all
/// </para>
/// </remarks>
internal sealed class GenerateCommand
{
    /// <summary>
    /// Creates the CLI command (empty parent — subcommands added by plugin registration)
    /// </summary>
    public static Command Create() => new("generate", "Generate static site from content");
}
