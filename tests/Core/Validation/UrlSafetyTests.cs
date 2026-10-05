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

    // ── Special-purpose IPv4 ranges (RFC 6890 registry) ──────────────────

    [TestMethod]
    [DataRow("https://0.1.2.3/")]           // 0.0.0.0/8 "this network"
    [DataRow("https://192.0.0.8/")]         // 192.0.0.0/24 IETF protocol assignments
    [DataRow("https://192.0.2.10/")]        // TEST-NET-1
    [DataRow("https://198.18.0.1/")]        // benchmarking 198.18.0.0/15
    [DataRow("https://198.19.255.255/")]
    [DataRow("https://198.51.100.7/")]      // TEST-NET-2
    [DataRow("https://203.0.113.9/")]       // TEST-NET-3
    [DataRow("https://240.0.0.1/")]         // reserved 240.0.0.0/4
    [DataRow("https://255.255.255.255/")]   // limited broadcast
    public void IsSafeOutboundUrl_ReservedIPv4Rejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    [DataRow("https://1.0.0.1/")]
    [DataRow("https://192.0.1.1/")]
    [DataRow("https://192.0.3.1/")]
    [DataRow("https://198.17.255.255/")]
    [DataRow("https://198.20.0.1/")]
    [DataRow("https://203.0.114.1/")]
    [DataRow("https://223.255.255.254/")]
    public void IsSafeOutboundUrl_PublicIPv4NextToReservedRangesAllowed(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsTrue(ok);
    }

    // ── Special-purpose IPv6 ranges and embedded IPv4 ────────────────────

    [TestMethod]
    [DataRow("https://[::]/")]                               // unspecified
    [DataRow("https://[::7f00:1]/")]                         // deprecated IPv4-compatible ::127.0.0.1
    [DataRow("https://[::808:808]/")]                        // deprecated IPv4-compatible ::8.8.8.8
    [DataRow("https://[::ffff:10.0.0.1]/")]                  // IPv4-mapped private
    [DataRow("https://[::ffff:198.51.100.1]/")]              // IPv4-mapped documentation
    [DataRow("https://[64:ff9b::a00:1]/")]                   // NAT64 → 10.0.0.1
    [DataRow("https://[64:ff9b::a9fe:a9fe]/")]               // NAT64 → 169.254.169.254
    [DataRow("https://[64:ff9b:1::1]/")]                     // local-use NAT64
    [DataRow("https://[2002:c0a8:101::1]/")]                 // 6to4 → 192.168.1.1
    [DataRow("https://[2002:7f00:1::1]/")]                   // 6to4 → 127.0.0.1
    [DataRow("https://[2001:0:4136:e378:8000:63bf:f5ff:fffe]/")] // Teredo client 10.0.0.1
    [DataRow("https://[2001:db8::1]/")]                      // documentation
    [DataRow("https://[100::1]/")]                           // discard-only
    [DataRow("https://[fec0::1]/")]                          // deprecated site-local
    public void IsSafeOutboundUrl_ReservedOrEmbeddedPrivateIPv6Rejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    [DataRow("https://[64:ff9b::808:808]/")]                 // NAT64 → 8.8.8.8
    [DataRow("https://[2002:808:808::1]/")]                  // 6to4 → 8.8.8.8
    [DataRow("https://[2001:0:4136:e378:8000:63bf:f7f7:f7f7]/")] // Teredo client 8.8.8.8
    [DataRow("https://[2001:4860:4860::8888]/")]
    [DataRow("https://[::ffff:8.8.8.8]/")]
    public void IsSafeOutboundUrl_PublicIPv6Or6to4Allowed(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsTrue(ok);
    }

    // ── Hostnames (no DNS resolution) ────────────────────────────────────

    [TestMethod]
    [DataRow("https://foo.localhost/")]
    [DataRow("https://a.b.LOCALHOST/")]
    [DataRow("https://localhost./")]
    [DataRow("https://foo.localhost./")]
    public void IsSafeOutboundUrl_LocalhostSubdomainRejected(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    [DataRow("https://notlocalhost.com/")]
    [DataRow("https://localhost.example.com/")]
    [DataRow("https://mylocalhost/")]
    public void IsSafeOutboundUrl_HostnameContainingLocalhostAllowed(string input)
    {
        var ok = UrlSafety.IsSafeOutboundUrl(new Uri(input));
        Assert.IsTrue(ok);
    }

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
