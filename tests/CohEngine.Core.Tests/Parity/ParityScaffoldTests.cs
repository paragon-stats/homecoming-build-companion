using CohEngine.Core.Data;
using CohEngine.Core.Maths;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// The parity gate's shape over every build. Today the actual side is the dump itself, quantized by <see cref="StandIn"/>
/// the way engine output is, so it only proves the comparison reaches every field. P2 replaces each stand-in value
/// with engine output and keeps the rest.
/// </summary>
[Trait("Category", "Parity")]
public sealed class ParityScaffoldTests
{
    public static TheoryData<string> Builds => Fixtures.Builds();

    [Theory]
    [MemberData(nameof(Builds))]
    public void Totals_match_mids(string name)
    {
        MidsTotals expected = Fixtures.Store.Build(name).Totals;
        MidsTotals actual = expected;

        CompareTotals(expected.Totals, actual.Totals, $"{name}.Totals");
        CompareTotals(expected.TotalsCapped, actual.TotalsCapped, $"{name}.TotalsCapped");
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Enhanced_powers_match_mids(string name)
    {
        IReadOnlyList<EnhancedPower> expected = Fixtures.Store.Build(name).EnhancedPowers;
        IReadOnlyList<EnhancedPower> actual = expected;

        ParityAssert.Count(expected.Count, actual.Count, $"{name}.EnhancedPowers");
        for (int i = 0; i < expected.Count; i++)
        {
            EnhancedPower want = expected[i];
            EnhancedPower got = actual[i];
            string path = $"{name}.{want.FullName}";
            ParityAssert.Equal(want.Accuracy, StandIn(got.Accuracy), $"{path}.Accuracy");
            ParityAssert.Equal(want.AccuracyMult, StandIn(got.AccuracyMult), $"{path}.AccuracyMult");
            ParityAssert.Equal(want.EndCost, StandIn(got.EndCost), $"{path}.EndCost");
            ParityAssert.Equal(want.InterruptTime, StandIn(got.InterruptTime), $"{path}.InterruptTime");
            ParityAssert.Equal(want.Range, StandIn(got.Range), $"{path}.Range");
            ParityAssert.Equal(want.RechargeTime, StandIn(got.RechargeTime), $"{path}.RechargeTime");
            ParityAssert.Equal(want.CastTime, StandIn(got.CastTime), $"{path}.CastTime");
            ParityAssert.Equal(want.DamagePerActivation, StandIn(got.DamagePerActivation), $"{path}.DamagePerActivation");
            ParityAssert.Equal(want.Base.RechargeTime, StandIn(got.Base.RechargeTime), $"{path}.Base.RechargeTime");
            ParityAssert.Equal(want.Base.EndCost, StandIn(got.Base.EndCost), $"{path}.Base.EndCost");
            ParityAssert.Equal(want.Base.Accuracy, StandIn(got.Base.Accuracy), $"{path}.Base.Accuracy");
            ParityAssert.Equal(want.Base.InterruptTime, StandIn(got.Base.InterruptTime), $"{path}.Base.InterruptTime");
            ParityAssert.Equal(want.Base.Range, StandIn(got.Base.Range), $"{path}.Base.Range");
            ParityAssert.Equal(want.Base.CastTime, StandIn(got.Base.CastTime), $"{path}.Base.CastTime");
            ParityAssert.Count(want.Effects.Count, got.Effects.Count, $"{path}.Effects");
            for (int j = 0; j < want.Effects.Count; j++)
            {
                ParityAssert.Equal(want.Effects[j].MathMag, StandIn(got.Effects[j].MathMag), $"{path}.Effects[{j}].Math_Mag");
                ParityAssert.Equal(want.Effects[j].MathDuration, StandIn(got.Effects[j].MathDuration), $"{path}.Effects[{j}].Math_Duration");
            }
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

    private static void CompareTotals(TotalStatistics expected, TotalStatistics actual, string path)
    {
        ParityAssert.Equal(expected.Def, StandIn(actual.Def), $"{path}.Def");
        ParityAssert.Equal(expected.Res, StandIn(actual.Res), $"{path}.Res");
        ParityAssert.Equal(expected.Mez, StandIn(actual.Mez), $"{path}.Mez");
        ParityAssert.Equal(expected.MezRes, StandIn(actual.MezRes), $"{path}.MezRes");
        ParityAssert.Equal(expected.DebuffRes, StandIn(actual.DebuffRes), $"{path}.DebuffRes");
        ParityAssert.Equal(expected.Elusivity, StandIn(actual.Elusivity), $"{path}.Elusivity");
        (string Field, double Want, double Got)[] scalars =
        [
            ("HPRegen", expected.HpRegen, StandIn(actual.HpRegen)),
            ("HPMax", expected.HpMax, StandIn(actual.HpMax)),
            ("Absorb", expected.Absorb, StandIn(actual.Absorb)),
            ("EndRec", expected.EndRec, StandIn(actual.EndRec)),
            ("EndUse", expected.EndUse, StandIn(actual.EndUse)),
            ("EndMax", expected.EndMax, StandIn(actual.EndMax)),
            ("RunSpd", expected.RunSpd, StandIn(actual.RunSpd)),
            ("MaxRunSpd", expected.MaxRunSpd, StandIn(actual.MaxRunSpd)),
            ("JumpSpd", expected.JumpSpd, StandIn(actual.JumpSpd)),
            ("MaxJumpSpd", expected.MaxJumpSpd, StandIn(actual.MaxJumpSpd)),
            ("FlySpd", expected.FlySpd, StandIn(actual.FlySpd)),
            ("MaxFlySpd", expected.MaxFlySpd, StandIn(actual.MaxFlySpd)),
            ("JumpHeight", expected.JumpHeight, StandIn(actual.JumpHeight)),
            ("StealthPvE", expected.StealthPve, StandIn(actual.StealthPve)),
            ("StealthPvP", expected.StealthPvp, StandIn(actual.StealthPvp)),
            ("ThreatLevel", expected.ThreatLevel, StandIn(actual.ThreatLevel)),
            ("Perception", expected.Perception, StandIn(actual.Perception)),
            ("BuffHaste", expected.BuffHaste, StandIn(actual.BuffHaste)),
            ("BuffAcc", expected.BuffAcc, StandIn(actual.BuffAcc)),
            ("BuffToHit", expected.BuffToHit, StandIn(actual.BuffToHit)),
            ("BuffDam", expected.BuffDam, StandIn(actual.BuffDam)),
            ("BuffEndRdx", expected.BuffEndRdx, StandIn(actual.BuffEndRdx)),
            ("BuffRange", expected.BuffRange, StandIn(actual.BuffRange)),
            ("BuffHeal", expected.BuffHeal, StandIn(actual.BuffHeal)),
        ];
        foreach ((string field, double want, double got) in scalars)
        {
            ParityAssert.Equal(want, got, $"{path}.{field}");
        }
    }

    // Stand-in for engine output, which is single precision; P2 replaces each call with the engine's value.
    private static double StandIn(double dumped)
    {
        return F32.Round(dumped);
    }

    private static double[] StandIn(IReadOnlyList<double> dumped)
    {
        return [.. dumped.Select(F32.Round)];
    }
}
