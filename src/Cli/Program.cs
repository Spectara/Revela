using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core;

// Resolve project BEFORE building host (standalone mode: --project or interactive selection)
var (projectPath, filteredArgs, shouldExit) = ProjectResolver.ResolveProject(args);

if (shouldExit)
{
    return 1;
}

// Create builder with correct ContentRootPath
var settings = new HostApplicationBuilderSettings
{
    Args = filteredArgs,
    ContentRootPath = projectPath ?? Directory.GetCurrentDirectory(),
};

var builder = Host.CreateApplicationBuilder(settings);
builder.ConfigureRevela(filteredArgs, new DiskPackageSource());

// NuGet-based package management (install, search, restore) — only in dynamic CLI
builder.Services.AddPackageManagement();

return await builder.Build().RunRevelaAsync(filteredArgs);
