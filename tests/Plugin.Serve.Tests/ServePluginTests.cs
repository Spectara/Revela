using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Plugin.Serve.Tests;

[TestClass]
public sealed class ServePluginTests
{
    [TestMethod]
    public void Metadata_HasCorrectValues()
    {
        // Arrange
        var plugin = new ServePlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        Assert.AreEqual("Serve", metadata.Name);
        Assert.AreEqual("1.0.0", metadata.Version);
        Assert.AreEqual("Local HTTP server for previewing generated sites", metadata.Description);
        Assert.AreEqual("Spectara", metadata.Author);
    }

    [TestMethod]
    public void ConfigureServices_RegistersServeCommand()
    {
        // Arrange
        var plugin = new ServePlugin();
        var services = new ServiceCollection();

        // Add required dependencies that ServeCommand needs
        services.AddLogging();
        services.AddOptions();

        // Act
        plugin.ConfigureServices(services);

        // Assert - ServeCommand should be registered
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ServeCommand));
        Assert.IsNotNull(descriptor, "ServeCommand should be registered");
        Assert.AreEqual(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [TestMethod]
    public void GetCommands_WithoutInitialize_ThrowsInvalidOperation()
    {
        // Arrange
        var plugin = new ServePlugin();

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>(() => plugin.GetCommands().ToList());
    }

    [TestMethod]
    public void GetCommands_ReturnsTwoCommands()
    {
        // Arrange
        var plugin = new ServePlugin();
        var services = new ServiceCollection();
        services.AddLogging();

        // Add IConfiguration (required by BindConfiguration)
        // Use empty configuration - defaults will be used
        var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
        var configuration = configBuilder.Build();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);

        // Add mock IConfigService (required by ConfigServeCommand)
        var configService = Substitute.For<IConfigService>();
        services.AddSingleton(configService);

        // Add mock IPathResolver (required by ServeCommand)
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.OutputPath.Returns("/fake/output");
        services.AddSingleton(pathResolver);

        plugin.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        plugin.Initialize(serviceProvider);

        // Act
        var commands = plugin.GetCommands().ToList();

        // Assert - returns 2 commands: serve (root), config serve
        Assert.HasCount(2, commands);

        // Check serve command (root level)
        var serveDescriptor = commands.First(c => c.Command.Name == "serve" && c.ParentCommand is null);
        Assert.AreEqual(15, serveDescriptor.Order, "Should be between generate (10) and clean (20)");
        Assert.AreEqual("Build", serveDescriptor.Group, "Should be in Build group");

        // Check config command (under config parent)
        var configDescriptor = commands.First(c => c.ParentCommand == "config");
        Assert.AreEqual("serve", configDescriptor.Command.Name);
    }
}
