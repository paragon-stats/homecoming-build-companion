using CohEngine.Core.Assembly;
using CohEngine.Core.Data;
using CohEngine.Core.Registry;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Tests.Parity;

/// <summary>The engine runs the parity classes share, each shaped as its Python test calls <c>compute_base_totals</c>.</summary>
internal static class EngineRun
{
    /// <summary>Gets the database every build is computed against.</summary>
    public static Pipeline.Database Data { get; } = new(Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, Fixtures.Store.Enums, Fixtures.Store.Config, Fixtures.Store.ServerData);

    /// <summary>Gets the fields the set-bonus, incarnate and sample-build tests leave out: speed caps, stealth and threat.</summary>
    public static IReadOnlySet<string> NotCompared { get; } = new HashSet<string>(["MaxFlySpd", "MaxJumpSpd", "StealthPvE", "StealthPvP", "ThreatLevel", "MaxRunSpd"], StringComparer.Ordinal);

    /// <summary>Gets the slotted inputs without set bonuses or incarnates.</summary>
    /// <param name="inputs">The build.</param>
    /// <returns>The options.</returns>
    public static Pipeline.BuildOptions Slotted(BuildInputs inputs)
    {
        return new Pipeline.BuildOptions { Slots = inputs.Slots, EnhDb = Fixtures.Store.EnhancementEffects, Tables = Fixtures.Store.Maths };
    }

    /// <summary>Computes the totals from the build's raw powers.</summary>
    /// <param name="inputs">The build.</param>
    /// <param name="options">The optional inputs.</param>
    /// <param name="forceLevel">The exemplar level, or null for the dumped configuration.</param>
    /// <returns>The totals.</returns>
    public static BaseTotals Totals(BuildInputs inputs, Pipeline.BuildOptions? options = null, int? forceLevel = null)
    {
        Pipeline.Database data = forceLevel is { } level ? Data with { Config = Data.Config with { ForceLevel = level } } : Data;
        return Pipeline.ComputeBaseTotals(inputs.Powers, inputs.ClassName, data, options);
    }

    /// <summary>
    /// The whole pipeline, as <c>test_sample_builds.py</c> runs it: each power assembled when the dump carries the
    /// assembly files, then slots, set bonuses, incarnates and the build's expression context.
    /// </summary>
    /// <param name="name">The build.</param>
    /// <param name="forceLevel">The exemplar level, or null for the dumped configuration.</param>
    /// <returns>The assembled powers, the totals and the slots.</returns>
    public static (IReadOnlyList<Power> Powers, BaseTotals Totals, BuildInputs Inputs) Full(string name, int? forceLevel = null)
    {
        BuildInputs inputs = new(name);
        BuiltBuild built = DumpScoring.Build(inputs.Fixture.Directory, Fixtures.Store.MidsDirectory, forceLevel: forceLevel);
        return (built.Powers, built.Result, inputs);
    }

    /// <summary>Compares every field of two totals.</summary>
    /// <param name="expected">The dumped totals.</param>
    /// <param name="actual">The engine's totals.</param>
    /// <param name="path">The field path prefix.</param>
    /// <param name="skip">Field names left out, as the Python leaves them out.</param>
    public static void CompareTotals(TotalStatistics expected, TotalStatistics actual, string path, IReadOnlySet<string>? skip = null)
    {
        (string Field, IReadOnlyList<double> Want, IReadOnlyList<double> Got)[] vectors =
        [
            ("Def", expected.Def, actual.Def),
            ("Res", expected.Res, actual.Res),
            ("Mez", expected.Mez, actual.Mez),
            ("MezRes", expected.MezRes, actual.MezRes),
            ("DebuffRes", expected.DebuffRes, actual.DebuffRes),
            ("Elusivity", expected.Elusivity, actual.Elusivity),
        ];
        foreach ((string field, IReadOnlyList<double> want, IReadOnlyList<double> got) in vectors)
        {
            ParityAssert.Equal(want, got, $"{path}.{field}");
        }

        foreach ((string field, double want, double got) in Scalars(expected, actual))
        {
            if (skip is null || !skip.Contains(field))
            {
                ParityAssert.Equal(want, got, $"{path}.{field}");
            }
        }
    }

    /// <summary>The scalar fields, named as <c>totals.json</c> names them.</summary>
    /// <param name="expected">The dumped totals.</param>
    /// <param name="actual">The engine's totals.</param>
    /// <returns>Each field and both values.</returns>
    public static (string Field, double Want, double Got)[] Scalars(TotalStatistics expected, TotalStatistics actual)
    {
        return
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
    }
}
