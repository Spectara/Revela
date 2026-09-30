using System.IO.Compression;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Spectara.Revela.Tests.Commands.Packages;

/// <summary>
/// Creates synthetic .nupkg files for package-management tests.
/// </summary>
internal static class TestPackageFactory
{
    /// <summary>
    /// Creates a well-formed package via <see cref="PackageBuilder"/> in <paramref name="directory"/>.
    /// </summary>
    public static string CreatePackage(string directory, string id, string version, string packageType = "RevelaPlugin")
    {
        _ = Directory.CreateDirectory(directory);
        var libraryPath = Path.Combine(directory, $"{id}.{version}.dll");
        File.WriteAllBytes(libraryPath, [1, 2, 3, 4]);

        var package = new PackageBuilder
        {
            Id = id,
            Version = new NuGetVersion(version),
            Description = "Synthetic test package"
        };
        package.Authors.Add("Test");
        package.PackageTypes.Add(new PackageType(packageType, new Version(0, 0)));
        package.Files.Add(new PhysicalPackageFile
        {
            SourcePath = libraryPath,
            TargetPath = $"lib/net10.0/{id}.dll"
        });

        var packagePath = Path.Combine(directory, $"{id}.{version}.nupkg");
        using (var stream = File.Create(packagePath))
        {
            package.Save(stream);
        }

        File.Delete(libraryPath);
        return packagePath;
    }

    /// <summary>
    /// Creates a hand-crafted package whose nuspec ID bypasses <see cref="PackageBuilder"/> validation.
    /// </summary>
    public static string CreateRawPackage(string packagePath, string nuspecId, string version = "1.0.0")
    {
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        var nuspec = archive.CreateEntry("package.nuspec");
        using (var writer = new StreamWriter(nuspec.Open()))
        {
            writer.Write(
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata>
                    <id>{nuspecId}</id>
                    <version>{version}</version>
                    <authors>Test</authors>
                    <description>Crafted test package</description>
                  </metadata>
                </package>
                """);
        }

        var library = archive.CreateEntry("lib/net10.0/Payload.dll");
        using (var stream = library.Open())
        {
            stream.Write([1, 2, 3, 4]);
        }

        return packagePath;
    }
}
