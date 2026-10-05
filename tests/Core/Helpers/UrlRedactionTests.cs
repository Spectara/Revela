using Spectara.Revela.Core.Helpers;

namespace Spectara.Revela.Tests.Core.Helpers;

[TestClass]
[TestCategory("Unit")]
public sealed class UrlRedactionTests
{
    [TestMethod]
    [DataRow("https://user:secret@feed.example/v3/index.json", "https://feed.example/v3/index.json")]
    [DataRow("https://secret@feed.example/v3/index.json", "https://feed.example/v3/index.json")]
    [DataRow("https://feed.example/v3/index.json?sig=secret&sv=1", "https://feed.example/v3/index.json")]
    [DataRow("https://feed.example/v3/index.json#secret", "https://feed.example/v3/index.json")]
    [DataRow("https://user:secret@feed.example:8443/v3/index.json?code=secret", "https://feed.example:8443/v3/index.json")]
    [DataRow("http://user:secret@localhost:5555/v3/index.json", "http://localhost:5555/v3/index.json")]
    [DataRow("https://user:secret@[2001:db8::1]/feed?x=secret", "https://[2001:db8::1]/feed")]
    public void Redact_UrlWithUserInfoQueryOrFragment_StripsThem(string input, string expected)
    {
        var redacted = UrlRedaction.Redact(input);

        Assert.AreEqual(expected, redacted);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("https://feed.example/v3/index.json")]
    [DataRow("https://api.nuget.org/v3/index.json")]
    [DataRow("http://localhost:5555/v3/index.json")]
    [DataRow("local-feed")]
    [DataRow("../shared/feed")]
    [DataRow("nuget.org")]
    [DataRow("Spectara.Revela.Plugins.Serve")]
    [DataRow("")]
    public void Redact_ValueWithoutSecrets_ReturnsUnchanged(string input) =>
        Assert.AreEqual(input, UrlRedaction.Redact(input));

    [TestMethod]
    public void Redact_LocalAbsolutePath_ReturnsUnchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), "revela-feed");

        Assert.AreEqual(path, UrlRedaction.Redact(path));
    }

    [TestMethod]
    public void Redact_Null_ReturnsNull() => Assert.IsNull(UrlRedaction.Redact(null));

    [TestMethod]
    [DataRow("https://user:secret@[invalid/feed")]
    [DataRow("https://user:secret@host:notaport/feed")]
    public void Redact_UnparseableUrl_HidesEverythingAfterScheme(string input)
    {
        var redacted = UrlRedaction.Redact(input);

        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
        Assert.StartsWith("https://", redacted, StringComparison.Ordinal);
    }
}
