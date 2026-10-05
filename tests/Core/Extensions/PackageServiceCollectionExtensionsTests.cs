using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Core.Extensions;

[TestClass]
[TestCategory("Unit")]
public sealed class PackageServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddPackages_PluginConfigureServicesThrows_SkipsPluginAndDiscardsItsRegistrations()
    {
        var healthy = new HealthyPlugin();
        var broken = new BrokenPlugin(_ => { });
        var services = CreateServices();

        using var loggerFactory = new RecordingLoggerFactory();
        services.AddPackages(new FixedPackageSource(healthy, broken), ["generate"], loggerFactory);

        using var provider = services.BuildServiceProvider();
        var plugin = Assert.ContainsSingle(provider.GetServices<IPlugin>());
        Assert.AreSame(healthy, plugin);
        Assert.AreSame(healthy, Assert.ContainsSingle(provider.GetRequiredService<IPackageContext>().Plugins).Plugin);
        Assert.IsNotNull(provider.GetService<HealthyService>());
        Assert.IsFalse(services.Any(d => d.ServiceType == typeof(BrokenService)));
    }

    [TestMethod]
    public void AddPackages_PluginThrowsAfterReplacingHostService_RestoresHostRegistration()
    {
        var broken = new BrokenPlugin(services => services.Replace(ServiceDescriptor.Singleton<IMarker, PluginMarker>()));
        var services = CreateServices();
        services.AddSingleton<IMarker, HostMarker>();

        using var loggerFactory = new RecordingLoggerFactory();
        services.AddPackages(new FixedPackageSource(broken), ["generate"], loggerFactory);

        var marker = Assert.ContainsSingle(services.Where(d => d.ServiceType == typeof(IMarker)));
        Assert.AreEqual(typeof(HostMarker), marker.ImplementationType);
    }

    [TestMethod]
    public void AddPackages_PluginConfigureServicesThrows_LogsOneWarningNamingThePlugin()
    {
        var broken = new BrokenPlugin(_ => { });
        var services = CreateServices();

        using var loggerFactory = new RecordingLoggerFactory();
        services.AddPackages(new FixedPackageSource(new HealthyPlugin(), broken), ["generate"], loggerFactory);

        var visible = loggerFactory.Entries.Where(e => e.Level >= LogLevel.Warning).ToList();
        var warning = Assert.ContainsSingle(visible);
        Assert.AreEqual(LogLevel.Warning, warning.Level);
        Assert.Contains(BrokenPlugin.DisplayName, warning.Message, StringComparison.Ordinal);
        Assert.Contains(BrokenPlugin.FailureReason, warning.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void AddPackages_PluginTryAddsServiceTheHostRegistered_KeepsSingleHostRegistration()
    {
        var plugin = new HealthyPlugin(services => services.TryAddSingleton<IMarker, PluginMarker>());
        var services = CreateServices();
        services.AddSingleton<IMarker, HostMarker>();

        using var loggerFactory = new RecordingLoggerFactory();
        services.AddPackages(new FixedPackageSource(plugin), ["generate"], loggerFactory);

        var marker = Assert.ContainsSingle(services.Where(d => d.ServiceType == typeof(IMarker)));
        Assert.AreEqual(typeof(HostMarker), marker.ImplementationType);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private interface IMarker;

    private sealed class HostMarker : IMarker;

    private sealed class PluginMarker : IMarker;

    private sealed class HealthyService;

    private sealed class BrokenService;

    private sealed class HealthyPlugin(Action<IServiceCollection>? configure = null) : IPlugin
    {
        public PackageMetadata Metadata { get; } = new()
        {
            Id = "Tests.Healthy",
            Name = "Healthy Plugin",
            Version = "1.0.0",
            Description = "Test fixture",
        };

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<HealthyService>();
            configure?.Invoke(services);
        }
    }

    private sealed class BrokenPlugin(Action<IServiceCollection> beforeFailing) : IPlugin
    {
        public const string DisplayName = "Broken Plugin";
        public const string FailureReason = "missing native dependency";

        public PackageMetadata Metadata { get; } = new()
        {
            Id = "Tests.Broken",
            Name = DisplayName,
            Version = "1.0.0",
            Description = "Test fixture",
        };

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<BrokenService>();
            beforeFailing(services);
            throw new InvalidOperationException(FailureReason);
        }
    }

    private sealed class FixedPackageSource(params IPlugin[] plugins) : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() =>
            [.. plugins.Select(p => new LoadedPluginInfo(p, PackageSource.Local))];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        private readonly List<LogEntry> entries = [];
        private readonly Lock entriesLock = new();

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (entriesLock)
                {
                    return [.. entries];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private void Add(LogEntry entry)
        {
            lock (entriesLock)
            {
                entries.Add(entry);
            }
        }

        private sealed class RecordingLogger(RecordingLoggerFactory owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }
}
