namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Classifies command paths that change installed package files on disk.
/// </summary>
/// <remarks>
/// <para>
/// Single source of truth for two decisions that must agree:
/// </para>
/// <list type="bullet">
/// <item>Startup skips loading packages for <see cref="ModifiesPackageFiles"/> commands, so the
/// files they replace or delete are not locked by the running process.</item>
/// <item>The interactive menu runs with packages loaded, so it refuses
/// <see cref="ModifiesPackageFiles"/> commands, which would replace or delete loaded assemblies.</item>
/// </list>
/// </remarks>
public static class PackageManagementCommands
{
    /// <summary>
    /// Returns whether the command path is <c>plugin|theme install|uninstall</c>.
    /// </summary>
    /// <param name="commandPath">CLI arguments or menu command path, starting at the root command.</param>
    public static bool ModifiesPackageFiles(IReadOnlyList<string> commandPath) =>
        IsPackageCommand(commandPath)
        && (IsSubcommand(commandPath, "install") || IsSubcommand(commandPath, "uninstall"));

    private static bool IsPackageCommand(IReadOnlyList<string> commandPath) =>
        commandPath.Count >= 2
        && (commandPath[0].Equals("plugin", StringComparison.OrdinalIgnoreCase)
            || commandPath[0].Equals("theme", StringComparison.OrdinalIgnoreCase));

    private static bool IsSubcommand(IReadOnlyList<string> commandPath, string name) =>
        commandPath[1].Equals(name, StringComparison.OrdinalIgnoreCase);
}
