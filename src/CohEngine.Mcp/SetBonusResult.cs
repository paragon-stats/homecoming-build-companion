using CohEngine.Core.Registry;

namespace CohEngine.Mcp;

/// <summary>The <c>set_bonus_lookup</c> tool's result: the set and its resolved bonuses, or an explicit unknown.</summary>
/// <param name="DataVersion">The hash of the data files the answer was computed from, or <c>unknown</c> when it read none.</param>
/// <param name="Set">The set; absent when it is unknown.</param>
/// <param name="Unknown">Why the engine has no value, as an <c>E&lt;NN&gt;</c> message; absent when the set was found.</param>
public sealed record SetBonusResult(string DataVersion, SetBonusView? Set, string? Unknown);
