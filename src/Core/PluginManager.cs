using System.IO.Compression;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectara.Revela.Core.Logging;

namespace Spectara.Revela.Core;

/// <summary>
/// Manages plugin installation, updates and removal via NuGet
/// </summary>
/// <remarks>
/// Uses C# 12 Primary Constructor with optional parameter.
/// Creates plugin directory automatically if it doesn't exist.
/// Supports both NuGet and ZIP installation sources.
/// </remarks>
public sealed class PluginManager(ILogger<PluginManager>? logger = null)
{
    private readonly ILogger<PluginManager> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginManager>.Instance;
    private readonly SourceRepository repository = Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json"));

    /// <summary>
    /// Gets the local plugin directory (next to executable).
    /// </summary>
    public static string LocalPluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugins");

    /// <summary>
    /// Gets the global plugin directory (in user's AppData).
    /// </summary>
    public static string GlobalPluginDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Revela", "plugins");
        }
    }

    public async Task<bool> InstallPluginAsync(string packageId, string? version = null, bool global = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDir = global ? GlobalPluginDirectory : LocalPluginDirectory;
            _ = Directory.CreateDirectory(targetDir);

            logger.InstallingPlugin(packageId);

            var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            using var cacheContext = new SourceCacheContext();
            var versions = await resource.GetAllVersionsAsync(
                packageId,
                cacheContext,
                NuGet.Common.NullLogger.Instance,
                cancellationToken);

            var targetVersion = version is not null
                ? NuGetVersion.Parse(version)
                : versions.MaxBy(v => v);

            if (targetVersion is null)
            {
                logger.PackageNotFound(packageId);
                return false;
            }

            logger.InstallingVersion(packageId, targetVersion.ToString());

            // TODO: Implement actual package download and extraction
            // For now, this is a placeholder

            return true;
        }
        catch (Exception ex)
        {
            logger.InstallFailed(ex, packageId);
            return false;
        }
    }

    /// <summary>
    /// Installs a plugin from a ZIP file (local path or URL).
    /// </summary>
    /// <param name="zipPath">Local file path or HTTP(S) URL to the ZIP file.</param>
    /// <param name="global">If true, installs to global AppData folder; otherwise installs next to executable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if installation succeeded.</returns>
    public async Task<bool> InstallFromZipAsync(string zipPath, bool global = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDir = global ? GlobalPluginDirectory : LocalPluginDirectory;
            _ = Directory.CreateDirectory(targetDir);

            logger.InstallingFromZip(zipPath, targetDir);

            // Handle URL or local path
            if (zipPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                zipPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return await InstallFromUrlAsync(new Uri(zipPath), targetDir, cancellationToken);
            }
            else
            {
                return await InstallFromLocalZipAsync(zipPath, targetDir, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.InstallFromZipFailed(ex, zipPath);
            return false;
        }
    }

    private async Task<bool> InstallFromUrlAsync(Uri url, string targetDir, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        await using var stream = await httpClient.GetStreamAsync(url, cancellationToken);

        // Download to temp file first
        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fileStream = File.Create(tempFile))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            return await InstallFromLocalZipAsync(tempFile, targetDir, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<bool> InstallFromLocalZipAsync(string zipPath, string targetDir, CancellationToken cancellationToken)
    {
        if (!File.Exists(zipPath))
        {
            logger.ZipFileNotFound(zipPath);
            return false;
        }

        // Extract files preserving directory structure
        // ZIP structure: Plugin.dll, Plugin.deps.json, Plugin/*.dll (dependencies)
        using var archive = await Task.Run(() => ZipFile.OpenRead(zipPath), cancellationToken);
        var fileCount = 0;

        foreach (var entry in archive.Entries)
        {
            // Skip directories (they end with /)
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            // Only extract DLL and deps.json files
            if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                !entry.Name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Preserve directory structure from ZIP
            var destPath = Path.Combine(targetDir, entry.FullName);
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                _ = Directory.CreateDirectory(destDir);
            }

            await Task.Run(() => entry.ExtractToFile(destPath, overwrite: true), cancellationToken);
            logger.ExtractedFile(entry.FullName, targetDir);
            fileCount++;
        }

        if (fileCount == 0)
        {
            logger.NoDllsInZip(zipPath);
            return false;
        }

        logger.PluginInstalledFromZip(fileCount, targetDir);
        return true;
    }

    public async Task<bool> UpdatePluginAsync(string packageId, bool global = false, CancellationToken cancellationToken = default)
    {
        logger.UpdatingPlugin(packageId);
        // Uninstall old version, install new version
        _ = await UninstallPluginAsync(packageId, global, cancellationToken);
        return await InstallPluginAsync(packageId, null, global, cancellationToken);
    }

    public Task<bool> UninstallPluginAsync(string packageId, bool global = false, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        try
        {
            logger.UninstallingPlugin(packageId);

            var pluginDir = global ? GlobalPluginDirectory : LocalPluginDirectory;
            var pluginPath = Path.Combine(pluginDir, packageId);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, recursive: true);
                logger.PluginUninstalled(packageId);
                return Task.FromResult(true);
            }

            // Also check for single DLL file
            var dllPath = Path.Combine(pluginDir, $"{packageId}.dll");
            if (File.Exists(dllPath))
            {
                File.Delete(dllPath);
                logger.PluginUninstalled(packageId);
                return Task.FromResult(true);
            }

            logger.PluginNotFound(packageId);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            logger.UninstallFailed(ex, packageId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Lists all installed plugins from both local and global directories.
    /// </summary>
    /// <remarks>
    /// Lists all DLLs in the root of plugin directories.
    /// Dependencies should be in subfolders (named after the plugin DLL).
    /// Convention: plugins/MyPlugin.dll + plugins/MyPlugin/*.dll (deps)
    /// </remarks>
    public static IEnumerable<(string Name, string Location)> ListInstalledPlugins()
    {
        var results = new List<(string Name, string Location)>();

        // Check local plugins (only root DLLs, not in subfolders)
        if (Directory.Exists(LocalPluginDirectory))
        {
            foreach (var dll in Directory.GetFiles(LocalPluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                results.Add((Path.GetFileNameWithoutExtension(dll), "local"));
            }
        }

        // Check global plugins (only root DLLs, not in subfolders)
        if (Directory.Exists(GlobalPluginDirectory))
        {
            foreach (var dll in Directory.GetFiles(GlobalPluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                results.Add((Path.GetFileNameWithoutExtension(dll), "global"));
            }
        }

        return results;
    }
}

