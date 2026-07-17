using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// Creates a configured <see cref="IServiceProvider"/> for integration testing.
/// </summary>
/// <remarks>
/// <para>
/// Builds a real DI container with Revela configuration loaded from the
/// specified project directory. Services can be registered via the configure
/// callback, allowing tests to add exactly the services they need.
/// </para>
/// <para>
/// Usage:
/// </para>
/// <code>
/// using var project = TestProject.Create(p => p.AddGallery("Photos", g => g.AddImages(3)));
/// using var host = RevelaTestHost.Build(project.RootPath, services =>
/// {
///     services.AddSingleton&lt;IContentService, ContentService&gt;();
/// });
///
/// var contentService = host.Services.GetRequiredService&lt;IContentService&gt;();
/// var result = await contentService.ScanAsync(project.SourcePath);
/// </code>
/// </remarks>
public static class RevelaTestHost
{
    /// <summary>
    /// Builds a host with Revela configuration using the specified project directory.
    /// </summary>
    /// <param name="projectPath">Path to the project directory (must contain project.json).</param>
    /// <param name="configure">Service registration callback for adding required services.</param>
    /// <returns>A built host with configured services. Dispose when done.</returns>
    public static IHost Build(string projectPath, Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = projectPath,
            EnvironmentName = "Testing",
        });

        // Load project.json from the test project directory
        builder.Configuration.AddJsonFile(
            Path.Combine(projectPath, "project.json"),
            optional: true,
            reloadOnChange: false);

        // Load site.json (re-keyed under the "site" section for SiteCoreConfig)
        builder.Configuration.AddSiteJson(
            Path.Combine(projectPath, "site.json"),
            optional: true,
            reloadOnChange: false);

        // Register all config sections (IOptions<T>)
        builder.Services.AddRevelaConfigSections();
        builder.Services.AddCoreServices();

        // Default IBuildInfo for tests — overridable via configure callback.
        builder.Services.TryAddSingleton<IBuildInfo>(new TestBuildInfo());

        // Register ProjectEnvironment
        builder.Services.AddOptions<ProjectEnvironment>()
            .Configure<IHostEnvironment>((env, host) => env.Path = host.ContentRootPath);

        // Let tests register the services they need
        configure?.Invoke(builder.Services);

        return builder.Build();
    }

    private sealed class TestBuildInfo : IBuildInfo
    {
        public HostKind Kind => HostKind.Standalone;
        public string Version => "0.0.0-test";
        public string InformationalVersion => "0.0.0-test";
        public string Framework => ".NET 10.0";
        public string Configuration => "Debug";
        public string RuntimeIdentifier => "test";
        public string FormatVersionLine() => "revela 0.0.0-test (test)";
    }
}
