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
    public void Constructor_NonCanonicalValue_ThrowsArgumentException(string value) => Assert.ThrowsExactly<ArgumentException>(() => new ArtifactId(value));

    [TestMethod]
    public void DefaultValue_IsEmpty() => Assert.IsTrue(default(ArtifactId).IsEmpty);
}
