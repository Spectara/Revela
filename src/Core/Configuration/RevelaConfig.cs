namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Root configuration for a Revela project
/// </summary>
public sealed class RevelaConfig
{
    public ProjectSettings Project { get; init; } = new();
    public SiteSettings Site { get; init; } = new();
    public ThemeSettings Theme { get; init; } = new();
    public BuildSettings Build { get; init; } = new();
    public IReadOnlyList<NavigationItem> Navigation { get; init; } = [];
}

public sealed class ProjectSettings
{
    public string Name { get; init; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration value from JSON")]
    public string BaseUrl { get; init; } = string.Empty;

    public string Language { get; init; } = "en";
}

public sealed class SiteSettings
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? Copyright { get; init; }
}

public sealed class ThemeSettings
{
    public string Name { get; init; } = "default";
}

public sealed class BuildSettings
{
    public string Output { get; init; } = "output";
    public ImageSettings Images { get; init; } = new();
    public CacheSettings Cache { get; init; } = new();
}

public sealed class ImageSettings
{
    public int Quality { get; init; } = 90;
    public IReadOnlyList<string> Formats { get; init; } = ["webp", "jpg"];
    public IReadOnlyList<int> Sizes { get; init; } = [640, 1024, 1280, 1920, 2560];
}

public sealed class CacheSettings
{
    public bool Enabled { get; init; } = true;
    public bool Exif { get; init; } = true;
    public bool Html { get; init; } = true;
    public string Directory { get; init; } = ".revela/cache";
}

public sealed class NavigationItem
{
    public required string Name { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration value from JSON")]
    public string? Url { get; init; }

    public string? Path { get; init; }
    public IReadOnlyList<NavigationItem>? Children { get; init; }
}

