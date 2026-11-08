using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals.Incarnate;
using CohEngine.Core.Totals.Phases;

using IncarnatePower = CohEngine.Core.Data.Incarnate;

namespace CohEngine.Core.Totals;

/// <summary>
/// The GBPA phase sequence that computes a build's totals: the enhancement bucket phase, the per-power enhancement
/// math, the self-buff bucket phase, then the totals fold. Only StatInclude powers contribute.
/// </summary>
/// <remarks>
/// Effects carrying ActiveConditionals or a SpecialCase are excluded without a context, and a SpecialCase effect always:
/// the CanInclude character-state switch is not ported. Mids keeps many default-true special cases, so a build with
/// one feeding a read bucket under-reports until that switch lands.
/// </remarks>
internal static class Pipeline
{
    /// <summary>Computes Totals and TotalsCapped for a build.</summary>
    /// <param name="powers">The build's powers; assemble them first for totals that include what Mids adds.</param>
    /// <param name="className">The build's class name.</param>
    /// <param name="data">The database the build is computed against.</param>
    /// <param name="build">The build's optional inputs.</param>
    /// <returns>The totals.</returns>
    /// <exception cref="ArgumentException">E09 for an unknown class; E11 for enhancement inputs supplied partially; E14 for set records without slots; E21 for incarnates without the enhancement inputs.</exception>
    /// <exception cref="InvalidDataException">H-ENH-001 or H-ENH-002 for a set piece the power cannot slot.</exception>
    /// <exception cref="NotSupportedException">E20 for an unported incarnate delivery path; E08 for an unported expression.</exception>
    public static BaseTotals ComputeBaseTotals(IReadOnlyList<Power> powers, string className, Database data, BuildOptions? build = null)
    {
        build ??= new BuildOptions();
        if (!(build.Slots is null && build.EnhDb is null && build.Tables is null) && !(build.Slots is not null && build.EnhDb is not null && build.Tables is not null))
        {
            throw new ArgumentException("E11: slots, enh_db and tables must be supplied together or all omitted", nameof(build));
        }

        int archetypeIndex = data.Classes.NidFromUidClass(className);
        if (archetypeIndex < 0)
        {
            throw new ArgumentException($"E09: unknown archetype class name: '{className}'", nameof(className));
        }

        // The dump harness writes Index positionally, so the stored index is also the list position.
        Archetype archetype = data.Classes.Classes[archetypeIndex];
        MagContext baseContext = new()
        {
            Mods = data.Mods,
            Classes = data.Classes,
            ArchetypeIndex = archetypeIndex,
            Config = data.Config,
            EnhanceNames = PerPowerEnhancementMathPhase.EnhanceNames(data.Enums),
            Enums = data.Enums,
            Expr = build.Expr,
            IncludeAllConditionals = build.IncludeAllConditionals,
        };
        BuffsX selfBuffs = new(data.Enums);
        BuffsX selfEnhance = new(data.Enums);

        // Every StatInclude power contributes, incarnates included: an Alpha's own effects are non-Buffable, and its
        // granted enhancement arrives through the resolved addends.
        List<Power> included = [.. powers.Where(power => power.StatInclude)];
        IncarnateAddends? addends = ResolveBuildIncarnates(build, powers, archetypeIndex, data);
        IReadOnlyDictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> incarnateScalar = addends?.Scalar ?? new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>();
        AddSetBonusPower(build, powers, data.Config, included);

        // The enhancement pass runs first: its build-wide buffs feed every effect's multiplier and the buff pass.
        foreach (Power power in included)
        {
            EnhancementBucketPhase.Apply(power, selfEnhance, baseContext);
        }

        MagContext buffContext = baseContext;
        IReadOnlyDictionary<int, double> toggleEndAgg = new Dictionary<int, double>();
        if (build.Slots is not null && build.EnhDb is not null && build.Tables is not null)
        {
            buffContext = baseContext with
            {
                EnhMult = PerPowerEnhancementMathPhase.ComputeEnhMultipliers(included, build.Slots, build.EnhDb, build.Tables, data.Config.ForceLevel, baseContext, addends?.Effect, selfEnhance),
            };
            toggleEndAgg = PerPowerEnhancementMathPhase.ComputeToggleEndAgg(included, build.Slots, build.EnhDb, build.Tables, data.Config.ForceLevel, incarnateScalar);
        }

        foreach (Power power in included)
        {
            SelfBuffBucketPhase.Apply(power, selfBuffs, buffContext, build.PrevHpMax, MagContext.IsGlobalAccSource(power));
        }

        BaseTotals result = ActorTotalsCalculator.Calculate(new TotalsRequest
        {
            Included = included,
            SelfBuffs = selfBuffs,
            SelfEnhance = selfEnhance,
            Archetype = archetype,
            Context = buffContext,
            Server = data.Server,
            ToggleEndAgg = toggleEndAgg,
            IncarnateScalar = incarnateScalar,
        });
        return addends is null ? result : result with { IncarnateAddends = addends };
    }

