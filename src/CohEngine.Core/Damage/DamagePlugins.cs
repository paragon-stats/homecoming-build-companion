using CohEngine.Core.Pets;

namespace CohEngine.Core.Damage;

/// <summary>The default source and modifier registries, in the order the Python port registers them.</summary>
public static class DamagePlugins
{
    /// <summary>Gets the sources: the non-chain ones in registration order, then the chain source that fills the remainder.</summary>
    public static IReadOnlyList<SourceRegistration> Sources { get; } =
    [
        new(DamageSources.ChainSource, FillsRemainder: true),
        new(DamageSources.AuraSource, FillsRemainder: false),
        new(DamageSources.PatchSource, FillsRemainder: false),
        new(DamageSources.BuffCastSource, FillsRemainder: false),
        new(PetDamage.PetSource, FillsRemainder: false),
    ];

    /// <summary>Gets the modifiers: the archetype rules, then click buffs and target debuffs.</summary>
    public static IReadOnlyList<Func<DamageContext, ModifierResult?>> Modifiers { get; } =
    [
        .. AtModifiers.Functions,
        DamageModifiers.ClickBuffModifier,
        DamageModifiers.TargetDebuffModifier,
    ];
}
