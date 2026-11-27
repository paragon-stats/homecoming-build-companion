namespace CohEngine.Core.Tests.Assembly;

/// <summary>The fields of a hand-made power that only a few tests set, with <c>conftest.py</c>'s defaults.</summary>
internal sealed record PowerExtras
{
    public string FullName { get; init; } = "Test.Test_Set.Test_Power";

    public string PowerSetType { get; init; } = "Primary";

    public string[]? AutoHit { get; init; }

    public double ActivatePeriod { get; init; }

    public int[]? Enhancements { get; init; }

    public bool HasOverride { get; init; }

    public bool AbsorbAttributes { get; init; }

    public string[]? IgnoreEnh { get; init; }

    public double AoeModifier { get; init; } = 1.0;

    public double BaseRechargeTime { get; init; }
}
