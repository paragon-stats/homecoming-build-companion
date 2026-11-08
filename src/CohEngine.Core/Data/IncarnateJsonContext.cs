using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the incarnate family.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(IReadOnlyList<Incarnate>))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class IncarnateJsonContext : JsonSerializerContext;
