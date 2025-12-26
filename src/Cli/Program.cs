using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;

// Enable UTF-8 output for proper Unicode/emoji rendering
Console.OutputEncoding = Encoding.UTF8;

// ✅ Standalone mode: Resolve project BEFORE building host
// This allows us to set ContentRootPath to the correct project directory
var (projectPath, filteredArgs, shouldExit) = ProjectResolver.ResolveProject(args);

if (shouldExit)
{
    return 1;
}

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
builder.Services.AddRevelaConfigSections(builder.Configuration);
builder.Services.AddRevelaCommands();
builder.Services.AddInteractiveMode();
builder.Services.AddPlugins(builder.Configuration, filteredArgs);

// ✅ Build host
var host = builder.Build();

// ✅ Post-build: Create CLI and execute
return host.UseRevelaCommands().Parse(filteredArgs).Invoke();
