using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Registry;

/// <summary>Source-generated JSON metadata for <c>enhancements.json</c>.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<EnhancementLegality>))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loader that uses it is covered.")]
public sealed partial class EnhancementLegalityJsonContext : JsonSerializerContext;
