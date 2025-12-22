namespace Spectara.Revela.Plugin.Serve.Tests;

[TestClass]
public sealed class StaticFileServerTests
{
    [TestMethod]
    [DataRow(".html", "text/html; charset=utf-8")]
    [DataRow(".HTML", "text/html; charset=utf-8")]
    [DataRow(".css", "text/css; charset=utf-8")]
    [DataRow(".js", "text/javascript; charset=utf-8")]
    [DataRow(".json", "application/json; charset=utf-8")]
    [DataRow(".avif", "image/avif")]
    [DataRow(".webp", "image/webp")]
    [DataRow(".jpg", "image/jpeg")]
    [DataRow(".jpeg", "image/jpeg")]
    [DataRow(".png", "image/png")]
    [DataRow(".svg", "image/svg+xml")]
    [DataRow(".ico", "image/x-icon")]
    [DataRow(".woff2", "font/woff2")]
    [DataRow(".unknown", "application/octet-stream")]
    public void GetMimeType_ReturnsCorrectType(string extension, string expectedMimeType)
    {
        // Act
        var result = StaticFileServer.GetMimeType(extension);

        // Assert
        Assert.AreEqual(expectedMimeType, result);
    }

    [TestMethod]
    public void GetMimeType_IsCaseInsensitive()
    {
        // Arrange & Act
        var lowercase = StaticFileServer.GetMimeType(".html");
        var uppercase = StaticFileServer.GetMimeType(".HTML");
        var mixed = StaticFileServer.GetMimeType(".HtMl");

        // Assert
        Assert.AreEqual(lowercase, uppercase);
        Assert.AreEqual(lowercase, mixed);
    }

    [TestMethod]
    public void GetMimeType_UnknownExtension_ReturnsOctetStream()
    {
        // Act
        var result = StaticFileServer.GetMimeType(".xyz123");

        // Assert
        Assert.AreEqual("application/octet-stream", result);
    }

    [TestMethod]
    public void GetMimeType_EmptyExtension_ReturnsOctetStream()
    {
        // Act
        var result = StaticFileServer.GetMimeType("");

        // Assert
        Assert.AreEqual("application/octet-stream", result);
    }
}
