namespace CohEngine.Core.Data;

/// <summary>The database-level set-bonus inputs.</summary>
/// <param name="Sets">The set definitions by set index.</param>
/// <param name="Powers">The referenced bonus powers by global power id.</param>
public sealed record SetBonusDb(IReadOnlyDictionary<int, EnhancementSetDef> Sets, IReadOnlyDictionary<int, SetBonusPower> Powers);
