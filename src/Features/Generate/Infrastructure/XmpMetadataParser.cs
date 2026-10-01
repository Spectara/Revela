using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Reads <see cref="XmpMetadata"/> from a raw XMP packet.
/// </summary>
/// <remarks>
/// <para>
/// Supports the forms Capture One, Lightroom, and Windows write: simple properties as
/// <c>rdf:Description</c> attributes or child elements, <c>dc:subject</c> as
/// <c>rdf:Bag</c>/<c>rdf:Seq</c>, and <c>dc:title</c>/<c>dc:description</c> as
/// <c>rdf:Alt</c> language alternatives (<c>x-default</c> preferred, else the first).
/// </para>
/// <para>
/// The packet is untrusted input: DTDs are rejected, no external resources are resolved,
/// and packets above <see cref="MaxPacketBytes"/> are refused. Uses <see cref="XmlReader"/>
/// and LINQ to XML only (no reflection-based serialization), so it is Native AOT safe.
/// </para>
/// </remarks>
internal static class XmpMetadataParser
{
    /// <summary>
    /// Largest XMP packet that is parsed (bytes). A standard JPEG XMP segment holds at most 64 KB.
    /// </summary>
    public const int MaxPacketBytes = 1024 * 1024;

    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxPacketBytes,
        MaxCharactersFromEntities = 0,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = true
    };

    /// <summary>
    /// Parses an XMP packet.
    /// </summary>
    /// <param name="packet">Raw packet bytes (e.g. the libvips <c>xmp-data</c> blob).</param>
    /// <returns>The descriptive metadata; <see cref="XmpMetadata.Empty"/> values for absent fields.</returns>
    /// <exception cref="XmlException">The packet is not well-formed XML or contains a DTD.</exception>
    /// <exception cref="InvalidDataException">The packet exceeds <see cref="MaxPacketBytes"/>.</exception>
    public static XmpMetadata Parse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length > MaxPacketBytes)
        {
            throw new InvalidDataException($"XMP packet exceeds {MaxPacketBytes} bytes.");
        }

        // Writers may pad the packet with NUL bytes, which are not valid XML characters.
        packet = packet.TrimEnd((byte)0);

        using var stream = new MemoryStream(packet.ToArray(), writable: false);
        using var reader = XmlReader.Create(stream, ReaderSettings);
        var document = XDocument.Load(reader, LoadOptions.None);

        var descriptions = document.Descendants(Rdf + "Description").ToList();

        return new XmpMetadata(
            Title: ReadLanguageAlternative(descriptions, Dc + "title"),
            Description: ReadLanguageAlternative(descriptions, Dc + "description"),
            Keywords: ReadKeywords(descriptions),
            Rating: ParseRating(ReadSimpleValue(descriptions, Xmp + "Rating")));
    }

    private static string? ReadSimpleValue(IEnumerable<XElement> descriptions, XName name)
    {
        foreach (var description in descriptions)
        {
            var value = (string?)description.Attribute(name) ?? (string?)description.Element(name);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadLanguageAlternative(IEnumerable<XElement> descriptions, XName name)
    {
        foreach (var description in descriptions)
        {
            var value = (string?)description.Attribute(name);
            if (value is null && description.Element(name) is { } element)
            {
                var items = element.Descendants(Rdf + "li").ToList();
                var preferred = items.FirstOrDefault(item =>
                    string.Equals((string?)item.Attribute(XNamespace.Xml + "lang"), "x-default", StringComparison.OrdinalIgnoreCase));

                // Non-conforming writers store the text directly in the property element.
                value = (preferred ?? items.FirstOrDefault())?.Value ?? element.Value;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static List<string> ReadKeywords(IEnumerable<XElement> descriptions)
    {
        var keywords = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var subject in descriptions.Elements(Dc + "subject"))
        {
            foreach (var item in subject.Descendants(Rdf + "li"))
            {
                var keyword = item.Value.Trim();
                if (keyword.Length > 0 && seen.Add(keyword))
                {
                    keywords.Add(keyword);
                }
            }
        }

        return keywords;
    }

    private static int? ParseRating(string? value)
    {
        // xmp:Rating is a Real in the spec; writers use whole stars (-1 = rejected).
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rating))
        {
            return null;
        }

        var stars = (int)Math.Round(rating, MidpointRounding.AwayFromZero);
        return stars is >= -1 and <= 5 ? stars : null;
    }
}
