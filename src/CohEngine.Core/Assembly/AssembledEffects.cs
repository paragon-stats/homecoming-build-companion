using CohEngine.Core.Data;

namespace CohEngine.Core.Assembly;

/// <summary>One build power after assembly: its effects, and the aspects it ignores once pet absorption copied them.</summary>
/// <param name="Effects">The assembled effects, re-indexed so each index is the list position.</param>
/// <param name="IgnoreEnh">The eEnhance names the power ignores.</param>
internal sealed record AssembledEffects(IReadOnlyList<Effect> Effects, IReadOnlySet<string> IgnoreEnh);
