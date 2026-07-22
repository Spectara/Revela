using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core;

// NuGet-based package management (install, search, restore) is only wired up in
// the dynamic CLI, applied after ConfigureRevela and before the host is built.
return await HostBootstrap.RunAsync(
    args,
    new DiskPackageSource(),
    configureExtra: builder => builder.Services.AddPackageManagement());
