using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using CohEngine.Core.Registry;

namespace CohEngine.Mcp;

/// <summary>Source-generated JSON metadata for every tool result, so serialization never reflects.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RulesResult))]
[JsonSerializable(typeof(DiagnosticsResult))]
[JsonSerializable(typeof(ScoreResult))]
[JsonSerializable(typeof(TotalsResult))]
[JsonSerializable(typeof(SetBonusResult))]
[JsonSerializable(typeof(PowerResult))]
[JsonSerializable(typeof(SetBonusView))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; every tool that uses it is covered.")]
internal sealed partial class McpJsonContext : JsonSerializerContext;
