using Spectara.Revela.Plugins.Source.OneDrive.Providers;

namespace Spectara.Revela.Tests.Plugins.Source.OneDrive.Providers;

/// <summary>
/// Tests for <see cref="SharedLinkProvider.RedactShareUrl"/>.
/// OneDrive share URLs embed an account-scoped bearer token in the path;
/// these tests prevent accidental regressions that would log the raw token.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class RedactShareUrlTests
{
    private const string RealLookingShareUrl =
        "https://1drv.ms/f/c/90362b8badfb304c/IgAJ09BVc-BeQJ2A7C4iK3r2ASwhRVRLojLQbLOoZHTwGgk";

    private const string ShareToken = "IgAJ09BVc-BeQJ2A7C4iK3r2ASwhRVRLojLQbLOoZHTwGgk";

    [TestMethod]
    public void DoesNotLeakShareToken()
    {
        var redacted = SharedLinkProvider.RedactShareUrl(RealLookingShareUrl);

        Assert.IsFalse(
            redacted.Contains(ShareToken, StringComparison.Ordinal),
            $"Redacted output must not contain the share token. Got: {redacted}");
        Assert.IsFalse(
            redacted.Contains("90362b8badfb304c", StringComparison.Ordinal),
            "Redacted output must not contain the account-scoped resource id.");
    }

    [TestMethod]
    public void PreservesHostForCorrelation()
    {
        var redacted = SharedLinkProvider.RedactShareUrl(RealLookingShareUrl);

        Assert.IsTrue(
            redacted.Contains("1drv.ms", StringComparison.Ordinal),
            $"Redacted output should keep the host for log correlation. Got: {redacted}");
    }

    [TestMethod]
    public void IsDeterministic_ForSameInput()
    {
        var a = SharedLinkProvider.RedactShareUrl(RealLookingShareUrl);
        var b = SharedLinkProvider.RedactShareUrl(RealLookingShareUrl);

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void DistinguishesDifferentUrls()
    {
        var a = SharedLinkProvider.RedactShareUrl("https://1drv.ms/f/c/aaa/TOKEN_ONE");
        var b = SharedLinkProvider.RedactShareUrl("https://1drv.ms/f/c/bbb/TOKEN_TWO");

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void HandlesNullEmptyAndWhitespace()
    {
        Assert.AreEqual("<empty>", SharedLinkProvider.RedactShareUrl(null));
        Assert.AreEqual("<empty>", SharedLinkProvider.RedactShareUrl(""));
        Assert.AreEqual("<empty>", SharedLinkProvider.RedactShareUrl("   "));
    }

    [TestMethod]
    public void HandlesUnparseableInput()
    {
        var redacted = SharedLinkProvider.RedactShareUrl("not a url");

        Assert.IsTrue(redacted.StartsWith("<unparseable>/#", StringComparison.Ordinal));
    }
}
