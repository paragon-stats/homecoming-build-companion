namespace CohEngine.Mcp;

/// <summary>The <c>power_lookup</c> tool's result: the power, or an explicit unknown.</summary>
/// <param name="DataVersion">The hash of the data files the answer was computed from, or <c>unknown</c> when it read none.</param>
/// <param name="Power">The power; absent when it is unknown.</param>
/// <param name="Unknown">Why the engine has no value, as an <c>E&lt;NN&gt;</c> message; absent when the power was found.</param>
/// <param name="UnknownFields">The fields of a found power the dump leaves out, by their result names, which are omitted from <c>power</c>; absent when none.</param>
public sealed record PowerResult(string DataVersion, PowerView? Power, string? Unknown, IReadOnlyList<string>? UnknownFields = null);
