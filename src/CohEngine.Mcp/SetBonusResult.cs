using CohEngine.Core.Registry;

namespace CohEngine.Mcp;

/// <summary>The <c>set_bonus_lookup</c> tool's result.</summary>
/// <param name="DataVersion">The engine build the answer came from.</param>
/// <param name="Set">The set and its resolved bonuses.</param>
public sealed record SetBonusResult(string DataVersion, SetBonusView Set);
