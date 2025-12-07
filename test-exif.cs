using System.Text.Json;
using Spectara.Revela.Features.Generate.Models;

var json = """
{
    "exif": {
        "make": "Sony",
        "model": "α 7 IV",
        "lensModel": "FE 12-24mm",
        "dateTaken": "2024-01-01T00:01:33Z",
        "fNumber": 4.5,
        "exposureTime": 0.4,
        "iso": 1600,
        "focalLength": 24
    }
}
""";

var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var entry = JsonSerializer.Deserialize<TestEntry>(json, options);
Console.WriteLine($"Exif: {entry?.Exif}");
Console.WriteLine($"FNumber: {entry?.Exif?.FNumber}");

public class TestEntry { public ExifData? Exif { get; init; } }
