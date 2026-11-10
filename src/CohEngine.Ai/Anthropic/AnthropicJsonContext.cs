using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Ai.Anthropic;

/// <summary>Source-generated JSON metadata for the Messages API bodies and stream events.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AnthropicRequest))]
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(AnthropicErrorEnvelope))]
[JsonSerializable(typeof(AnthropicStreamEvent))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the client that uses it is covered.")]
internal sealed partial class AnthropicJsonContext : JsonSerializerContext;
