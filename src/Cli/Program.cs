using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;

// Enable UTF-8 output for proper Unicode/emoji rendering
Console.OutputEncoding = Encoding.UTF8;

// ✅ Standalone mode: Resolve project BEFORE building host
// This allows us to set ContentRootPath to the correct project directory
var (projectPath, filteredArgs, shouldExit) = ProjectResolver.ResolveProject(args);

if (shouldExit)
{
    return 1;
}

// ✅ Detect interactive mode: no arguments AND interactive terminal
// System.CommandLine 2.0 shows help by default when no subcommand is given,
// so we need to detect this case and explicitly trigger interactive mode.
var isInteractiveMode = filteredArgs.Length == 0
    && !Console.IsInputRedirected
    && !Console.IsOutputRedirected
    && Environment.UserInteractive;

// ✅ Create builder with correct ContentRootPath
// In standalone mode: project directory from selection or --project
// In tool mode: current working directory (default behavior)
var settings = new HostApplicationBuilderSettings
{
    Args = filteredArgs,
    ContentRootPath = projectPath ?? Directory.GetCurrentDirectory(),
};

var builder = Host.CreateApplicationBuilder(settings);

// ✅ Pre-build: Load configuration and register services
builder.AddRevelaConfiguration();
builder.Services.AddRevelaConfigSections();
builder.Services.AddRevelaCommands();
builder.Services.AddInteractiveMode();
builder.Services.AddPlugins(builder.Configuration, filteredArgs);

// Register ProjectEnvironment (runtime info about project location)
builder.Services.AddOptions<ProjectEnvironment>()
    .Configure<IHostEnvironment>((env, host) => env.Path = host.ContentRootPath);

// ✅ Build host
var host = builder.Build();

// ✅ Post-build: Create CLI and execute
var rootCommand = host.UseRevelaCommands();

// If interactive mode detected, run it directly instead of going through System.CommandLine parser
if (isInteractiveMode)
{
    var interactiveService = host.Services.GetRequiredService<IInteractiveMenuService>();
    interactiveService.RootCommand = rootCommand;
    return await interactiveService.RunAsync(CancellationToken.None);
}

return await rootCommand.Parse(filteredArgs).InvokeAsync();
