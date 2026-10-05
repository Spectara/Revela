using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Features.Packages.Services;

// Packages are discovered once configuration and the environment are known: assemblies next
// to the executable are only loaded in Development (F5). NuGet-based package management
// (install, search, restore) is only wired up in the dynamic CLI, applied after
// ConfigureRevela and before the host is built.
return await HostBootstrap.RunAsync(
    args,
    (environment, loggerFactory) => new DiskPackageSource(PackageOptions.ForEnvironment(environment.EnvironmentName), loggerFactory),
    configureExtra: builder => builder.Services.AddPackageManagement());
