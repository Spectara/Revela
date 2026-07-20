using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Provides page-local rendering callbacks for inline-gallery blocks.
/// </summary>
internal sealed record GalleryBlockContext(
    string SourcePath,
    IReadOnlyList<Image> PageImages,
    Action<int> EnsureGalleryGrid,
    Func<IReadOnlyList<Image>, int, string> RenderGalleryGrid,
    Action MarkInlineGallery,
    Action<string> ReportWarning);
