using System.Buffers.Binary;
using System.Security.Cryptography;

using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals;
using CohEngine.Core.Totals.Incarnate;
using CohEngine.Core.Totals.Phases;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// The parity gate over every build: the whole pipeline (assembly, slots, set bonuses, incarnates) against Mids'
/// totals, per-power scalars and effect magnitudes. A field the Python engine also misses is listed in
/// <see cref="KnownGaps"/> with the Python engine's value, which the C# must equal while still differing from Mids.
/// The per-power values neither engine computes (accuracy, range, damage, effect durations) keep the
/// <see cref="StandIn"/> value.
/// </summary>
[Trait("Category", "Parity")]
public sealed class ParityScaffoldTests
{
    private const string Sniper = "Redirects.Arsenal_Assault.Sniper_Rifle_Normal";
    private const string Cardiac = "arsenal_dominator_cardiac";
    private const string Agility = "arsenal_dominator_incarnate";
    private const string AgilitySlotted = "arsenal_dominator_incarnate_slotted";
    private const string NoSlots = "arsenal_dominator_noslots";
    private const string Stalker = "Stalker (Fiery Melee - Invulnerability)";
    private const string Tanker = "wpss_tanker_leadership";
    private const string Blaster = "Laser - PRIME - Blaster (Energy Blast - Energy Manipulation)";

    // Each entry is a build, a field path, and the Python reference engine's value for it, measured on the same dumps:
    // Cardiac's global Range incarnate, the Stalker's Hidden stealth (a SpecialCase CanInclude drops), the Tanker's
    // regeneration, recovery and range, the redirected Sniper Rifle (the engine reads Sniper_Rifle's cast and cost, as
    // redirects are not ported), and one Blaster recharge.
    private static readonly (string Build, string Path, double Python)[] KnownGaps =
    [
        (Cardiac, "Totals.BuffRange", 0.0),
        (Cardiac, "TotalsCapped.BuffRange", 0.0),
        (Stalker, "Totals.StealthPvE", -264.0),
        (Stalker, "Totals.StealthPvP", -371.0),
        (Stalker, "TotalsCapped.StealthPvE", -264.0),
        (Stalker, "TotalsCapped.StealthPvP", -371.0),
        (Tanker, "Totals.HPRegen", 4.1373748779296875),
        (Tanker, "Totals.EndRec", 1.3109599351882935),
        (Tanker, "Totals.BuffRange", 0.0),
        (Tanker, "TotalsCapped.HPRegen", 4.1373748779296875),
        (Tanker, "TotalsCapped.EndRec", 1.3109599351882935),
        (Tanker, "TotalsCapped.BuffRange", 0.0),
        (Cardiac, $"{Sniper}.EndCost", 12.765517234802246),
        (Cardiac, $"{Sniper}.CastTime", 1.1699999570846558),
        (Cardiac, $"{Sniper}.Base.EndCost", 18.510000228881836),
        (Cardiac, $"{Sniper}.Base.CastTime", 1.1699999570846558),
        (Agility, $"{Sniper}.EndCost", 18.510000228881836),
        (Agility, $"{Sniper}.RechargeTime", 15.03759479522705),
        (Agility, $"{Sniper}.CastTime", 1.1699999570846558),
        (Agility, $"{Sniper}.Base.EndCost", 18.510000228881836),
        (Agility, $"{Sniper}.Base.CastTime", 1.1699999570846558),
        (AgilitySlotted, $"{Sniper}.EndCost", 18.510000228881836),
        (AgilitySlotted, $"{Sniper}.RechargeTime", 15.03759479522705),
        (AgilitySlotted, $"{Sniper}.CastTime", 1.1699999570846558),
        (AgilitySlotted, $"{Sniper}.Base.EndCost", 18.510000228881836),
        (AgilitySlotted, $"{Sniper}.Base.CastTime", 1.1699999570846558),
        (NoSlots, $"{Sniper}.EndCost", 18.510000228881836),
        (NoSlots, $"{Sniper}.CastTime", 1.1699999570846558),
        (NoSlots, $"{Sniper}.Base.EndCost", 18.510000228881836),
        (NoSlots, $"{Sniper}.Base.CastTime", 1.1699999570846558),
        (Blaster, "Blaster_Support.Energy_Manipulation.Stun.RechargeTime", 3.5563457012176514),
    ];

