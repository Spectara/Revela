using System.Net;
using System.Net.Sockets;

namespace Spectara.Revela.Sdk.Validation;

/// <summary>
/// URL safety helpers for plugins that fetch resources from user-supplied URLs.
/// </summary>
/// <remarks>
/// These checks are <b>defense in depth</b>, not a sandbox. They reject obvious
/// SSRF targets (loopback and <c>*.localhost</c>, private networks, link-local,
/// special-purpose IANA ranges, IPv6 forms embedding such IPv4 addresses) but
/// do <b>not</b> resolve hostnames — a hostname pointing at <c>127.0.0.1</c>
/// via DNS will still pass <see cref="IsSafeOutboundHost"/>.
/// For full protection, plugins should additionally configure outbound firewall
/// rules at the deployment level.
/// </remarks>
public static class UrlSafety
{
    /// <summary>
    /// Returns <c>true</c> when the URL is well-formed, uses an allowed scheme,
    /// and its host passes <see cref="IsSafeOutboundHost"/>.
    /// </summary>
    /// <param name="uri">The URL to validate.</param>
    /// <param name="allowHttp">When <c>false</c> (default) only <c>https</c> is accepted.</param>
    public static bool IsSafeOutboundUrl(Uri uri, bool allowHttp = false)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (allowHttp)
        {
            if (uri.Scheme is not ("http" or "https"))
            {
                return false;
            }
        }
        else if (uri.Scheme is not "https")
        {
            return false;
        }

        return IsSafeOutboundHost(uri.Host);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="host"/> is either a hostname
    /// (DNS will resolve at request time) or a literal IP outside the loopback,
    /// private, link-local, multicast, unspecified, documentation, benchmarking
    /// and reserved ranges. IPv6 forms that embed an IPv4 address (mapped, NAT64,
    /// 6to4, Teredo) are judged by the embedded address.
    /// <c>localhost</c> and every <c>*.localhost</c> name are rejected.
    /// </summary>
    public static bool IsSafeOutboundHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // Strip IPv6 brackets if Uri.Host returned them.
        var literal = host.StartsWith('[') && host.EndsWith(']')
            ? host[1..^1]
            : host;

        if (IsLocalhostName(literal))
        {
            return false;
        }

        if (!IPAddress.TryParse(literal, out var ip))
        {
            // Hostname — DNS resolution happens later. Accept here.
            return true;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return !IsReservedIPv4(bytes);
        }

        return ip.AddressFamily == AddressFamily.InterNetworkV6 && !IsReservedIPv6(bytes);
    }

    // RFC 6761 §6.3: "localhost" and every name under ".localhost" resolve to loopback.
    // A trailing dot (fully qualified form) does not change that.
    private static bool IsLocalhostName(string host)
    {
        var name = host.EndsWith('.') ? host[..^1] : host;
        return string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    // Special-purpose IPv4 ranges (IANA registry, RFC 6890) that are not valid outbound targets.
    private static bool IsReservedIPv4(ReadOnlySpan<byte> b) =>
        b[0] == 0                                           // 0.0.0.0/8 "this network" (RFC 1122 §3.2.1.3)
        || b[0] == 10                                       // 10.0.0.0/8 private (RFC 1918)
        || (b[0] == 100 && (b[1] & 0xC0) == 64)             // 100.64.0.0/10 carrier-grade NAT (RFC 6598)
        || b[0] == 127                                      // 127.0.0.0/8 loopback (RFC 1122 §3.2.1.3)
        || (b[0] == 169 && b[1] == 254)                     // 169.254.0.0/16 link-local, cloud metadata (RFC 3927)
        || (b[0] == 172 && (b[1] & 0xF0) == 16)             // 172.16.0.0/12 private (RFC 1918)
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)          // 192.0.0.0/24 IETF protocol assignments (RFC 6890 §2.2.2)
        || (b[0] == 192 && b[1] == 0 && b[2] == 2)          // 192.0.2.0/24 TEST-NET-1 (RFC 5737)
        || (b[0] == 192 && b[1] == 168)                     // 192.168.0.0/16 private (RFC 1918)
        || (b[0] == 198 && (b[1] & 0xFE) == 18)             // 198.18.0.0/15 benchmarking (RFC 2544)
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)       // 198.51.100.0/24 TEST-NET-2 (RFC 5737)
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)        // 203.0.113.0/24 TEST-NET-3 (RFC 5737)
        || b[0] >= 224;                                     // 224.0.0.0/4 multicast (RFC 5771), 240.0.0.0/4 reserved
                                                            // (RFC 1112 §4) incl. 255.255.255.255 broadcast (RFC 919)

    // Special-purpose IPv6 ranges (IANA registry, RFC 6890). Forms that carry an IPv4
    // address are judged by that address so they cannot tunnel to a blocked IPv4 target.
    private static bool IsReservedIPv6(ReadOnlySpan<byte> b)
    {
        // ::/96 — unspecified, loopback and the deprecated IPv4-compatible form (RFC 4291 §2.5.5.1).
        if (b[..12].IndexOfAnyExcept((byte)0) < 0)
        {
            return true;
        }

        // 64:ff9b::/96 — NAT64 well-known prefix, IPv4 in the last 32 bits (RFC 6052 §2.1).
        if (b[..12].SequenceEqual(Nat64WellKnownPrefix))
        {
            return IsReservedIPv4(b[12..]);
        }

        // 64:ff9b:1::/48 — local-use NAT64 prefix (RFC 8215).
        if (b[..6].SequenceEqual(Nat64LocalUsePrefix))
        {
            return true;
        }

        // 100::/64 — discard-only (RFC 6666).
        if (b[0] == 0x01 && b[1..8].IndexOfAnyExcept((byte)0) < 0)
        {
            return true;
        }

        if (b[0] == 0x20 && b[1] == 0x01)
        {
            // 2001::/32 — Teredo; the client IPv4 is the bit-inverted last 32 bits (RFC 4380 §4).
            if (b[2] == 0 && b[3] == 0)
            {
                Span<byte> client = [(byte)~b[12], (byte)~b[13], (byte)~b[14], (byte)~b[15]];
                return IsReservedIPv4(client);
            }

            // 2001:db8::/32 — documentation (RFC 3849).
            if (b[2] == 0x0D && b[3] == 0xB8)
            {
                return true;
            }
        }

        // 2002::/16 — 6to4, IPv4 in bits 16..47 (RFC 3056 §2).
        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return IsReservedIPv4(b[2..6]);
        }

        return (b[0] & 0xFE) == 0xFC                        // fc00::/7 unique local (RFC 4193)
            || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)      // fe80::/10 link-local (RFC 4291 §2.5.6)
            || (b[0] == 0xFE && (b[1] & 0xC0) == 0xC0)      // fec0::/10 deprecated site-local (RFC 3879)
            || b[0] == 0xFF;                                // ff00::/8 multicast (RFC 4291 §2.7)
    }

    private static ReadOnlySpan<byte> Nat64WellKnownPrefix => [0x00, 0x64, 0xFF, 0x9B, 0, 0, 0, 0, 0, 0, 0, 0];

    private static ReadOnlySpan<byte> Nat64LocalUsePrefix => [0x00, 0x64, 0xFF, 0x9B, 0x00, 0x01];
}
