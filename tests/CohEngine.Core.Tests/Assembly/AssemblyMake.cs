using CohEngine.Core.Assembly;
using CohEngine.Core.Data;

namespace CohEngine.Core.Tests.Assembly;

/// <summary>The hand-made records of <c>test_assembly.py</c>, with <c>conftest.py</c>'s assembly defaults.</summary>
internal static class AssemblyMake
{
    public const int Class = 3;

    public static readonly AssemblyEntities NoEntities = new(new Dictionary<int, AssemblyEntity>(), new Dictionary<int, AssemblyPowerset>());

    public static readonly SetBonusDb SetDb = new(
        new Dictionary<int, EnhancementSetDef>
        {
            [7] = new()
            {
                Nid = 7,
                Uid = "Test_Set",
                SetType = 0,
                SetTypeName = "Defense",
                Enhancements = [50, 51, 52, 56],
                Bonus = [],
                SpecialBonus = [NoBonus, new BonusItem { Slotted = 0, PvMode = 0, Index = [900] }, NoBonus, NoBonus],
            },
            [8] = new()
            {
                Nid = 8,
                Uid = "Pet_Set",
                SetType = 1,
                SetTypeName = "PetDamage",
                Enhancements = [60],
                Bonus = [],
                SpecialBonus = [NoBonus],
            },
        },
        new Dictionary<int, SetBonusPower>());

    public static readonly Dictionary<int, EnhancementRecord> EnhDb = new()
    {
        [50] = Enh(50, 500),
        [51] = Enh(51, 500),
        [52] = Enh(52, 500, isProc: true, effects: true),
        [53] = Enh(53, -1),
        [54] = Enh(54, 500, nidSet: -1),
        [55] = Enh(55, 500),
        [56] = Enh(56, 501),
        [57] = Enh(57, 500, nidSet: 99),
        [58] = Enh(58, 404),
        [60] = Enh(60, 500, nidSet: 8),
    };

    private static BonusItem NoBonus => new() { Slotted = 0, PvMode = 0, Index = [] };

    public static AssemblyPower Power(
        Effect[] effects,
        int nid = 1,
        string fullName = "Test.Test_Set.Test_Power",
        string powerType = "Toggle",
        string powerSetType = "Primary",
        string[]? affected = null,
        string[]? autoHit = null,
        double activatePeriod = 0.0,
        int[]? enhancements = null,
        bool hasGrant = false,
        bool hasOverride = false,
        bool absorbEffects = false,
        bool absorbAttributes = false,
        string[]? ignoreEnh = null,
        double aoeModifier = 1.0,
        double baseRechargeTime = 0.0)
    {
        return new AssemblyPower(
            new HashSet<string>(affected ?? ["Caster"], StringComparer.Ordinal),
            new HashSet<string>(autoHit ?? ["Caster"], StringComparer.Ordinal),
            new HashSet<int>(enhancements ?? []),
            new HashSet<string>(ignoreEnh ?? [], StringComparer.Ordinal))
        {
            Nid = nid,
            FullName = fullName,
            PowerType = powerType,
            PowerSetType = powerSetType,
            ActivatePeriod = activatePeriod,
            RechargeTime = 0.0,
            BaseRechargeTime = baseRechargeTime,
            CastTime = 0.0,
            AoeModifier = aoeModifier,
            HasGrantPowerEffect = hasGrant,
            HasPowerOverrideEffect = hasOverride,
            AbsorbSummonEffects = absorbEffects,
            AbsorbSummonAttributes = absorbAttributes,
            Effects = effects,
        };
    }

    public static BuildPowerAssembly Build(AssemblyPower power, int buildIndex = 0, bool procInclude = false, SubPower[]? subPowers = null)
    {
        return new BuildPowerAssembly(buildIndex, procInclude, subPowers ?? [], power);
    }

    public static BuildPowerAssembly Build(params Effect[] effects)
    {
        return Build(Power(effects));
    }

    public static AssemblyEntities Entities(params object[] records)
    {
        return new AssemblyEntities(
            records.OfType<AssemblyEntity>().ToDictionary(entity => entity.Nid),
            records.OfType<AssemblyPowerset>().ToDictionary(powerset => powerset.Nid));
    }

    public static AssemblyEntity Entity(int nid, int[] powersets, int classNid = 18, string? uid = null, string[][]? firstPowersetEffectTypes = null)
    {
        return new AssemblyEntity([.. (firstPowersetEffectTypes ?? []).Select(types => (ISet<string>)new HashSet<string>(types, StringComparer.Ordinal))])
        {
            Nid = nid,
            Uid = uid ?? $"Pets_{nid}",
            ClassNid = classNid,
            PowersetNids = powersets,
        };
    }

    public static AssemblyPowerset Powerset(int nid, params int[] powers)
    {
        return new AssemblyPowerset { Nid = nid, FullName = $"Pets.Set_{nid}", SetType = "Pet", PowerNids = powers };
    }

    public static SlotRef Slot(int enh)
    {
        return new SlotRef { Level = 0, IsInherent = false, Enh = enh, Grade = 0, IoLevel = 49, RelativeLevel = 4 };
    }

    public static bool Never(Effect effect)
    {
        throw new InvalidOperationException($"conditions called for effect {effect.Index}");
    }

    public static IReadOnlyList<Effect> Assemble(
        BuildPowerAssembly buildPower,
        AssemblyPower[]? powers = null,
        SlotRef[]? slots = null,
        Func<Effect, bool>? conditions = null,
        AssemblyEntities? entities = null)
    {
        return PowerAssembler.AssemblePower(
            buildPower,
            slots ?? [],
            EnhDb,
            SetDb,
            (powers ?? []).ToDictionary(power => power.Nid),
            entities ?? NoEntities,
            Class,
            conditions ?? Never).Effects;
    }

    public static string[] Types(IEnumerable<Effect> effects)
    {
        return [.. effects.Select(effect => effect.EffectType)];
    }

    private static EnhancementRecord Enh(int nid, int powerNid, int nidSet = 7, bool isProc = false, bool effects = false)
    {
        return new EnhancementRecord
        {
            Nid = nid,
            StaticIndex = nid,
            TypeName = "SetO",
            Superior = false,
            Effects = effects ? [new EnhEffect { Enhance = "Damage", EnhanceSubId = 0, Schedule = 0, Multiplier = 1.0, BuffMode = "Any" }] : [],
            NidSet = nidSet,
            IsProc = isProc,
            PowerNid = powerNid,
        };
    }
}
