namespace Spectara.Revela.Core.Tests;

/// <summary>
/// Validates that PluginLoadContext.IsSharedAssembly() rules remain consistent.
/// </summary>
/// <remarks>
/// SYNC: If these tests fail after changing IsSharedAssembly(),
/// also update Sdk/Build/Spectara.Revela.Sdk.targets to match.
/// </remarks>
[TestClass]
public sealed class PluginLoadContextTests
{
    [TestMethod]
    [DataRow("Spectara.Revela.Sdk")]
    [DataRow("Spectara.Revela.Core")]
    [DataRow("Spectara.Revela.Commands")]
    public void IsSharedAssembly_RevelaAssemblies_ReturnsTrue(string assemblyName) =>
        Assert.IsTrue(PluginLoadContext.IsSharedAssembly(assemblyName));

    [TestMethod]
    [DataRow("System.CommandLine")]
    [DataRow("Spectre.Console")]
    public void IsSharedAssembly_ExplicitSharedThirdParty_ReturnsTrue(string assemblyName) =>
        Assert.IsTrue(PluginLoadContext.IsSharedAssembly(assemblyName));

    [TestMethod]
    [DataRow("Microsoft.Extensions.Options")]
    [DataRow("Microsoft.Extensions.DependencyInjection.Abstractions")]
    [DataRow("Microsoft.Extensions.Configuration")]
    [DataRow("Microsoft.Extensions.Configuration.Abstractions")]
    [DataRow("Microsoft.Extensions.Configuration.Binder")]
    [DataRow("Microsoft.Extensions.Logging")]
    [DataRow("Microsoft.Extensions.Logging.Abstractions")]
    [DataRow("Microsoft.Extensions.Primitives")]
    [DataRow("Microsoft.Extensions.Options.ConfigurationExtensions")]
    [DataRow("Microsoft.Extensions.Options.DataAnnotations")]
    public void IsSharedAssembly_MicrosoftExtensions_ReturnsTrue(string assemblyName) =>
        Assert.IsTrue(PluginLoadContext.IsSharedAssembly(assemblyName));

    [TestMethod]
    [DataRow("Microsoft.Extensions.Http")]
    [DataRow("Microsoft.Extensions.Http.Resilience")]
    [DataRow("Microsoft.Extensions.Telemetry")]
    [DataRow("Microsoft.Extensions.Telemetry.Abstractions")]
    [DataRow("Microsoft.Extensions.Compliance.Abstractions")]
    [DataRow("Microsoft.Extensions.Diagnostics.ExceptionSummarization")]
    [DataRow("Microsoft.Extensions.AmbientMetadata.Application")]
    [DataRow("Microsoft.Extensions.AutoActivation")]
    [DataRow("Microsoft.Extensions.ObjectPool")]
    public void IsSharedAssembly_PluginSpecificMicrosoftExtensions_ReturnsFalse(string assemblyName) =>
        Assert.IsFalse(PluginLoadContext.IsSharedAssembly(assemblyName));

    [TestMethod]
    [DataRow("Markdig")]
    [DataRow("Scriban")]
    [DataRow("NetVips")]
    [DataRow("Polly.Core")]
    [DataRow("Polly.Extensions")]
    [DataRow("Newtonsoft.Json")]
    [DataRow("NuGet.Protocol")]
    [DataRow("SSH.NET")]
    [DataRow("SomeCustomLibrary")]
    public void IsSharedAssembly_ThirdPartyLibraries_ReturnsFalse(string assemblyName) =>
        Assert.IsFalse(PluginLoadContext.IsSharedAssembly(assemblyName));
}