    // The Python engine's effect magnitudes against Math_Mag, effects paired by identity: how many differ from Mids,
    // how many it computes, and the SHA-256 of their float32 bit patterns (little-endian, in effect order). The engine
    // reads the pre-Pass5 enhanced magnitude, so Mids' post-buff multiply and its assembled effect order account for
    // the differences; the C# must reproduce all three.
    private static readonly Dictionary<string, (int Mismatches, int Count, string Digest)> PythonMagnitudes = new(StringComparer.Ordinal)
    {
        [Cardiac] = (30, 365, "68bf49d4847a3525849641fb1f4ea382ba689a9889f6f3693ecb85ee80de5502"),
        [Agility] = (41, 365, "1848e99acad24882ac026d962050560e7ce58ccc4b83ab7086d622f7ce496068"),
        [AgilitySlotted] = (41, 365, "fe4e4974111861f97e0ee9fc0c1bd7221fcc6c1a593a701b1ef4d0174104c859"),
        [NoSlots] = (10, 357, "ddcfa43f9be75905938666c6ac575e65d90a8a539cda15a0b1a3c502d7377fd7"),
        ["hasten_2rech"] = (7, 413, "cb9a6033ebdd9b6fb8daea5274d716859620b1a5274b3a5ea3048869692326f8"),
        ["Hollowmind - Dominator (Darkness Control - Psionic Assault)"] = (38, 305, "5921ba6c74a7e2e578d67f0fa82a187b06510b63617e60789c0f09ebc17786f8"),
        ["Laser - Accretion - Scrapper (Dark Melee - Shield Defense)"] = (2, 452, "89a3a460c977a53b0b30c431d91689b6aee0c344d90fbe71239c67a985fd3d51"),
        ["Laser - ALT+F4 - Controller (Illusion Control - Time Manipulation)"] = (8, 366, "e5972a12ed7ce61473de8f10f35767e8b428e9ea3621d6ca94b91fa6a83d5c93"),
        ["Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)"] = (32, 403, "bffd93920a1246ecb1ecd595323947bd47188c3c099c410c69c94a007df7bf5a"),
        ["Laser - Fallout - Sentinel (Fire Blast - Radiation Armor)"] = (0, 77, "2f98f0c1bc7b0b85091ac5085b7098b1114426f21c6ce5489e67456bf5022a7a"),
        ["Laser - HAZMAT - PvE - Brute (Radiation Melee - Fiery Aura)"] = (34, 578, "b12e26882298e9dc2c413da58c2bd92339cffc416d599d8688cd0d4f15d04762"),
        ["Laser - NUKE - Corruptor (Fire Blast - Radiation Emission)"] = (2, 391, "a186fcc4302a34c4bb8e024400ee932eb945e2dcea82a82285fa907de41bc246"),
        [Blaster] = (17, 451, "5a9a829e9b49fca4ff8f23a015caf2b4cf37bfc56de59154e6ca6acc5e437361"),
        ["Laser - QUANTUM - Tanker (Super Reflexes - Energy Melee)"] = (6, 463, "c9827c8d80034ac3bb224a447f3576a7e052b63b43d2919f91d87710f41168d1"),
        ["Melee Arachnos Widow (Night Widow Training - Widow Teamwork)"] = (62, 483, "837a928e6ab6e6d9d8f263cf0903da3490b09075cdca69af47bac20df40e707d"),
        ["Petless Mastermind (Demon Summoning - Force Field)"] = (90, 547, "c6016481822102046d9b6d24eb45d1812b2d5aefda430f392892f309a3726d4f"),
        ["shield_scrapper_global_endrdx"] = (7, 413, "f32f4a4698cff98e08a2a7077d0eb7cdbff69f5f2f3c6e1eeacc97b985198c5c"),
        ["shield_scrapper_mixed_sets"] = (7, 413, "f35c3bb6d3f81b344422b8741760ca8d056d1af7008d040cfefa86ed73b43181"),
        ["shield_scrapper_noslots"] = (7, 413, "5402d89e7861f553e4e1fc6e55b9a274029cc2d2256b3d12b18e7f83a68edfc6"),
        ["shield_scrapper_set_bonuses"] = (106, 413, "5614bddb594a1bf4e39e44ccffed445ed7ef4ac9059808b0263daabc925866f9"),
        ["shield_scrapper_set_bonuses_exemplar25"] = (95, 413, "30e04235d70ef7390d46f34e04a8219d1b22ee7758fa187820b891d94863b759"),
        ["shield_scrapper_slotted"] = (7, 413, "cb9a6033ebdd9b6fb8daea5274d716859620b1a5274b3a5ea3048869692326f8"),
        [Stalker] = (2, 447, "98ccb536584966923e5428a555991c6259d1cfee00eb4f5c03eb9876fdac5144"),
        [Tanker] = (121, 386, "7681f6164050022ed1db011391660a2dd0ae5b94e89b218d4b062762fa48ebad"),
    };

