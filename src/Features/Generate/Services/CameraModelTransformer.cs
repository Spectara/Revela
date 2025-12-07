namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Transforms camera model codes to user-friendly names
/// </summary>
/// <remarks>
/// Inspired by expose.sh camera model transformations (line 683):
/// - Sony ILCE codes → α series names
/// - Removes parenthetical info from lens names
///
/// Examples:
/// - "ILCE-7M4" → "α 7 IV"
/// - "ILCE-7RM5" → "α 7R V"
/// - "Sony FE 50mm F1.8 (SEL50F18F)" → "Sony FE 50mm F1.8"
/// </remarks>
public static class CameraModelTransformer
{
    /// <summary>
    /// Extract the actual value from NetVips EXIF string format.
    /// </summary>
    /// <remarks>
    /// NetVips returns EXIF strings in format: "VALUE (VALUE, TYPE, N components, M bytes)"
    /// Example: "SONY (SONY, ASCII, 5 components, 5 bytes)" → "SONY"
    /// Example: "ILCE-7M4 (ILCE-7M4, ASCII, 9 components, 9 bytes)" → "ILCE-7M4"
    /// </remarks>
    public static string? ExtractExifValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        // Find the first " (" pattern which marks start of metadata
        var metaStart = raw.IndexOf(" (", StringComparison.Ordinal);
        if (metaStart > 0)
        {
            return raw[..metaStart].Trim();
        }

        return raw.Trim();
    }

    /// <summary>
    /// Transform camera model code to user-friendly name
    /// </summary>
    public static string? TransformModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        // Sony α (Alpha) series transformations
        // Based on expose.sh line 683
        var transformed = model
            // α 7 series (full frame, general purpose)
            .Replace("ILCE-7M4", "α 7 IV", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7M3", "α 7 III", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7M2", "α 7 II", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7", "α 7", StringComparison.OrdinalIgnoreCase)
            // α 7R series (full frame, high resolution)
            .Replace("ILCE-7RM5", "α 7R V", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7RM4", "α 7R IV", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7RM3", "α 7R III", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7RM2", "α 7R II", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7R", "α 7R", StringComparison.OrdinalIgnoreCase)
            // α 7S series (full frame, low light)
            .Replace("ILCE-7SM3", "α 7S III", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7SM2", "α 7S II", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7S", "α 7S", StringComparison.OrdinalIgnoreCase)
            // α 7C series (compact full frame)
            .Replace("ILCE-7CM2", "α 7C II", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7C", "α 7C", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-7CR", "α 7CR", StringComparison.OrdinalIgnoreCase)
            // α 9 series (professional sports)
            .Replace("ILCE-9M3", "α 9 III", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-9M2", "α 9 II", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-9", "α 9", StringComparison.OrdinalIgnoreCase)
            // α 1 (flagship)
            .Replace("ILCE-1", "α 1", StringComparison.OrdinalIgnoreCase)
            // α 6000 series (APS-C)
            .Replace("ILCE-6700", "α 6700", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-6600", "α 6600", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-6500", "α 6500", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-6400", "α 6400", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-6300", "α 6300", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-6100", "α 6100", StringComparison.OrdinalIgnoreCase)
            .Replace("ILCE-6000", "α 6000", StringComparison.OrdinalIgnoreCase);

        return transformed;
    }

    /// <summary>
    /// Clean lens model name by removing parenthetical info
    /// </summary>
    /// <remarks>
    /// Based on expose.sh line 681: sed 's/ (.*)//'
    ///
    /// Example:
    /// "Sony FE 50mm F1.8 (SEL50F18F)" → "Sony FE 50mm F1.8"
    /// </remarks>
    public static string? CleanLensModel(string? lensModel)
    {
        if (string.IsNullOrWhiteSpace(lensModel))
        {
            return lensModel;
        }

        // Remove everything from first opening parenthesis to end
        var parenIndex = lensModel.IndexOf('(', StringComparison.Ordinal);
        if (parenIndex > 0)
        {
            return lensModel[..parenIndex].TrimEnd();
        }

        return lensModel;
    }

    /// <summary>
    /// Get user-friendly camera manufacturer name
    /// </summary>
    public static string? TransformMake(string? make)
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            return make;
        }

        // Normalize common variations
        return make
            .Replace("SONY", "Sony", StringComparison.OrdinalIgnoreCase)
            .Replace("CANON", "Canon", StringComparison.OrdinalIgnoreCase)
            .Replace("NIKON", "Nikon", StringComparison.OrdinalIgnoreCase)
            .Replace("FUJIFILM", "Fujifilm", StringComparison.OrdinalIgnoreCase)
            .Replace("OLYMPUS", "Olympus", StringComparison.OrdinalIgnoreCase)
            .Replace("PANASONIC", "Panasonic", StringComparison.OrdinalIgnoreCase);
    }
}
