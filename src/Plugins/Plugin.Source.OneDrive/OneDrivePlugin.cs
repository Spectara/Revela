using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Spectara.Revela.Plugin.Source.OneDrive.Commands;
using Spectara.Revela.Plugin.Source.OneDrive.Configuration;
using Spectara.Revela.Plugin.Source.OneDrive.Providers;
using Spectara.Revela.Plugin.Source.OneDrive.Wizard;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugin.Source.OneDrive;

/// <summary>
/// OneDrive source plugin for Revela
/// </summary>
public sealed class OneDrivePlugin : IPlugin
{
    private IServiceProvider? services;

    /// <inheritdoc />
    public IPluginMetadata Metadata => new PluginMetadata
    {
        Name = "Source OneDrive",
        Version = "1.0.0",
        Description = "Download images from OneDrive shared folders",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureConfiguration(IConfigurationBuilder configuration)
    {
        // Nothing to do - plugin config is stored in project.json via IConfigService.
        // Environment variables with SPECTARA__REVELA__ prefix are auto-loaded.
        // Example ENV: SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL=https://...
    }

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // Register Plugin Configuration (IOptions pattern)
        // Binds to Plugins:Spectara.Revela.Plugin.Source.OneDrive section
        // Note: No ValidateDataAnnotations/ValidateOnStart - plugins may be installed but not configured.
        // Validation happens in commands when config is actually needed (e.g., DownloadCommand).
        services.AddOptions<OneDrivePluginConfig>()
            .BindConfiguration(OneDrivePluginConfig.SectionName);

        // Register Typed HttpClient for SharedLinkProvider with Resilience
        // Custom resilience handler: retry without verbose logging
        // Only logs on final failure, not on each retry attempt
        services.AddHttpClient<SharedLinkProvider>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // OneDrive API can be slow for large files
            client.DefaultRequestHeaders.Add("User-Agent", "Revela/1.0 (Static Site Generator)");
        })
        .AddResilienceHandler("onedrive-retry", builder =>
        {
            // Retry: 3 attempts with exponential backoff, no logging on retry
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                // Handles: HTTP 408, 429, 500+ (including 503), network errors
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => (int)r.StatusCode >= 500 ||
                                       r.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                                       r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                // No OnRetry callback = no logging during retries
                // User only sees error if ALL retries fail
            });

            // Timeout per request attempt
            builder.AddTimeout(TimeSpan.FromMinutes(2));
        });

        // Note: DownloadAnalyzer is static, no DI registration needed

        // Register Commands for Dependency Injection
        services.AddTransient<OneDriveSourceCommand>();
        services.AddTransient<ConfigOneDriveCommand>();

        // Register Wizard Step (for project setup wizard integration)
        services.AddTransient<IWizardStep, OneDriveWizardStep>();
    }

    /// <inheritdoc />
    public void Initialize(IServiceProvider services) => this.services = services;

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands()
    {
        if (services is null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call Initialize() first.");
        }

        // Resolve commands from DI container
        var sourceCommand = services.GetRequiredService<OneDriveSourceCommand>();
        var configCommand = services.GetRequiredService<ConfigOneDriveCommand>();

        // 1. Register source command → revela source onedrive sync
        //    Creates: source → onedrive → sync
        //    Requires project (downloads to project's source folder)
        var oneDriveCommand = new Command("onedrive", "OneDrive shared folder source");
        oneDriveCommand.Subcommands.Add(sourceCommand.Create());
        yield return new CommandDescriptor(oneDriveCommand, ParentCommand: "source", Order: 20);

        // 2. Register config command → revela config onedrive
        //    Direct under config with Source group for menu organization
        //    Doesn't require project (config file is independent)
        yield return new CommandDescriptor(
            configCommand.Create(),
            ParentCommand: "config",
            Order: 10,
            Group: "Source",
            RequiresProject: false);
    }
}
