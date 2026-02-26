namespace Spectara.Revela.Core.Logging;

/// <summary>
/// High-performance logging for NupkgExtractor using source-generated extension methods.
/// </summary>
internal static partial class NupkgExtractorLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting package: {PackageId} v{Version}")]
    public static partial void ExtractingPackage(this ILogger<NupkgExtractor> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No compatible libraries found in package {PackageId}")]
    public static partial void NoCompatibleLibs(this ILogger<NupkgExtractor> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Extracted {FileName} to {TargetDir}")]
    public static partial void ExtractedFile(this ILogger<NupkgExtractor> logger, string fileName, string targetDir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No files extracted from package {PackageId}")]
    public static partial void NoFilesExtracted(this ILogger<NupkgExtractor> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Package {PackageId} extracted successfully ({FileCount} file(s))")]
    public static partial void PackageExtracted(this ILogger<NupkgExtractor> logger, string packageId, int fileCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created metadata file: {MetadataPath}")]
    public static partial void MetadataCreated(this ILogger<NupkgExtractor> logger, string metadataPath);
}