    /// <summary>The build's incarnate addends, or null when none were supplied; they need the enhancement inputs.</summary>
    private static IncarnateAddends? ResolveBuildIncarnates(BuildOptions build, IReadOnlyList<Power> powers, int archetypeIndex, Database data)
    {
        if (build.Incarnates is null)
        {
            return null;
        }

        if (build.Tables is null)
        {
            throw new ArgumentException(
                "E21: incarnates require the enhancement inputs (slots, enh_db, tables) — an incarnate's "
                + "enhancement folds through the per-effect multiplier and per-power scalar path; pass them together",
                nameof(build));
        }

        return IncarnateResolver.Resolve(build.Incarnates, powers, data.Mods, data.Classes, archetypeIndex);
    }

    /// <summary>Validates the set slotting and adds the set-bonus virtual power when it carries effects.</summary>
    private static void AddSetBonusPower(BuildOptions build, IReadOnlyList<Power> powers, EngineConfig config, List<Power> included)
    {
        if (build.SetDb is null)
        {
            return;
        }

        if (build.Slots is null || build.EnhDb is null)
        {
            string missing = string.Join(" and ", new[] { ("slots", build.Slots is null), ("enh_db", build.EnhDb is null) }.Where(item => item.Item2).Select(item => item.Item1));
            throw new ArgumentException(
                $"E14: set_db was supplied but {missing} is None; assembling the set-bonus "
                + "virtual power requires slots and enh_db — pass them together with set_db",
                nameof(build));
        }

        SetSlotting.ValidateSetSlotting(powers, build.Slots, build.EnhDb, build.SetDb);
        Power virtualPower = Accumulator.BuildSetBonusPower(powers, build.Slots, build.EnhDb, build.SetDb, config.ForceLevel, config.DisablePve);
        if (virtualPower.Effects.Count != 0)
        {
            included.Add(virtualPower);
        }
    }

    /// <summary>The database a build's totals are computed against.</summary>
    /// <param name="Mods">The AttribMod tables.</param>
    /// <param name="Classes">The class table.</param>
    /// <param name="Enums">The enum maps.</param>
    /// <param name="Config">The engine configuration.</param>
    /// <param name="Server">The server constants.</param>
    internal sealed record Database(AttribMods Mods, ArchetypeDb Classes, EnumMaps Enums, EngineConfig Config, ServerData Server);

    /// <summary>A build's optional inputs; omit the slots, records and tables for the empty-slot path.</summary>
    internal sealed record BuildOptions
    {
        /// <summary>Gets the previous recompute's HPMax, which Absorb normalization reads; 0 on a fresh compute.</summary>
        public double PrevHpMax { get; init; }

        /// <summary>Gets the build's slots.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<SlotRef>>? Slots { get; init; }

        /// <summary>Gets the enhancement records.</summary>
        public IReadOnlyDictionary<int, EnhancementRecord>? EnhDb { get; init; }

        /// <summary>Gets the Maths.mhd tables.</summary>
        public MathTables? Tables { get; init; }

        /// <summary>Gets the set records.</summary>
        public SetBonusDb? SetDb { get; init; }

        /// <summary>Gets the incarnates.</summary>
        public IReadOnlyList<IncarnatePower>? Incarnates { get; init; }

        /// <summary>Gets the build state conditions and expressions read.</summary>
        public ExpressionContext? Expr { get; init; }

        /// <summary>Gets a value indicating whether every conditional and SpecialCase effect counts; it exists for the hazard-gate test.</summary>
        public bool IncludeAllConditionals { get; init; }
    }
}
