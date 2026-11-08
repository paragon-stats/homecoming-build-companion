using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.Data;

/// <summary>Source-generated JSON metadata for the assembly family.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<AssemblyPower>))]
[JsonSerializable(typeof(AssemblyEntitiesFile))]
[JsonSerializable(typeof(IReadOnlyList<PowerAssemblyRecord>))]
[JsonSerializable(typeof(IReadOnlyList<AssembledPower>))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
public sealed partial class AssemblyJsonContext : JsonSerializerContext;
