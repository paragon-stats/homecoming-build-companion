namespace CohEngine.Core.Data;

/// <summary>A build's <c>set_bonus_virtual_power.json</c>.</summary>
public sealed record SetBonusVirtualPower
{
    /// <summary>Gets the effect count Mids reports.</summary>
    public required int EffectCount { get; init; }

    /// <summary>Gets the effects.</summary>
    public required IReadOnlyList<SetBonusEffect> Effects { get; init; }
}
