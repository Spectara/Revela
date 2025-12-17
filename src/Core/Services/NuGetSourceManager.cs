using System.Text.Json;
using Spectara.Revela.Core.Models;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Manages NuGet package sources configuration
/// </summary>
/// <remarks>
/// Sources are stored in %APPDATA%/Revela/nuget-sources.json.
/// Built-in source (nuget.org) is always available.
/// </remarks>
public sealed class NuGetSourceManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the path to the nuget-sources.json config file
    /// </summary>
    public static string ConfigFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var revelaDir = Path.Combine(appData, "Revela");
            _ = Directory.CreateDirectory(revelaDir);
            return Path.Combine(revelaDir, "nuget-sources.json");
        }
    }

    /// <summary>
    /// Gets the default NuGet.org source
    /// </summary>
    public static NuGetSource DefaultSource => new()
    {
        Name = "nuget.org",
        Url = "https://api.nuget.org/v3/index.json",
        Enabled = true
    };

    /// <summary>
    /// Loads all configured sources (including built-in nuget.org)
    /// </summary>
    public static async Task<List<NuGetSource>> LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = new List<NuGetSource> { DefaultSource };

        var configPath = ConfigFilePath;
        if (!File.Exists(configPath))
        {
            return sources;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            var config = JsonSerializer.Deserialize<NuGetSourceConfig>(json);
            if (config?.Sources is not null)
            {
                sources.AddRange(config.Sources.Where(s => s.Enabled));
            }
        }
        catch
        {
            // Ignore malformed config, use defaults
        }

        return sources;
    }

    /// <summary>
    /// Adds a new NuGet source
    /// </summary>
#pragma warning disable CA1054 // URI parameters should not be strings - string required for user input
    public static async Task AddSourceAsync(string name, string url, CancellationToken cancellationToken = default)
#pragma warning restore CA1054
    {
        var config = await LoadConfigAsync(cancellationToken);

        // Check for duplicate name
        if (config.Sources.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Source '{name}' already exists");
        }

        // Check if nuget.org (reserved name)
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot add source with reserved name 'nuget.org'");
        }

        config.Sources.Add(new NuGetSource
        {
            Name = name,
            Url = url,
            Enabled = true
        });

        await SaveConfigAsync(config, cancellationToken);
    }

    /// <summary>
    /// Removes a NuGet source
    /// </summary>
    public static async Task<bool> RemoveSourceAsync(string name, CancellationToken cancellationToken = default)
    {
        if (name.Equals("nuget.org", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove built-in source 'nuget.org'");
        }

        var config = await LoadConfigAsync(cancellationToken);
        var removed = config.Sources.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            await SaveConfigAsync(config, cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all sources including built-in (even disabled ones)
    /// </summary>
    public static async Task<List<NuGetSource>> GetAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigAsync(cancellationToken);
        var sources = new List<NuGetSource> { DefaultSource };
        sources.AddRange(config.Sources);
        return sources;
    }

    private static async Task<NuGetSourceConfig> LoadConfigAsync(CancellationToken cancellationToken)
    {
        var configPath = ConfigFilePath;
        if (!File.Exists(configPath))
        {
            return new NuGetSourceConfig();
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            return JsonSerializer.Deserialize<NuGetSourceConfig>(json) ?? new NuGetSourceConfig();
        }
        catch
        {
            return new NuGetSourceConfig();
        }
    }

    private static async Task SaveConfigAsync(NuGetSourceConfig config, CancellationToken cancellationToken)
    {
        var configPath = ConfigFilePath;
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, cancellationToken);
    }
}
