using System.Text.Json.Serialization;

using Spectara.Revela.Plugins.Calendar.Models;

namespace Spectara.Revela.Plugins.Calendar.Commands;

/// <summary>
/// Source-generated JSON serializer context for calendar types.
/// Enables trimming and AOT compatibility.
/// </summary>
[JsonSerializable(typeof(CalendarData))]
internal sealed partial class CalendarJsonContext : JsonSerializerContext;
