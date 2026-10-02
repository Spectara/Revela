using System.Reflection;
using System.Reflection.Emit;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Core.Abstractions;

[TestClass]
[TestCategory("Unit")]
public sealed class PackageVersionTests
{
    [TestMethod]
    public void FromAssembly_InformationalVersionWithBuildMetadata_ReturnsSemanticVersion()
    {
        var assembly = CreateAssembly(new Version(1, 0, 0), "0.0.1-beta.21+abc1234");

        var version = PackageVersion.FromAssembly(assembly);

        Assert.AreEqual("0.0.1-beta.21", version);
    }

    [TestMethod]
    public void FromAssembly_InformationalVersionWithoutBuildMetadata_ReturnsItUnchanged()
    {
        var assembly = CreateAssembly(new Version(1, 0, 0), "2.1.0-rc.1");

        var version = PackageVersion.FromAssembly(assembly);

        Assert.AreEqual("2.1.0-rc.1", version);
    }

    [TestMethod]
    public void FromAssembly_NoInformationalVersion_FallsBackToAssemblyVersion()
    {
        var assembly = CreateAssembly(new Version(3, 4, 5, 6), informationalVersion: null);

        var version = PackageVersion.FromAssembly(assembly);

        Assert.AreEqual("3.4.5", version);
    }

    private static AssemblyBuilder CreateAssembly(Version version, string? informationalVersion)
    {
        var name = new AssemblyName($"PackageVersionTests_{Guid.NewGuid():N}") { Version = version };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }

        return assembly;
    }
}
