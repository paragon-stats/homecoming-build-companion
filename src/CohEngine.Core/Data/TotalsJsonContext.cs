using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the totals family.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MidsTotals))]
[JsonSerializable(typeof(IReadOnlyList<EnhancedPower>))]
[JsonSerializable(typeof(ServerData))]
[JsonSerializable(typeof(EngineConfig))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, int>>))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class TotalsJsonContext : JsonSerializerContext;
