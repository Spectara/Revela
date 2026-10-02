using Spectara.Revela.Core.Helpers;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
public sealed class PackageManagementCommandsTests
{
    [TestMethod]
    [DataRow("plugin", "install")]
    [DataRow("plugin", "uninstall")]
    [DataRow("theme", "install")]
    [DataRow("THEME", "Uninstall")]
    public void ModifiesPackageFiles_InstallOrUninstall_ReturnsTrue(string command, string subcommand)
    {
        Assert.IsTrue(PackageManagementCommands.ModifiesPackageFiles([command, subcommand, "Some.Package"]));
    }

    [TestMethod]
    [DataRow("plugin", "list")]
    [DataRow("theme", "list")]
    [DataRow("theme", "files")]
    [DataRow("theme", "extract")]
    [DataRow("generate", "all")]
    [DataRow("config", "theme")]
    public void ModifiesPackageFiles_ReadOnlyOrOtherCommand_ReturnsFalse(string command, string subcommand)
    {
        Assert.IsFalse(PackageManagementCommands.ModifiesPackageFiles([command, subcommand]));
    }

    [TestMethod]
    public void ModifiesPackageFiles_TooFewSegments_ReturnsFalse()
    {
        Assert.IsFalse(PackageManagementCommands.ModifiesPackageFiles([]));
        Assert.IsFalse(PackageManagementCommands.ModifiesPackageFiles(["theme"]));
    }

    [TestMethod]
    [DataRow("plugin")]
    [DataRow("theme")]
    public void DeletesPackageFiles_Uninstall_ReturnsTrue(string command)
    {
        Assert.IsTrue(PackageManagementCommands.DeletesPackageFiles([command, "uninstall"]));
    }

    [TestMethod]
    [DataRow("plugin", "install")]
    [DataRow("theme", "install")]
    [DataRow("theme", "list")]
    [DataRow("generate", "uninstall")]
    public void DeletesPackageFiles_NotPackageUninstall_ReturnsFalse(string command, string subcommand)
    {
        Assert.IsFalse(PackageManagementCommands.DeletesPackageFiles([command, subcommand]));
    }
}
