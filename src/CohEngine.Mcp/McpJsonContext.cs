using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Mcp;

/// <summary>Source-generated JSON metadata for every tool result, so serialization never reflects.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RulesResult))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; every tool that uses it is covered.")]
internal sealed partial class McpJsonContext : JsonSerializerContext;
