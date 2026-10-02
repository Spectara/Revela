using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Generate;

/// <summary>
/// The generic clean commands remove artifacts by kind through their owners: core and plugin
/// artifacts alike, never durable ones.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CleanCommandsTests
{
    [TestMethod]
    [DataRow(true, DisplayName = "CLI command")]
    [DataRow(false, DisplayName = "pipeline step")]
    public async Task CleanCache_RemovesCacheArtifactsAndKeepsOutputImageStateAndDurable(bool viaCommand)
    {
        using var site = new Site();

        var succeeded = viaCommand
            ? await site.Get<CleanCacheCommand>().ExecuteAsync(CancellationToken.None) == 0
            : (await ((IPipelineStep)site.Get<CleanCacheCommand>()).ExecuteAsync(CancellationToken.None)).Success;

        Assert.IsTrue(succeeded);
        Assert.IsFalse(File.Exists(site.Manifest), "The manifest is a cache artifact.");
        Assert.IsFalse(File.Exists(site.PluginCache), "A plugin's cache artifact is cleaned by its owner.");
        Assert.IsTrue(File.Exists(site.ImageState), "The image state describes the output.");
        Assert.IsTrue(File.Exists(site.Page));
        Assert.IsTrue(File.Exists(site.Variant), "Cleaning the cache must not delete output.");
        Assert.IsTrue(File.Exists(site.PluginOutputRecord));
        Assert.IsTrue(File.Exists(site.Durable));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "CLI command")]
    [DataRow(false, DisplayName = "pipeline step")]
    public async Task CleanOutput_RemovesOutputImageStateAndPluginRecordAndKeepsCache(bool viaCommand)
    {
        using var site = new Site();

        var succeeded = viaCommand
            ? await site.Get<CleanOutputCommand>().ExecuteAsync(CancellationToken.None) == 0
            : (await ((IPipelineStep)site.Get<CleanOutputCommand>()).ExecuteAsync(CancellationToken.None)).Success;

        Assert.IsTrue(succeeded);
        Assert.IsFalse(Directory.Exists(site.Project.OutputPath), "The rendered site and processed images are gone.");
        Assert.IsFalse(File.Exists(site.ImageState), "The image state goes with the images.");
        Assert.IsFalse(File.Exists(site.PluginOutputRecord), "A plugin's output record goes with the output.");
        Assert.IsTrue(File.Exists(site.Manifest), "The manifest is a cache artifact.");
        Assert.IsTrue(File.Exists(site.PluginCache));
        Assert.IsTrue(File.Exists(site.Durable));
        Assert.IsLessThan(
            site.Calls.IndexOf(CoreArtifacts.RenderedSite.Value),
            site.Calls.IndexOf("acme.sidecars/files"),
            "The plugin's sidecars depend on the rendered site and go first.");
        Assert.DoesNotContain(CoreArtifacts.Manifest.Value, site.Calls);
    }

    [TestMethod]
    public async Task CleanAll_RemovesCacheAndOutputButKeepsDurableArtifacts()
    {
        using var site = new Site();

        var exitCode = await site.Get<CleanAllCommand>().ExecuteAsync(CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(Directory.Exists(site.Project.OutputPath));
        Assert.IsFalse(File.Exists(site.Manifest));
        Assert.IsFalse(File.Exists(site.ImageState));
        Assert.IsFalse(File.Exists(site.PluginCache));
        Assert.IsFalse(File.Exists(site.PluginOutputRecord));
        Assert.IsTrue(File.Exists(site.Durable), "Durable data is only removed by its owner's own clean command.");
    }

    [TestMethod]
    public async Task OwnersCleanCommand_DurableArtifact_IsRemoved()
    {
        using var site = new Site();

        var result = await site.Get<IArtifactLifecycle>().InvalidateAsync(new ArtifactId("acme.ai/captions"));

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsFalse(File.Exists(site.Durable));
    }

    [TestMethod]
    [DataRow("output")]
    [DataRow("all")]
    public async Task Clean_UnsafeOutputPath_FailsAndDeletesNothing(string command)
    {
        using var site = new Site(output: ".");

        var exitCode = command == "output"
            ? await site.Get<CleanOutputCommand>().ExecuteAsync(CancellationToken.None)
            : await site.Get<CleanAllCommand>().ExecuteAsync(CancellationToken.None);

        Assert.AreEqual(1, exitCode);
        Assert.IsEmpty(site.Calls, "No owner may run when the output path is unsafe.");
        Assert.IsTrue(File.Exists(site.Manifest));
        Assert.IsTrue(File.Exists(site.ImageState));
        Assert.IsTrue(File.Exists(Path.Combine(site.Project.RootPath, "project.json")));
    }

    /// <summary>
    /// A project with core artifacts on disk and three plugin artifacts, one of each kind.
    /// </summary>
    private sealed class Site : IDisposable
    {
        private readonly IHost host;

        public Site(string output = "output")
        {
            Project = TestProject.Create();
            File.WriteAllText(
                Path.Combine(Project.RootPath, "project.json"),
                $$"""{ "paths": { "source": "source", "output": "{{output}}" } }""");
            Directory.CreateDirectory(Project.SourcePath);

            Manifest = Write(".revela", "core", "manifest.json");
            ImageState = Write(".revela", "core", "images.json");
            Page = Write("output", "index.html");
            Variant = Write("output", "images", "photos", "a", "320.jpg");
            PluginCache = Write(".revela", "acme.data", "gallery", "data.json");
            PluginOutputRecord = Write(".revela", "acme.sidecars", "record.json");
            Durable = Write(".revela", "acme.ai", "captions.json");

            host = RevelaTestHost.Build(Project.RootPath, services =>
            {
                services.AddGenerateFeature();
                services.AddSingleton<IArtifactInvalidator>(new FileArtifact("acme.data/pages", ArtifactKind.Cache, [CoreArtifacts.Manifest], PluginCache));
                services.AddSingleton<IArtifactInvalidator>(new FileArtifact("acme.sidecars/files", ArtifactKind.Output, [CoreArtifacts.RenderedSite], PluginOutputRecord));
                services.AddSingleton<IArtifactInvalidator>(new FileArtifact("acme.ai/captions", ArtifactKind.Durable, [], Durable));
                RecordInvalidations(services, Calls);
            });
        }

        public TestProject Project { get; }

        public List<string> Calls { get; } = [];

        public string Manifest { get; }

        public string ImageState { get; }

        public string Page { get; }

        public string Variant { get; }

        public string PluginCache { get; }

        public string PluginOutputRecord { get; }

        public string Durable { get; }

        public T Get<T>()
            where T : notnull => host.Services.GetRequiredService<T>();

        public void Dispose()
        {
            host.Dispose();
            Project.Dispose();
        }

        private string Write(params string[] relativePath)
        {
            var path = Path.Combine([Project.RootPath, .. relativePath]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}");
            return path;
        }
    }

    /// <summary>
    /// Wraps every registered invalidator, core and plugin, to record the invalidation order.
    /// </summary>
    private static void RecordInvalidations(IServiceCollection services, List<string> calls)
    {
        foreach (var descriptor in services.Where(item => item.ServiceType == typeof(IArtifactInvalidator)).ToList())
        {
            services.Remove(descriptor);
            services.AddTransient<IArtifactInvalidator>(provider => new RecordingInvalidator(
                descriptor.ImplementationInstance as IArtifactInvalidator
                    ?? (IArtifactInvalidator)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!),
                calls));
        }
    }

    private sealed class RecordingInvalidator(IArtifactInvalidator inner, List<string> calls) : IArtifactInvalidator
    {
        public ArtifactId Artifact => inner.Artifact;

        public ArtifactKind Kind => inner.Kind;

        public IReadOnlyCollection<ArtifactId> DependsOn => inner.DependsOn;

        public ValueTask<OperationResult> InvalidateAsync(CancellationToken cancellationToken = default)
        {
            calls.Add(Artifact.Value);
            return inner.InvalidateAsync(cancellationToken);
        }
    }

    private sealed class FileArtifact(
        string id,
        ArtifactKind kind,
        IReadOnlyCollection<ArtifactId> dependsOn,
        string file) : IArtifactInvalidator
    {
        public ArtifactId Artifact { get; } = new(id);

        public ArtifactKind Kind { get; } = kind;

        public IReadOnlyCollection<ArtifactId> DependsOn { get; } = dependsOn;

        public ValueTask<OperationResult> InvalidateAsync(CancellationToken cancellationToken = default)
        {
            File.Delete(file);
            return ValueTask.FromResult(OperationResult.Ok());
        }
    }
}
