using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the power and effect family.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<Power>))]
[JsonSerializable(typeof(IReadOnlyList<Effect>))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class EffectJsonContext : JsonSerializerContext;
