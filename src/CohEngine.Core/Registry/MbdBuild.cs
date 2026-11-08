using CohEngine.Core.Data;

namespace CohEngine.Core.Registry;

/// <summary>A <c>.mbd</c> build resolved to the inputs the registries fold over.</summary>
/// <param name="Powers">The build's powers.</param>
/// <param name="Slots">The build's slots by build index.</param>
/// <param name="Format">The file facts that arm the <c>H-FMT</c> rules.</param>
public sealed record MbdBuild(IReadOnlyList<Power> Powers, IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> Slots, MbdFormat Format);
