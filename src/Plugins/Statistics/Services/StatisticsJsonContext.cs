using System.Text.Json.Serialization;

using Spectara.Revela.Plugins.Statistics.Models;

namespace Spectara.Revela.Plugins.Statistics.Services;

/// <summary>
/// Source-generated JSON serializer context for statistics types.
/// Enables trimming and AOT compatibility.
/// </summary>
[JsonSerializable(typeof(SiteStatistics))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class StatisticsJsonContext : JsonSerializerContext;
