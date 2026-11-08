using CohEngine.Core.Data;
using CohEngine.Core.Totals;
using CohEngine.Core.Totals.Phases;

namespace CohEngine.Core.Tests.Totals;

/// <summary>The helpers of <c>test_base_totals.py</c>: a context over the tiny tables and the passes run on one power.</summary>
internal static class TotalsMake
{
    public static readonly EngineConfig Pve = new() { Suppression = 0, DisablePve = false, ForceLevel = 50, ScalingToHit = 0.75 };

    public static readonly EngineConfig Pvp = Pve with { DisablePve = true };

    public static EnumMaps Enums => Fixtures.Store.Enums;

    public static MagContext Context(EngineConfig? config = null)
    {
        return new MagContext
        {
            Mods = EffectMagnitudeTests.TinyMods,
            Classes = EffectMagnitudeTests.TinyClasses,
            ArchetypeIndex = 0,
            Config = config ?? Pve,
            EnhanceNames = PerPowerEnhancementMathPhase.EnhanceNames(Enums),
            Enums = Enums,
        };
    }

    public static BuffsX BuffPass(Power power, EngineConfig? config = null, double prevHpMax = 0.0, bool globalAccSource = false)
    {
        BuffsX buffs = new(Enums);
        SelfBuffBucketPhase.Apply(power, buffs, Context(config), prevHpMax, globalAccSource);
        return buffs;
    }

    public static BuffsX EnhancePass(Power power)
    {
        BuffsX buffs = new(Enums);
        EnhancementBucketPhase.Apply(power, buffs, Context());
        return buffs;
    }

    public static int Type(string name)
    {
        return Enums.Maps["eEffectType"][name];
    }

    public static int Stat(string name)
    {
        return Enums.Maps["eStatType"][name];
    }

    public static int Mez(string name)
    {
        return Enums.Maps["eMez"][name];
    }

    public static int Damage(string name)
    {
        return Enums.Maps["eDamage"][name];
    }

    public static Pipeline.Database TinyData(EngineConfig? config = null, ArchetypeDb? classes = null)
    {
        return new Pipeline.Database(EffectMagnitudeTests.TinyMods, classes ?? EffectMagnitudeTests.TinyClasses, Enums, config ?? Pve, Fixtures.Store.ServerData);
    }

    public static BaseTotals Compute(IReadOnlyList<Power> powers, Pipeline.BuildOptions? options = null, EngineConfig? config = null, ArchetypeDb? classes = null)
    {
        return Pipeline.ComputeBaseTotals(powers, "Class_Test", TinyData(config, classes), options);
    }
}
