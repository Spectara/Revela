using System.Text;
using System.Xml;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Generate.Infrastructure;

/// <summary>
/// Tests for <see cref="XmpMetadataParser"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class XmpMetadataParserTests
{
    private const string RdfOpen = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
         <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        """;

    private const string RdfClose = """
         </rdf:RDF>
        </x:xmpmeta>
        """;

    private static XmpMetadata Parse(string packet) => XmpMetadataParser.Parse(Encoding.UTF8.GetBytes(packet));

    private static string Description(string attributes, string body) => $"""
        {RdfOpen}
          <rdf:Description rdf:about="" xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/" {attributes}>
          {body}
          </rdf:Description>
        {RdfClose}
        """;

    [TestMethod]
    public void Parse_CaptureOneStylePacket_ReadsAllFields()
    {
        var packet = TestImageGenerator.CreateXmpPacket(
            title: "Abendlicht",
            description: "Sonnenuntergang am See",
            keywords: ["Selected", "Startseite"],
            rating: 4);

        var result = Parse(packet);

        Assert.AreEqual("Abendlicht", result.Title);
        Assert.AreEqual("Sonnenuntergang am See", result.Description);
        Assert.AreEqual("Selected,Startseite", string.Join(',', result.Keywords));
        Assert.AreEqual(4, result.Rating);
    }

    [TestMethod]
    public void Parse_RatingAsElement_ReadsRating()
    {
        var result = Parse(Description(string.Empty, "<xmp:Rating>5</xmp:Rating>"));

        Assert.AreEqual(5, result.Rating);
    }

    [TestMethod]
    public void Parse_RejectedRating_ReadsMinusOne()
    {
        var result = Parse(Description("xmp:Rating=\"-1\"", string.Empty));

        Assert.AreEqual(-1, result.Rating);
    }

    [TestMethod]
    [DataRow("6")]
    [DataRow("-2")]
    [DataRow("five")]
    [DataRow("")]
    public void Parse_InvalidRating_ReturnsNullRating(string rating)
    {
        var result = Parse(Description($"xmp:Rating=\"{rating}\"", string.Empty));

        Assert.IsNull(result.Rating);
    }

    [TestMethod]
    public void Parse_LanguageAlternatives_PrefersXDefault()
    {
        var result = Parse(Description(string.Empty, """
            <dc:title><rdf:Alt>
              <rdf:li xml:lang="de-DE">Deutscher Titel</rdf:li>
              <rdf:li xml:lang="x-default">Default title</rdf:li>
            </rdf:Alt></dc:title>
            """));

        Assert.AreEqual("Default title", result.Title);
    }

    [TestMethod]
    public void Parse_LanguageAlternativesWithoutDefault_UsesFirst()
    {
        var result = Parse(Description(string.Empty, """
            <dc:description><rdf:Alt>
              <rdf:li xml:lang="de-DE">Erste Beschreibung</rdf:li>
              <rdf:li xml:lang="en-US">Second description</rdf:li>
            </rdf:Alt></dc:description>
            """));

        Assert.AreEqual("Erste Beschreibung", result.Description);
    }

    [TestMethod]
    public void Parse_SeparateDescriptionElements_MergesFields()
    {
        var packet = $"""
            {RdfOpen}
              <rdf:Description rdf:about="" xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmp:Rating="3"/>
              <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:subject><rdf:Seq><rdf:li>Landscape</rdf:li></rdf:Seq></dc:subject>
              </rdf:Description>
            {RdfClose}
            """;

        var result = Parse(packet);

        Assert.AreEqual(3, result.Rating);
        Assert.AreEqual("Landscape", string.Join(',', result.Keywords));
    }

    [TestMethod]
    public void Parse_BlankAndDuplicateKeywords_TrimsAndDeduplicates()
    {
        var result = Parse(Description(string.Empty, """
            <dc:subject><rdf:Bag>
              <rdf:li> Selected </rdf:li>
              <rdf:li>   </rdf:li>
              <rdf:li>selected</rdf:li>
              <rdf:li>Startseite</rdf:li>
            </rdf:Bag></dc:subject>
            """));

        Assert.AreEqual("Selected,Startseite", string.Join(',', result.Keywords));
    }

    [TestMethod]
    public void Parse_BlankTitle_ReturnsNullTitle()
    {
        var result = Parse(TestImageGenerator.CreateXmpPacket(title: "   "));

        Assert.IsNull(result.Title);
    }

    [TestMethod]
    public void Parse_NoDescriptiveFields_ReturnsEmptyMetadata()
    {
        var result = Parse(Description("xmp:CreatorTool=\"Capture One\"", string.Empty));

        Assert.IsNull(result.Title);
        Assert.IsNull(result.Description);
        Assert.IsEmpty(result.Keywords);
        Assert.IsNull(result.Rating);
    }

    [TestMethod]
    public void Parse_TrailingNulPadding_IsIgnored()
    {
        var bytes = Encoding.UTF8.GetBytes(TestImageGenerator.CreateXmpPacket(rating: 2)).Concat(new byte[16]).ToArray();

        var result = XmpMetadataParser.Parse(bytes);

        Assert.AreEqual(2, result.Rating);
    }

    [TestMethod]
    public void Parse_MalformedXml_ThrowsXmlException() => Assert.ThrowsExactly<XmlException>(() => Parse("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF>"));

    [TestMethod]
    public void Parse_DocumentTypeDefinition_IsRejected()
    {
        var packet = """
            <?xml version="1.0"?>
            <!DOCTYPE lol [<!ENTITY lol "lol">]>
            <x:xmpmeta xmlns:x="adobe:ns:meta/">&lol;</x:xmpmeta>
            """;

        Assert.ThrowsExactly<XmlException>(() => Parse(packet));
    }

    [TestMethod]
    public void Parse_OversizedPacket_ThrowsInvalidDataException()
    {
        var packet = new byte[XmpMetadataParser.MaxPacketBytes + 1];

        Assert.ThrowsExactly<InvalidDataException>(() => XmpMetadataParser.Parse(packet));
    }
}
