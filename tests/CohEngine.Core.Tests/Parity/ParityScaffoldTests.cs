using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// The parity gate over every build: the whole pipeline (assembly, slots, set bonuses, incarnates) against Mids'
/// totals and per-power scalars. The fields the engine does not compute yet keep the <see cref="StandIn"/> value,
/// and the gaps the Python engine also has are listed in <see cref="KnownGaps"/>, so a fix flips them loudly.
/// </summary>
[Trait("Category", "Parity")]
public sealed class ParityScaffoldTests
{
    private const string Sniper = "Redirects.Arsenal_Assault.Sniper_Rifle_Normal";

    // Each entry is a build (or build-name prefix) and a field path the Python reference engine misses the same way:
    // Cardiac's global Range incarnate, the Stalker's Hidden stealth (a SpecialCase CanInclude drops), the Tanker's
    // regeneration, recovery and range, the redirected Sniper Rifle (redirects are not ported), and one Blaster recharge.
    private static readonly (string Build, string Path)[] KnownGaps =
    [
        ("arsenal_dominator_cardiac", "Totals.BuffRange"),
        ("arsenal_dominator_cardiac", "TotalsCapped.BuffRange"),

        ("Stalker (Fiery Melee - Invulnerability)", "Totals.StealthPvE"),
        ("Stalker (Fiery Melee - Invulnerability)", "Totals.StealthPvP"),
        ("Stalker (Fiery Melee - Invulnerability)", "TotalsCapped.StealthPvE"),
        ("Stalker (Fiery Melee - Invulnerability)", "TotalsCapped.StealthPvP"),
        ("wpss_tanker_leadership", "Totals.HPRegen"),
        ("wpss_tanker_leadership", "Totals.EndRec"),
        ("wpss_tanker_leadership", "Totals.BuffRange"),
        ("wpss_tanker_leadership", "TotalsCapped.HPRegen"),
        ("wpss_tanker_leadership", "TotalsCapped.EndRec"),
        ("wpss_tanker_leadership", "TotalsCapped.BuffRange"),

        ("arsenal_dominator_", Sniper),

        ("Laser - PRIME - Blaster (Energy Blast - Energy Manipulation)", "Blaster_Support.Energy_Manipulation.Stun.RechargeTime"),
    ];

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
        (IReadOnlyList<Power> powers, BaseTotals totals, BuildInputs inputs) = Run(name);
        PowerStatistics.StatsContext context = new()
        {
            Config = ForceLevel(name) is { } level ? Fixtures.Store.Config with { ForceLevel = level } : Fixtures.Store.Config,
            Tables = Fixtures.Store.Maths,
            EnhDb = Fixtures.Store.EnhancementEffects,
            GlobalEnhance = totals.GlobalEnhance,
            RechargeCap = Fixtures.Store.Archetypes.Classes[inputs.ArchetypeIndex].RechargeCap,
            IncarnateScalar = totals.IncarnateAddends!.Scalar,
        };
        Dictionary<int, Power> byIndex = powers.ToDictionary(power => power.BuildIndex);
        IReadOnlyList<EnhancedPower> expected = Fixtures.Store.Build(name).EnhancedPowers;

        ParityAssert.Count(expected.Count, powers.Count, $"{name}.EnhancedPowers");
        foreach (EnhancedPower want in expected)
        {
            Power power = byIndex[want.BuildIndex];
            PowerStatistics.PowerStats got = PowerStatistics.ComputePowerStats(power, inputs.Slots.GetValueOrDefault(power.BuildIndex, []), context);
            string path = $"{want.FullName}";
            Check(name, want.Accuracy, StandIn(want.Accuracy), $"{path}.Accuracy");
            Check(name, want.AccuracyMult, StandIn(want.AccuracyMult), $"{path}.AccuracyMult");
            Check(name, want.EndCost, got.EndCost, $"{path}.EndCost");
            Check(name, want.InterruptTime, got.InterruptTime, $"{path}.InterruptTime");
            Check(name, want.Range, StandIn(want.Range), $"{path}.Range");
            Check(name, want.RechargeTime, got.RechargeTime, $"{path}.RechargeTime");
            Check(name, want.CastTime, got.CastTime, $"{path}.CastTime");
            Check(name, want.DamagePerActivation, StandIn(want.DamagePerActivation), $"{path}.DamagePerActivation");
            Check(name, want.Base.RechargeTime, power.RechargeTime, $"{path}.Base.RechargeTime");
            Check(name, want.Base.EndCost, power.EndCost, $"{path}.Base.EndCost");
            Check(name, want.Base.Accuracy, StandIn(want.Base.Accuracy), $"{path}.Base.Accuracy");
            Check(name, want.Base.InterruptTime, power.InterruptTime, $"{path}.Base.InterruptTime");
            Check(name, want.Base.Range, StandIn(want.Base.Range), $"{path}.Base.Range");
            Check(name, want.Base.CastTime, power.CastTime, $"{path}.Base.CastTime");
            for (int j = 0; j < want.Effects.Count; j++)
            {
                Check(name, want.Effects[j].MathMag, StandIn(want.Effects[j].MathMag), $"{path}.Effects[{j}].Math_Mag");
                Check(name, want.Effects[j].MathDuration, StandIn(want.Effects[j].MathDuration), $"{path}.Effects[{j}].Math_Duration");
            }
        }
    }

    [Fact]
    public void Every_known_gap_still_differs()
    {
        foreach ((string build, string field) in KnownGaps.Where(gap => gap.Path.StartsWith("Totals", StringComparison.Ordinal)))
        {
            (string side, string scalar) = (field.Split('.')[0], field.Split('.')[1]);
            MidsTotals expected = Fixtures.Store.Build(build).Totals;
            BaseTotals actual = Run(build).Totals;
            bool capped = string.Equals(side, "TotalsCapped", StringComparison.Ordinal);
            (_, double want, double got) = EngineRun.Scalars(capped ? expected.TotalsCapped : expected.Totals, capped ? actual.TotalsCapped : actual.Totals).Single(item => string.Equals(item.Field, scalar, StringComparison.Ordinal));
            Assert.NotEqual(F32.Round(want), got);
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

    private static (IReadOnlyList<Power> Powers, BaseTotals Totals, BuildInputs Inputs) Run(string name)
    {
        return EngineRun.Full(name, ForceLevel(name));
    }

    private static void CompareTotals(string name, TotalStatistics expected, TotalStatistics actual, string side)
    {
        HashSet<string> skip = [.. KnownGaps.Where(gap => string.Equals(gap.Build, name, StringComparison.Ordinal) && gap.Path.StartsWith(side + ".", StringComparison.Ordinal)).Select(gap => gap.Path[(side.Length + 1)..])];
        EngineRun.CompareTotals(expected, actual, $"{name}.{side}", skip);
    }

    private static void Check(string name, double want, double got, string path)
    {
        if (KnownGaps.Any(gap => name.StartsWith(gap.Build, StringComparison.Ordinal) && path.StartsWith(gap.Path, StringComparison.Ordinal)))
        {
            return;
        }

        ParityAssert.Equal(want, got, $"{name}.{path}");
    }

    // Stand-in for a per-power value the engine does not compute yet (accuracy, range, damage, effect magnitudes).
    private static double StandIn(double dumped)
    {
        return F32.Round(dumped);
    }
}