    public static TheoryData<string> Builds => Fixtures.Builds();

    [Theory]
    [MemberData(nameof(Builds))]
    public void Totals_match_mids(string name)
    {
        MidsTotals expected = Fixtures.Store.Build(name).Totals;
        BaseTotals actual = Run(name).Totals;

        CompareTotals(name, expected.Totals, actual.Totals, "Totals");
        CompareTotals(name, expected.TotalsCapped, actual.TotalsCapped, "TotalsCapped");
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Enhanced_powers_match_mids(string name)
    {
        IReadOnlyList<EnhancedPower> expected = Fixtures.Store.Build(name).EnhancedPowers;
        List<(string Path, double Want, double Got)> rows = PowerRows(name);
        ParityAssert.Count(expected.Count, Run(name).Powers.Count, $"{name}.EnhancedPowers");
        foreach ((string path, double want, double got) in rows)
        {
            Check(name, want, got, path);
        }

        foreach (EnhancedPower want in expected)
        {
            Check(name, want.Accuracy, StandIn(want.Accuracy), $"{want.FullName}.Accuracy");
            Check(name, want.AccuracyMult, StandIn(want.AccuracyMult), $"{want.FullName}.AccuracyMult");
            Check(name, want.Range, StandIn(want.Range), $"{want.FullName}.Range");
            Check(name, want.DamagePerActivation, StandIn(want.DamagePerActivation), $"{want.FullName}.DamagePerActivation");
            Check(name, want.Base.Accuracy, StandIn(want.Base.Accuracy), $"{want.FullName}.Base.Accuracy");
            Check(name, want.Base.Range, StandIn(want.Base.Range), $"{want.FullName}.Base.Range");
            for (int j = 0; j < want.Effects.Count; j++)
            {
                Check(name, want.Effects[j].MathDuration, StandIn(want.Effects[j].MathDuration), $"{want.FullName}.Effects[{j}].Math_Duration");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Effect_magnitudes_match_the_python_engine(string name)
    {
        (IReadOnlyList<Power> powers, _, BuildInputs inputs) = Run(name);
        MagContext context = MagnitudeContext(name, inputs);
        Dictionary<(int BuildIndex, int EffectIndex), double> mults = Multipliers(powers, inputs, context);
        Dictionary<int, Power> byIndex = powers.ToDictionary(power => power.BuildIndex);
        int mismatches = 0;
        int count = 0;
        using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] bits = new byte[4];
        foreach (EnhancedPower want in inputs.Fixture.EnhancedPowers)
        {
            Power power = byIndex[want.BuildIndex];
            Dictionary<(string, string, string, string), Queue<double>> dumped = [];
            foreach (EnhancedEffect effect in want.Effects)
            {
                (string, string, string, string) key = (effect.EffectType, effect.DamageType, effect.MezType, effect.EtModifies);
                dumped.TryAdd(key, new Queue<double>());
                dumped[key].Enqueue(effect.MathMag);
            }

            foreach (Effect effect in power.Effects)
            {
                double mag = F32.Round(context.Mag(effect, power) * mults.GetValueOrDefault((power.BuildIndex, effect.Index), 1.0));
                BinaryPrimitives.WriteInt32LittleEndian(bits, BitConverter.SingleToInt32Bits((float)mag));
                digest.AppendData(bits);
                count++;
                if (!dumped.TryGetValue((effect.EffectType, effect.DamageType, effect.MezType, effect.EtModifies), out Queue<double>? queue) || queue.Count == 0 || F32.Round(queue.Dequeue()) != mag)
                {
                    mismatches++;
                }
            }

            mismatches += dumped.Values.Sum(queue => queue.Count);
        }

        Assert.Equal(PythonMagnitudes[name], (mismatches, count, Convert.ToHexStringLower(digest.GetHashAndReset())));
    }

    [Fact]
    public void Every_known_gap_holds_the_python_value()
    {
        foreach ((string build, string path, double python) in KnownGaps)
        {
            (double mids, double got) = Gap(build, path);
            Assert.True(got == F32.Round(python), FormattableString.Invariant($"{build}.{path}: the engine gives {got:R}, the Python engine {python:R}"));
            Assert.True(got != F32.Round(mids), FormattableString.Invariant($"{build}.{path}: now matches Mids ({mids:R}); drop the gap"));
        }
    }

    [Fact]
    public void A_mismatch_names_the_field_path()
    {
        XunitException scalar = Assert.Throws<XunitException>(() => ParityAssert.Equal(1.0, 1.5, "build.Totals.HPMax"));
        XunitException length = Assert.Throws<XunitException>(() => ParityAssert.Equal([1.0], [], "build.Totals.Def"));
        XunitException cell = Assert.Throws<XunitException>(() => ParityAssert.Equal([1.0, 2.0], [1.0, 3.0], "build.Totals.Def"));

        Assert.Equal("build.Totals.HPMax: expected 1, got 1.5", scalar.Message);
        Assert.Equal("build.Totals.Def: expected 1 entries, got 0", length.Message);
        Assert.StartsWith("build.Totals.Def[1]:", cell.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_dumped_side_is_quantized()
    {
        ParityAssert.Equal(0.1, 0.10000000149011612, "build.Totals.BuffHaste");
        ParityAssert.Equal(0.0, -0.0, "build.Totals.Absorb");
        XunitException unquantized = Assert.Throws<XunitException>(() => ParityAssert.Equal(0.1, 0.1, "build.Totals.BuffHaste"));

        Assert.Equal("build.Totals.BuffHaste: expected 0.10000000149011612, got 0.1", unquantized.Message);
    }

    [Fact]
    public void A_dumped_value_past_single_range_names_the_field_path()
    {
        XunitException overflow = Assert.Throws<XunitException>(() => ParityAssert.Equal(1e39, 1.0, "build.Totals.HPMax"));
        XunitException count = Assert.Throws<XunitException>(() => ParityAssert.Count(2, 1, "build.EnhancedPowers"));

        Assert.StartsWith("build.Totals.HPMax: the dumped value does not fit single precision", overflow.Message, StringComparison.Ordinal);
        Assert.Equal("build.EnhancedPowers: expected 2 entries, got 1", count.Message);
    }

    // The exemplared dump was taken at ForceLevel 25; every other dump at the configured 50.
    private static int? ForceLevel(string name)
    {
        return name.EndsWith("_exemplar25", StringComparison.Ordinal) ? 25 : null;
    }

    private static EngineConfig Config(string name)
    {
        return ForceLevel(name) is { } level ? Fixtures.Store.Config with { ForceLevel = level } : Fixtures.Store.Config;
    }

    private static (IReadOnlyList<Power> Powers, BaseTotals Totals, BuildInputs Inputs) Run(string name)
    {
        return EngineRun.Full(name, ForceLevel(name));
    }

    /// <summary>Every per-power scalar the engine computes, as (power.field path, Mids value, engine value).</summary>
    private static List<(string Path, double Want, double Got)> PowerRows(string name)
    {
        (IReadOnlyList<Power> powers, BaseTotals totals, BuildInputs inputs) = Run(name);
        PowerStatistics.StatsContext context = new()
        {
            Config = Config(name),
            Tables = Fixtures.Store.Maths,
            EnhDb = Fixtures.Store.EnhancementEffects,
            GlobalEnhance = totals.GlobalEnhance,
            RechargeCap = Fixtures.Store.Archetypes.Classes[inputs.ArchetypeIndex].RechargeCap,
            IncarnateScalar = totals.IncarnateAddends!.Scalar,
        };
        Dictionary<int, Power> byIndex = powers.ToDictionary(power => power.BuildIndex);
        List<(string Path, double Want, double Got)> rows = [];
        foreach (EnhancedPower want in Fixtures.Store.Build(name).EnhancedPowers)
        {
            Power power = byIndex[want.BuildIndex];
            PowerStatistics.PowerStats got = PowerStatistics.ComputePowerStats(power, inputs.Slots.GetValueOrDefault(power.BuildIndex, []), context);
            string path = want.FullName;
            rows.Add(($"{path}.EndCost", want.EndCost, got.EndCost));
            rows.Add(($"{path}.InterruptTime", want.InterruptTime, got.InterruptTime));
            rows.Add(($"{path}.RechargeTime", want.RechargeTime, got.RechargeTime));
            rows.Add(($"{path}.CastTime", want.CastTime, got.CastTime));
            rows.Add(($"{path}.Base.RechargeTime", want.Base.RechargeTime, power.RechargeTime));
            rows.Add(($"{path}.Base.EndCost", want.Base.EndCost, power.EndCost));
            rows.Add(($"{path}.Base.InterruptTime", want.Base.InterruptTime, power.InterruptTime));
            rows.Add(($"{path}.Base.CastTime", want.Base.CastTime, power.CastTime));
        }

        return rows;
    }

    /// <summary>A known gap's Mids value and engine value.</summary>
    private static (double Mids, double Got) Gap(string build, string path)
    {
        string[] parts = path.Split('.', 2);
        if (parts[0] is "Totals" or "TotalsCapped")
        {
            MidsTotals expected = Fixtures.Store.Build(build).Totals;
            BaseTotals actual = Run(build).Totals;
            bool capped = string.Equals(parts[0], "TotalsCapped", StringComparison.Ordinal);
            (_, double want, double got) = EngineRun.Scalars(capped ? expected.TotalsCapped : expected.Totals, capped ? actual.TotalsCapped : actual.Totals).Single(item => string.Equals(item.Field, parts[1], StringComparison.Ordinal));
            return (want, got);
        }

        (_, double mids, double engine) = PowerRows(build).Single(row => string.Equals(row.Path, path, StringComparison.Ordinal));
        return (mids, engine);
    }

    /// <summary>The magnitude context the totals pipeline reads its base magnitudes with.</summary>
    private static MagContext MagnitudeContext(string name, BuildInputs inputs)
    {
        return new MagContext
        {
            Mods = Fixtures.Store.AttribMods,
            Classes = Fixtures.Store.Archetypes,
            ArchetypeIndex = inputs.ArchetypeIndex,
            Config = Config(name),
            EnhanceNames = PerPowerEnhancementMathPhase.EnhanceNames(Fixtures.Store.Enums),
            Enums = Fixtures.Store.Enums,
            Expr = inputs.Fixture.Has("power_assembly.json") ? inputs.Expression : null,
        };
    }

    /// <summary>The per-effect multipliers, built as <c>Pipeline.ComputeBaseTotals</c> builds them.</summary>
    private static Dictionary<(int BuildIndex, int EffectIndex), double> Multipliers(IReadOnlyList<Power> powers, BuildInputs inputs, MagContext context)
    {
        List<Power> included = [.. powers.Where(power => power.StatInclude)];
        Power bonus = Accumulator.BuildSetBonusPower(powers, inputs.Slots, Fixtures.Store.EnhancementEffects, Fixtures.Store.SetBonuses, context.Config.ForceLevel, context.Config.DisablePve);
        if (bonus.Effects.Count != 0)
        {
            included.Add(bonus);
        }

        BuffsX selfEnhance = new(Fixtures.Store.Enums);
        foreach (Power power in included)
        {
            EnhancementBucketPhase.Apply(power, selfEnhance, context);
        }

        IncarnateAddends addends = IncarnateResolver.Resolve(inputs.Fixture.Incarnates, powers, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, inputs.ArchetypeIndex);
        return PerPowerEnhancementMathPhase.ComputeEnhMultipliers(included, inputs.Slots, Fixtures.Store.EnhancementEffects, Fixtures.Store.Maths, context.Config.ForceLevel, context, addends.Effect, selfEnhance);
    }

    private static void CompareTotals(string name, TotalStatistics expected, TotalStatistics actual, string side)
    {
        HashSet<string> skip = [.. KnownGaps.Where(gap => string.Equals(gap.Build, name, StringComparison.Ordinal) && gap.Path.StartsWith(side + ".", StringComparison.Ordinal)).Select(gap => gap.Path[(side.Length + 1)..])];
        EngineRun.CompareTotals(expected, actual, $"{name}.{side}", skip);
    }

    // A known gap is asserted by Every_known_gap_holds_the_python_value, not here.
    private static void Check(string name, double want, double got, string path)
    {
        if (KnownGaps.Any(gap => string.Equals(gap.Build, name, StringComparison.Ordinal) && string.Equals(gap.Path, path, StringComparison.Ordinal)))
        {
            return;
        }

        ParityAssert.Equal(want, got, $"{name}.{path}");
    }

    // Stand-in for a per-power value neither engine computes yet (accuracy, range, damage, effect durations).
    private static double StandIn(double dumped)
    {
        return F32.Round(dumped);
    }
}
