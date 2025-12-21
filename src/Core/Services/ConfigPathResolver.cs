namespace Spectara.Revela.Core.Services;

/// <summary>
/// Resolves configuration and plugin paths based on installation type
/// </summary>
/// <remarks>
/// <para>
/// Determines the appropriate directory for configuration files and plugins:
/// </para>
/// <list type="bullet">
/// <item><b>Portable/ZIP:</b> Directory containing revela.exe (writable)</item>
/// <item><b>dotnet tool / Program Files:</b> %APPDATA%/Revela (exe dir not writable)</item>
/// </list>
/// <para>
/// The decision is made once at startup by testing write access to the exe directory.
/// </para>
/// </remarks>
public static class ConfigPathResolver
{
    private static readonly Lazy<string> LazyConfigDirectory = new(DetermineConfigDirectory);
    private static readonly Lazy<bool> LazyIsPortable = new(DetermineIsPortable);

    /// <summary>
    /// Gets the directory where revela.exe is located
    /// </summary>
    public static string ExeDirectory => AppContext.BaseDirectory;

    /// <summary>
    /// Gets the global AppData directory for Revela
    /// </summary>
    public static string AppDataDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Revela");
        }
    }

    /// <summary>
    /// Gets the directory for configuration files (revela.json)
    /// </summary>
    /// <remarks>
    /// Portable installation: exe directory.
    /// dotnet tool / Program Files: AppData directory.
    /// </remarks>
    public static string ConfigDirectory => LazyConfigDirectory.Value;

    /// <summary>
    /// Gets the directory for plugins
    /// </summary>
    /// <remarks>
    /// Portable installation: exe-dir/plugins.
    /// dotnet tool / Program Files: AppData/plugins.
    /// </remarks>
    public static string LocalPluginDirectory => Path.Combine(ConfigDirectory, "plugins");

    /// <summary>
    /// Gets the global plugin directory (always in AppData)
    /// </summary>
    public static string GlobalPluginDirectory => Path.Combine(AppDataDirectory, "plugins");

    /// <summary>
    /// Gets whether this is a portable installation (config stored next to exe)
    /// </summary>
    public static bool IsPortableInstallation => LazyIsPortable.Value;

    /// <summary>
    /// Gets the full path to the global config file (revela.json)
    /// </summary>
    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "revela.json");

    private static string DetermineConfigDirectory()
    {
        // Try to write to exe directory
        if (IsDirectoryWritable(ExeDirectory))
        {
            return ExeDirectory;
        }

        // Fall back to AppData
        var appDataDir = AppDataDirectory;
        _ = Directory.CreateDirectory(appDataDir);
        return appDataDir;
    }

    private static bool DetermineIsPortable()
    {
        return IsDirectoryWritable(ExeDirectory);
    }

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            // Create and immediately delete a test file
            var testFile = Path.Combine(path, $".revela-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, string.Empty);
            File.Delete(testFile);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
