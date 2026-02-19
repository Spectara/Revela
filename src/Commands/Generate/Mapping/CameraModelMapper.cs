using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Generate.Mapping;

/// <summary>
/// Maps camera model codes to user-friendly names
/// </summary>
/// <remarks>
/// Supports:
/// - Built-in defaults for Sony ILCE → α series and common manufacturers
/// - Custom mappings via project.json (build.cameras section)
///
/// Configuration example in project.json:
/// <code>
/// {
///   "generate": {
///     "cameras": {
///       "models": { "CUSTOM-CODE": "Friendly Name" },
///       "makes": { "CUSTOM-MAKE": "Nice Make" }
///     }
///   }
/// }
/// </code>
/// </remarks>
internal sealed class CameraModelMapper
{
    private readonly Dictionary<string, string> modelMappings;
    private readonly Dictionary<string, string> makeMappings;

    /// <summary>
    /// Default Sony ILCE to α series mappings
    /// </summary>
    private static readonly Dictionary<string, string> DefaultModelMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // α 7 series (full frame, general purpose)
        ["ILCE-7M4"] = "α 7 IV",
        ["ILCE-7M3"] = "α 7 III",
        ["ILCE-7M2"] = "α 7 II",
        ["ILCE-7"] = "α 7",
        // α 7R series (full frame, high resolution)
        ["ILCE-7RM5"] = "α 7R V",
        ["ILCE-7RM4A"] = "α 7R IVA",
        ["ILCE-7RM4"] = "α 7R IV",
        ["ILCE-7RM3A"] = "α 7R IIIA",
        ["ILCE-7RM3"] = "α 7R III",
        ["ILCE-7RM2"] = "α 7R II",
        ["ILCE-7R"] = "α 7R",
        // α 7S series (full frame, low light/video)
        ["ILCE-7SM3"] = "α 7S III",
        ["ILCE-7SM2"] = "α 7S II",
        ["ILCE-7S"] = "α 7S",
        // α 7C series (compact full frame)
        ["ILCE-7CM2"] = "α 7C II",
        ["ILCE-7CR"] = "α 7CR",
        ["ILCE-7C"] = "α 7C",
        // α 9 series (professional sports)
        ["ILCE-9M3"] = "α 9 III",
        ["ILCE-9M2"] = "α 9 II",
        ["ILCE-9"] = "α 9",
        // α 1 (flagship)
        ["ILCE-1"] = "α 1",
        // α 6000 series (APS-C)
        ["ILCE-6700"] = "α 6700",
        ["ILCE-6600"] = "α 6600",
        ["ILCE-6500"] = "α 6500",
        ["ILCE-6400"] = "α 6400",
        ["ILCE-6300"] = "α 6300",
        ["ILCE-6100"] = "α 6100",
        ["ILCE-6000"] = "α 6000",
        // ZV series (vlogging)
        ["ZV-E10M2"] = "ZV-E10 II",
        ["ZV-E10"] = "ZV-E10",
        ["ZV-E1"] = "ZV-E1"
    };

    /// <summary>
    /// Default manufacturer name normalizations
    /// </summary>
    private static readonly Dictionary<string, string> DefaultMakeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SONY"] = "Sony",
        ["CANON"] = "Canon",
        ["NIKON CORPORATION"] = "Nikon",
        ["NIKON"] = "Nikon",
        ["FUJIFILM"] = "Fujifilm",
        ["OLYMPUS CORPORATION"] = "Olympus",
        ["OLYMPUS"] = "Olympus",
        ["OM Digital Solutions"] = "OM System",
        ["PANASONIC"] = "Panasonic",
        ["Panasonic"] = "Panasonic",
        ["LEICA"] = "Leica",
        ["RICOH IMAGING COMPANY, LTD."] = "Pentax",
        ["PENTAX"] = "Pentax",
        ["HASSELBLAD"] = "Hasselblad",
        ["DJI"] = "DJI",
        ["Apple"] = "Apple",
        ["samsung"] = "Samsung",
        ["SAMSUNG"] = "Samsung",
        ["Google"] = "Google",
        ["HUAWEI"] = "Huawei"
    };

    /// <summary>
    /// Create mapper with default mappings and optional custom config
    /// </summary>
    public CameraModelMapper(IOptionsMonitor<GenerateConfig> config)
    {
        // Start with defaults
        modelMappings = new Dictionary<string, string>(DefaultModelMappings, StringComparer.OrdinalIgnoreCase);
        makeMappings = new Dictionary<string, string>(DefaultMakeMappings, StringComparer.OrdinalIgnoreCase);

        // Merge custom mappings from config (custom overrides defaults)
        var cameras = config.CurrentValue.Cameras;

        foreach (var (key, value) in cameras.Models)
        {
            modelMappings[key] = value;
        }

        foreach (var (key, value) in cameras.Makes)
        {
            makeMappings[key] = value;
        }
    }

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
    /// Map camera model code to user-friendly name
    /// </summary>
    public string? MapModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        // Check for exact match first
        if (modelMappings.TryGetValue(model, out var mapped))
        {
            return mapped;
        }

        // Return original if no mapping found
        return model;
    }

    /// <summary>
    /// Get user-friendly camera manufacturer name
    /// </summary>
    public string? MapMake(string? make)
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            return make;
        }

        // Check for exact match
        if (makeMappings.TryGetValue(make, out var mapped))
        {
            return mapped;
        }

        // Return original if no mapping found
        return make;
    }

    /// <summary>
    /// Clean lens model name by removing parenthetical info
    /// </summary>
    /// <remarks>
    /// Example: "Sony FE 50mm F1.8 (SEL50F18F)" → "Sony FE 50mm F1.8"
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
}
