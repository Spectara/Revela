using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;

// Enable UTF-8 output for proper Unicode/emoji rendering
Console.OutputEncoding = Encoding.UTF8;

// ✅ Use Host.CreateApplicationBuilder for full .NET hosting features
var builder = Host.CreateApplicationBuilder(args);

// ✅ Pre-build: Load configuration and register services
builder.AddRevelaConfiguration();
builder.Services.AddRevelaConfigSections(builder.Configuration);
builder.Services.AddRevelaCommands();
builder.Services.AddInteractiveMode();
builder.Services.AddPlugins(builder.Configuration, args);

// ✅ Build host
var host = builder.Build();

// ✅ Post-build: Create CLI and execute
return host.UseRevelaCommands().Parse(args).Invoke();
