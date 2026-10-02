using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;

namespace Spectara.Revela.Tests.Core.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class ArtifactIdTests
{
    [TestMethod]
    public void Constructor_SameCanonicalValue_ProducesEqualIds()
    {
        var first = new ArtifactId("example/data");
        var second = new ArtifactId("example/data");

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("unnamespaced")]
    [DataRow(" example/data")]
    [DataRow("example\\data")]
    [DataRow("/data")]
    [DataRow("example/")]
    [DataRow("example//data")]
    [DataRow("example/invalid data")]
    [DataRow("Example/data")]
    [DataRow("../data")]
    [DataRow(".hidden/data")]
    [DataRow("con/data")]
    public void Constructor_NonCanonicalValue_ThrowsArgumentException(string value) => Assert.ThrowsExactly<ArgumentException>(() => new ArtifactId(value));

    [TestMethod]
    [DataRow("statistics/data", "statistics")]
    [DataRow("acme.captions/text/en", "acme.captions")]
    public void Owner_IsThePartBeforeTheFirstSlash(string value, string owner) =>
        Assert.AreEqual(owner, new ArtifactId(value).Owner);

    [TestMethod]
    public void DefaultValue_IsEmpty() => Assert.IsTrue(default(ArtifactId).IsEmpty);
}

[TestClass]
[TestCategory("Unit")]
public sealed class ProjectPathsOwnerTests
{
    [TestMethod]
    [DataRow("core")]
    [DataRow("statistics")]
    [DataRow("acme.captions")]
    [DataRow("my-plugin2")]
    public void GetOwnerDirectory_ValidOwner_ReturnsFolderBelowRevela(string owner) =>
        Assert.AreEqual(Path.Combine(".revela", owner), ProjectPaths.GetOwnerDirectory(owner));

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("..", DisplayName = "parent")]
    [DataRow("../output", DisplayName = "traversal")]
    [DataRow("a/b", DisplayName = "slash")]
    [DataRow("a\\b", DisplayName = "backslash")]
    [DataRow("Statistics", DisplayName = "uppercase")]
    [DataRow(".hidden", DisplayName = "leading dot")]
    [DataRow("trailing.", DisplayName = "trailing dot")]
    [DataRow("double..dot", DisplayName = "double separator")]
    [DataRow("1st", DisplayName = "leading digit")]
    [DataRow("with space", DisplayName = "whitespace")]
    [DataRow("nul", DisplayName = "Windows device name")]
    [DataRow("com1.data", DisplayName = "Windows device name with extension")]
    public void GetOwnerDirectory_InvalidOwner_Throws(string owner)
    {
        Assert.IsFalse(ProjectPaths.IsValidOwner(owner));
        Assert.ThrowsExactly<ArgumentException>(() => ProjectPaths.GetOwnerDirectory(owner));
    }

    [TestMethod]
    public void IsValidOwner_TooLong_ReturnsFalse() => Assert.IsFalse(ProjectPaths.IsValidOwner(new string('a', 65)));
}
