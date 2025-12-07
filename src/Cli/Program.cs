using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli;
using Spectara.Revela.Commands;

// ✅ Use Host.CreateApplicationBuilder for full .NET hosting features
var builder = Host.CreateApplicationBuilder(args);

// ✅ Pre-build: Load configuration and register services
builder.AddRevelaConfiguration();
builder.Services.AddRevelaCommands();
builder.Services.AddPlugins(builder.Configuration);

// ✅ Build host
var host = builder.Build();

// ✅ Post-build: Create CLI and execute
return host.UseRevelaCommands().Parse(args).Invoke();
