using CohEngine.Core.Data;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// The parity gate's shape over every build. Today both sides are the Mids reference dump, so it only proves the
/// comparison runs over every field; P2 replaces each <c>actual</c> with engine output and keeps the rest.
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

        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            EnhancedPower want = expected[i];
            EnhancedPower got = actual[i];
            string path = $"{name}.{want.FullName}";
            ParityAssert.Equal(want.Accuracy, got.Accuracy, $"{path}.Accuracy");
            ParityAssert.Equal(want.AccuracyMult, got.AccuracyMult, $"{path}.AccuracyMult");
            ParityAssert.Equal(want.EndCost, got.EndCost, $"{path}.EndCost");
            ParityAssert.Equal(want.InterruptTime, got.InterruptTime, $"{path}.InterruptTime");
            ParityAssert.Equal(want.Range, got.Range, $"{path}.Range");
            ParityAssert.Equal(want.RechargeTime, got.RechargeTime, $"{path}.RechargeTime");
            ParityAssert.Equal(want.CastTime, got.CastTime, $"{path}.CastTime");
            ParityAssert.Equal(want.DamagePerActivation, got.DamagePerActivation, $"{path}.DamagePerActivation");
            ParityAssert.Equal(want.Base.RechargeTime, got.Base.RechargeTime, $"{path}.Base.RechargeTime");
            ParityAssert.Equal(want.Base.EndCost, got.Base.EndCost, $"{path}.Base.EndCost");
            ParityAssert.Equal(want.Base.Accuracy, got.Base.Accuracy, $"{path}.Base.Accuracy");
            ParityAssert.Equal(want.Base.InterruptTime, got.Base.InterruptTime, $"{path}.Base.InterruptTime");
            ParityAssert.Equal(want.Base.Range, got.Base.Range, $"{path}.Base.Range");
            ParityAssert.Equal(want.Base.CastTime, got.Base.CastTime, $"{path}.Base.CastTime");
            Assert.Equal(want.Effects.Count, got.Effects.Count);
            for (int j = 0; j < want.Effects.Count; j++)
            {
                ParityAssert.Equal(want.Effects[j].MathMag, got.Effects[j].MathMag, $"{path}.Effects[{j}].Math_Mag");
                ParityAssert.Equal(want.Effects[j].MathDuration, got.Effects[j].MathDuration, $"{path}.Effects[{j}].Math_Duration");
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
        Assert.Equal("build.Totals.Def: expected 1 cells, got 0", length.Message);
        Assert.StartsWith("build.Totals.Def[1]:", cell.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_equal_once_quantized_pass()
    {
        ParityAssert.Equal(0.1, 0.10000000149011612, "build.Totals.BuffHaste");
        ParityAssert.Equal(0.0, -0.0, "build.Totals.Absorb");
    }

    private static void CompareTotals(TotalStatistics expected, TotalStatistics actual, string path)
    {
        ParityAssert.Equal(expected.Def, actual.Def, $"{path}.Def");
        ParityAssert.Equal(expected.Res, actual.Res, $"{path}.Res");
        ParityAssert.Equal(expected.Mez, actual.Mez, $"{path}.Mez");
        ParityAssert.Equal(expected.MezRes, actual.MezRes, $"{path}.MezRes");
        ParityAssert.Equal(expected.DebuffRes, actual.DebuffRes, $"{path}.DebuffRes");
        ParityAssert.Equal(expected.Elusivity, actual.Elusivity, $"{path}.Elusivity");
        (string Field, double Want, double Got)[] scalars =
        [
            ("HPRegen", expected.HpRegen, actual.HpRegen),
            ("HPMax", expected.HpMax, actual.HpMax),
            ("Absorb", expected.Absorb, actual.Absorb),
            ("EndRec", expected.EndRec, actual.EndRec),
            ("EndUse", expected.EndUse, actual.EndUse),
            ("EndMax", expected.EndMax, actual.EndMax),
            ("RunSpd", expected.RunSpd, actual.RunSpd),
            ("MaxRunSpd", expected.MaxRunSpd, actual.MaxRunSpd),
            ("JumpSpd", expected.JumpSpd, actual.JumpSpd),
            ("MaxJumpSpd", expected.MaxJumpSpd, actual.MaxJumpSpd),
            ("FlySpd", expected.FlySpd, actual.FlySpd),
            ("MaxFlySpd", expected.MaxFlySpd, actual.MaxFlySpd),
            ("JumpHeight", expected.JumpHeight, actual.JumpHeight),
            ("StealthPvE", expected.StealthPve, actual.StealthPve),
            ("StealthPvP", expected.StealthPvp, actual.StealthPvp),
            ("ThreatLevel", expected.ThreatLevel, actual.ThreatLevel),
            ("Perception", expected.Perception, actual.Perception),
            ("BuffHaste", expected.BuffHaste, actual.BuffHaste),
            ("BuffAcc", expected.BuffAcc, actual.BuffAcc),
            ("BuffToHit", expected.BuffToHit, actual.BuffToHit),
            ("BuffDam", expected.BuffDam, actual.BuffDam),
            ("BuffEndRdx", expected.BuffEndRdx, actual.BuffEndRdx),
            ("BuffRange", expected.BuffRange, actual.BuffRange),
            ("BuffHeal", expected.BuffHeal, actual.BuffHeal),
        ];
        foreach ((string field, double want, double got) in scalars)
        {
            ParityAssert.Equal(want, got, $"{path}.{field}");
        }
    }
}
