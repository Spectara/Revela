using Spectara.Revela.Features.Packages.Services;

namespace Spectara.Revela.Features.Packages.Logging;

/// <summary>
/// High-performance logging for NupkgExtractor using source-generated extension methods.
/// </summary>
internal static partial class NupkgExtractorLogging
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting package: {PackageId} v{Version}")]
    public static partial void ExtractingPackage(this ILogger<NupkgExtractor> logger, string packageId, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No compatible libraries found in package {PackageId}")]
    public static partial void NoCompatibleLibs(this ILogger<NupkgExtractor> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Package {PackageId} does not declare package type {RequiredPackageType}; nothing was installed")]
    public static partial void WrongPackageType(this ILogger<NupkgExtractor> logger, string packageId, string requiredPackageType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Extracted {FileName} to {TargetDir}")]
    public static partial void ExtractedFile(this ILogger<NupkgExtractor> logger, string fileName, string targetDir);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No files extracted from package {PackageId}")]
    public static partial void NoFilesExtracted(this ILogger<NupkgExtractor> logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Package {PackageId} extracted successfully ({FileCount} file(s))")]
    public static partial void PackageExtracted(this ILogger<NupkgExtractor> logger, string packageId, int fileCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped suspicious nupkg entry with unsafe path: {EntryName}")]
    public static partial void SkippedSuspiciousEntry(this ILogger<NupkgExtractor> logger, string entryName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Rejected package with invalid package ID '{PackageId}' in .nuspec")]
    public static partial void InvalidPackageId(this ILogger<NupkgExtractor> logger, string packageId);
}
