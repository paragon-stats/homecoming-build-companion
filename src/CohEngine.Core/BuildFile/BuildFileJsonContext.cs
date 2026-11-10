using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CohEngine.Core.BuildFile;

/// <summary>Source-generated JSON metadata for the build-file family; keys match exactly, as the Python reader's do.</summary>
[JsonSerializable(typeof(IReadOnlyList<PowerIndexRecord>))]
[JsonSerializable(typeof(IReadOnlyList<EnhancementIndexRecord>))]
[JsonSerializable(typeof(IReadOnlyList<EnhancementDbRecord>))]
[JsonSerializable(typeof(IReadOnlyList<EnhancementSetRecord>))]
[JsonSerializable(typeof(IReadOnlyList<PowerDbRecord>))]
[JsonSerializable(typeof(MbdDocument))]
[ExcludeFromCodeCoverage(Justification = "Source-generated serializer metadata; the loaders that use it are covered.")]
internal sealed partial class BuildFileJsonContext : JsonSerializerContext;
