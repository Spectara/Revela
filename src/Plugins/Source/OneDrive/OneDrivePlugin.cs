using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Telemetry;
using Spectara.Revela.Plugins.Source.OneDrive.Commands;
using Spectara.Revela.Plugins.Source.OneDrive.Configuration;
using Spectara.Revela.Plugins.Source.OneDrive.Providers;
using Spectara.Revela.Plugins.Source.OneDrive.Wizard;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Hosting;

namespace Spectara.Revela.Plugins.Source.OneDrive;

/// <summary>
/// OneDrive source plugin for Revela
/// </summary>
public sealed partial class OneDrivePlugin : IPlugin
{
    /// <inheritdoc />
    public PackageMetadata Metadata { get; } = new()
    {
        Id = "Spectara.Revela.Plugins.Source.OneDrive",
        Name = "Source OneDrive",
        Version = PackageVersion.FromAssembly(typeof(OneDrivePlugin).Assembly),
        Description = "Download images from OneDrive shared folders",
        Author = "Spectara"
    };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // BindConfiguration must live in user-written source so the .NET
        // Configuration Binding Source Generator can intercept it for trim/AOT.
        // The Section constant is hand-written on the config class; the SDK generator
        // reports REVELA002 if it differs from the [RevelaConfig] attribute.
        services.AddOptions<OneDrivePluginConfig>()
            .BindConfiguration(OneDrivePluginConfig.Section);

        // Trim/AOT-safe DataAnnotations validation via the
        // [OptionsValidator] source generator. TryAddEnumerable keeps it idempotent.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OneDrivePluginConfig>, OneDrivePluginConfigValidator>());

        services.AddHttpClient<SharedLinkProvider>((serviceProvider, client) =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // OneDrive API can be slow for large files
            // SharedLinkProvider streams every response with its own caps; this bounds any
            // HttpClient-buffered (ResponseContentRead) call as well.
            client.MaxResponseContentBufferSize = SharedLinkProvider.DefaultMaxJsonResponseBytes;
            var version = serviceProvider.GetRequiredService<IBuildInfo>().Version;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"Revela/{version} (Static Site Generator)");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        .RemoveAllLoggers()
        .AddResilienceHandler("onedrive-retry", (builder, context) =>
        {
            var telemetry = new TelemetryOptions(context.GetOptions<TelemetryOptions>())
            {
                LoggerFactory = NullLoggerFactory.Instance
            };
            telemetry.TelemetryListeners.Add(new SafeTelemetryListener(context.ServiceProvider.GetRequiredService<ILogger<SharedLinkProvider>>()));
            builder.ConfigureTelemetry(telemetry);

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
            });

            // Timeout per request attempt
            builder.AddTimeout(TimeSpan.FromMinutes(2));
        });

        // Note: DownloadAnalyzer is static, no DI registration needed

        // Register Commands for Dependency Injection (idempotent)
        services.TryAddTransient<OneDriveSourceCommand>();
        services.TryAddTransient<ConfigOneDriveCommand>();

        // Register Wizard Step (for project setup wizard integration)
        services.TryAddEnumerable(ServiceDescriptor.Transient<IWizardStep, OneDriveWizardStep>());
    }

    private sealed partial class SafeTelemetryListener(ILogger<SharedLinkProvider> logger) : TelemetryListener
    {
        public override void Write<TResult, TArgs>(in TelemetryEventArguments<TResult, TArgs> args)
        {
            if (args.Outcome is not { } outcome || !logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            var host = args.Context.GetRequestMessage()?.RequestUri?.Host ?? "unknown";
            var errorCategory = outcome.Exception?.GetType().Name ?? "None";
            var statusCode = outcome.Result is HttpResponseMessage response ? (int?)response.StatusCode : null;
            LogHttpOutcome(args.Event.EventName, host, errorCategory, statusCode);
        }

        [LoggerMessage(Level = LogLevel.Debug, Message = "OneDrive HTTP event {EventName} for {Host}: {ErrorCategory}, status {StatusCode}")]
        private partial void LogHttpOutcome(string eventName, string host, string errorCategory, int? statusCode);
    }

    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
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
        //    Writes project.json, so it requires a project (like every plugin config command)
        yield return new CommandDescriptor(
            configCommand.Create(),
            ParentCommand: "config",
            Order: 10,
            Group: "Source");
    }
}
