using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the enhancement family.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<EnhancementRecord>))]
[JsonSerializable(typeof(IReadOnlyList<BuildSlots>))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class EnhancementJsonContext : JsonSerializerContext;
