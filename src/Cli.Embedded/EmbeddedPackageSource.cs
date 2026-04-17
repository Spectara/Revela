using Spectara.Revela.Plugins.Calendar;
using Spectara.Revela.Plugins.Compress;
using Spectara.Revela.Plugins.Serve;
using Spectara.Revela.Plugins.Source.Calendar;
using Spectara.Revela.Plugins.Source.OneDrive;
using Spectara.Revela.Plugins.Statistics;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Themes.Lumina;
using Spectara.Revela.Themes.Lumina.Calendar;
using Spectara.Revela.Themes.Lumina.Statistics;

namespace Spectara.Revela.Cli.Embedded;

/// <summary>
/// Package source with all plugins and themes statically linked.
/// </summary>
/// <remarks>
/// No reflection, no disk scanning, no Assembly.LoadFrom().
/// All types are known at compile time — AOT and trimming compatible.
/// </remarks>
internal sealed class EmbeddedPackageSource : IPackageSource
{
    /// <inheritdoc />
    public IReadOnlyList<LoadedPluginInfo> LoadPlugins() =>
    [
        new(new CompressPlugin(), PackageSource.Bundled),
        new(new ServePlugin(), PackageSource.Bundled),
        new(new OneDrivePlugin(), PackageSource.Bundled),
        new(new StatisticsPlugin(), PackageSource.Bundled),
        new(new CalendarPlugin(), PackageSource.Bundled),
        new(new SourceCalendarPlugin(), PackageSource.Bundled),
    ];

    /// <inheritdoc />
    public IReadOnlyList<LoadedThemeInfo> LoadThemes() =>
    [
        new(new LuminaTheme(), PackageSource.Bundled),
        new(new LuminaStatisticsExtension(), PackageSource.Bundled),
        new(new LuminaCalendarExtension(), PackageSource.Bundled),
    ];
}
