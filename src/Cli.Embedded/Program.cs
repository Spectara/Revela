using Spectara.Revela.Cli.Embedded;
using Spectara.Revela.Cli.Hosting;

return await HostBootstrap.RunAsync(args, new EmbeddedPackageSource());
