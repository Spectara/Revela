using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Embedded;
using Spectara.Revela.Cli.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
});

builder.ConfigureRevela(args, new EmbeddedPackageSource());

return await builder.Build().RunRevelaAsync(args);
