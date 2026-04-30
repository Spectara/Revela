using Spectara.Revela.Sdk.Validation;

namespace Spectara.Revela.Tests.Core.Validation;

[TestClass]
[TestCategory("Unit")]
public sealed class UrlSafetyTests
{
    // ── Schemes ──────────────────────────────────────────────────────────

    [TestMethod]
    public void IsSafeOutboundUrl_HttpsAllowed_ReturnsTrue()
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri("https://example.com/feed.ics"));
        Assert.IsTrue(ok);
    }

    [TestMethod]
    public void IsSafeOutboundUrl_HttpRejectedByDefault()
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri("http://example.com/feed.ics"));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void IsSafeOutboundUrl_HttpAllowedWhenOptedIn()
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri("http://example.com/feed.ics"), allowHttp: true);
        Assert.IsTrue(ok);
    }

    [TestMethod]
    [DataRow("ftp://example.com/x")]
    [DataRow("file:///etc/passwd")]
    [DataRow("javascript:alert(1)")]
    public void IsSafeOutboundUrl_NonHttpSchemesRejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input), allowHttp: true);
        Assert.IsFalse(ok);
    }

    // ── Loopback ─────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("https://localhost/")]
    [DataRow("https://LOCALHOST/")]
    [DataRow("https://127.0.0.1/")]
    [DataRow("https://127.5.6.7/")]
    [DataRow("https://[::1]/")]
    public void IsSafeOutboundUrl_LoopbackRejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    // ── Private IPv4 ranges ──────────────────────────────────────────────

    [TestMethod]
    [DataRow("https://10.0.0.1/")]
    [DataRow("https://10.255.255.255/")]
    [DataRow("https://172.16.0.1/")]
    [DataRow("https://172.31.255.255/")]
    [DataRow("https://192.168.0.1/")]
    [DataRow("https://192.168.255.255/")]
    [DataRow("https://100.64.0.1/")]   // RFC 6598 CGN
    public void IsSafeOutboundUrl_PrivateIPv4Rejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    [DataRow("https://172.15.0.1/")]   // just outside 172.16/12
    [DataRow("https://172.32.0.1/")]   // just outside 172.16/12
    [DataRow("https://11.0.0.1/")]     // outside 10/8
    [DataRow("https://192.169.0.1/")]  // outside 192.168/16
    public void IsSafeOutboundUrl_NonPrivateIPv4Allowed(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsTrue(ok);
    }

    // ── Link-local & cloud metadata ──────────────────────────────────────

    [TestMethod]
    [DataRow("https://169.254.1.1/")]
    [DataRow("https://169.254.169.254/latest/meta-data/")]  // AWS / Azure metadata
    public void IsSafeOutboundUrl_LinkLocalRejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    // ── IPv6 ─────────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("https://[fe80::1]/")]    // link-local
    [DataRow("https://[fc00::1]/")]    // unique local
    [DataRow("https://[ff00::1]/")]    // multicast
    public void IsSafeOutboundUrl_UnsafeIPv6Rejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void IsSafeOutboundUrl_IPv4MappedLoopbackRejected()
    {
        // ::ffff:127.0.0.1 — IPv6-mapped IPv4 loopback must still be caught.
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri("https://[::ffff:127.0.0.1]/"));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void IsSafeOutboundUrl_PublicIPv6Allowed()
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri("https://[2606:4700:4700::1111]/")); // Cloudflare DNS
        Assert.IsTrue(ok);
    }

    // ── Multicast / unspecified ──────────────────────────────────────────

    [TestMethod]
    [DataRow("https://0.0.0.0/")]
    [DataRow("https://224.0.0.1/")]    // multicast
    [DataRow("https://239.255.255.255/")]
    public void IsSafeOutboundUrl_MulticastAndUnspecifiedRejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    // ── Hostnames (no DNS resolution) ────────────────────────────────────

    [TestMethod]
    [DataRow("https://1drv.ms/f/s!ABC")]
    [DataRow("https://onedrive.live.com/share")]
    [DataRow("https://calendar.google.com/cal.ics")]
    [DataRow("https://example.com/feed")]
    public void IsSafeOutboundUrl_PublicHostnamesAllowed(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsTrue(ok);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [TestMethod]
    public void IsSafeOutboundUrl_RelativeUriRejected()
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri("/relative/path", UriKind.Relative));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void IsSafeOutboundUrl_NullThrows() => Assert.ThrowsExactly<ArgumentNullException>(() => UrlSafety.IsSafeOutboundUrl(null!));

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void IsSafeOutboundHost_EmptyRejected(string host)
    {
        var ok = UrlSafety.IsSafeOutboundHost(host);
        Assert.IsFalse(ok);
    }
}
