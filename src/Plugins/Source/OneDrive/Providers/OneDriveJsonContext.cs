using System.Text.Json.Serialization;

namespace Spectara.Revela.Plugins.Source.OneDrive.Providers;

/// <summary>
/// Source-generated JSON serializer context for OneDrive API types.
/// Enables trimming and AOT compatibility.
/// </summary>
[JsonSerializable(typeof(SharedLinkProvider.BadgerTokenRequest))]
[JsonSerializable(typeof(SharedLinkProvider.BadgerTokenResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class OneDriveJsonContext : JsonSerializerContext;
