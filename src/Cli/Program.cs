using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
});

builder.ConfigureRevela(args, new DiskPackageSource());

// NuGet-based package management (install, search, restore) — only in dynamic CLI
builder.Services.AddPackageManagement();

return await builder.Build().RunRevelaAsync(args);
