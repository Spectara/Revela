using System.Net;
using System.Net.Sockets;

namespace Spectara.Revela.Sdk.Validation;

/// <summary>
/// URL safety helpers for plugins that fetch resources from user-supplied URLs.
/// </summary>
/// <remarks>
/// These checks are <b>defense in depth</b>, not a sandbox. They reject obvious
/// SSRF targets (loopback, private networks, link-local, IPv6 site-local) but
/// do <b>not</b> resolve hostnames — a hostname pointing at <c>127.0.0.1</c>
/// via DNS will still pass <see cref="IsSafeOutboundHost"/>.
/// For full protection, plugins should additionally configure outbound firewall
/// rules at the deployment level.
/// </remarks>
public static class UrlSafety
{
    /// <summary>
    /// Returns <c>true</c> when the URL is well-formed, uses an allowed scheme,
    /// and (when the host is a literal IP) does not target loopback, private,
    /// link-local, or multicast addresses.
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
    /// (DNS will resolve at request time) or a literal IP that is not loopback,
    /// private, link-local, multicast, or unspecified (<c>0.0.0.0</c> / <c>::</c>).
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

        // "localhost" is technically a hostname but always loopback.
        if (string.Equals(literal, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IPAddress.TryParse(literal, out var ip))
        {
            // Hostname — DNS resolution happens later. Accept here.
            return true;
        }

        if (IPAddress.IsLoopback(ip)) // 127.0.0.0/8 + ::1
        {
            return false;
        }

        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return !IsPrivateIPv4(bytes)
                && !IsLinkLocalIPv4(bytes)
                && !IsMulticastIPv4(bytes);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !ip.IsIPv6LinkLocal
                && !ip.IsIPv6SiteLocal
                && !ip.IsIPv6Multicast
                && !IsIPv6UniqueLocal(ip)
                && !IsIPv4MappedUnsafe(ip);
        }

        return false;
    }

    // RFC 1918: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
    // Plus RFC 6598 carrier-grade NAT: 100.64.0.0/10
    private static bool IsPrivateIPv4(byte[] b) =>
        b[0] == 10
        || (b[0] == 172 && (b[1] & 0xF0) == 16)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 100 && (b[1] & 0xC0) == 64);

    // RFC 3927: 169.254.0.0/16 (link-local, includes AWS/Azure metadata IP)
    private static bool IsLinkLocalIPv4(byte[] b) =>
        b[0] == 169 && b[1] == 254;

    // RFC 5771: 224.0.0.0/4
    private static bool IsMulticastIPv4(byte[] b) =>
        (b[0] & 0xF0) == 224;

    // RFC 4193: fc00::/7 — IPv6 Unique Local Address (ULA).
    // .NET's IsIPv6SiteLocal only covers the deprecated fec0::/10.
    private static bool IsIPv6UniqueLocal(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return (b[0] & 0xFE) == 0xFC;
    }

    // IPv6-mapped IPv4 (::ffff:10.0.0.1) must be re-checked against IPv4 rules.
    private static bool IsIPv4MappedUnsafe(IPAddress ip)
    {
        if (!ip.IsIPv4MappedToIPv6)
        {
            return false;
        }

        var mapped = ip.MapToIPv4();
        return !IsSafeOutboundHost(mapped.ToString());
    }
}
