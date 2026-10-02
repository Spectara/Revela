using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Verifies how host commands appear as interactive menu entries.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class InteractiveMenuEntryTests
{
    [TestMethod]
    public void Info_IsDirectlyExecutableEntryInInfoGroup()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Menu" } }));
        var builder = HostBootstrap.CreateBuilder([], new EmptyPackageSource(), project.RootPath);
        using var host = builder.Build();

        var rootCommand = host.UseRevelaCommands();
        var orderRegistry = host.Services.GetRequiredService<CommandOrderRegistry>();
        var info = rootCommand.Subcommands.Single(c => string.Equals(c.Name, "info", StringComparison.Ordinal));

        var choice = MenuChoice.FromCommand(info, orderRegistry.IsPipelineStep(info));

        Assert.AreEqual(CommandGroups.Info, orderRegistry.GetGroup(info));
        Assert.IsFalse(orderRegistry.RequiresProject(info), "info must be reachable without a project.");
        Assert.AreEqual(MenuAction.Execute, choice.Action, "info has no subcommands; selecting it runs `revela info`.");
        Assert.AreSame(info, choice.Command);
        Assert.StartsWith("info ", choice.DisplayName, StringComparison.Ordinal);
    }

    private sealed class EmptyPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() => [];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}
