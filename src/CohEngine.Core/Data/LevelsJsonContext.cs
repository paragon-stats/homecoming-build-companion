using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the level schedule.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LevelsFile))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class LevelsJsonContext : JsonSerializerContext;
