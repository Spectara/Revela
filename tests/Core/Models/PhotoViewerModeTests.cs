using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Tests.Core.Models;

[TestClass]
[TestCategory("Unit")]
public sealed class PhotoViewerModeTests
{
    [TestMethod]
    [DataRow(PhotoViewerMode.Page, "page")]
    [DataRow(PhotoViewerMode.Lightbox, "lightbox")]
    [DataRow(PhotoViewerMode.None, "none")]
    public void ToValue_EveryMode_RoundTripsThroughTryParse(PhotoViewerMode mode, string value)
    {
        Assert.AreEqual(value, mode.ToValue());
        Assert.IsTrue(PhotoViewerModeValues.TryParse(value, out var parsed));
        Assert.AreEqual(mode, parsed);
    }

    [TestMethod]
    [DataRow(" Lightbox ", PhotoViewerMode.Lightbox)]
    [DataRow("PAGE", PhotoViewerMode.Page)]
    public void TryParse_CaseAndWhitespace_AreIgnored(string value, PhotoViewerMode expected)
    {
        Assert.IsTrue(PhotoViewerModeValues.TryParse(value, out var parsed));
        Assert.AreEqual(expected, parsed);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("slideshow")]
    [DataRow("")]
    [DataRow(null)]
    public void TryParse_UnknownOrNumericValue_ReturnsFalse(string? value) =>
        Assert.IsFalse(PhotoViewerModeValues.TryParse(value, out _));
}
