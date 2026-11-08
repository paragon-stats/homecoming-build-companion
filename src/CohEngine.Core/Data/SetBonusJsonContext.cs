using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the set-bonus family.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<EnhancementSetDef>))]
[JsonSerializable(typeof(IReadOnlyList<SetBonusPower>))]
[JsonSerializable(typeof(SetBonusVirtualPower))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class SetBonusJsonContext : JsonSerializerContext;
