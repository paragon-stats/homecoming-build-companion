namespace CohEngine.Core.Registry;

/// <summary>Which registries <c>validate</c> folds over a build.</summary>
[Flags]
public enum ValidateChecks
{
    /// <summary>The hard-limit rules.</summary>
    HardLimits = 1,

    /// <summary>The legality rules.</summary>
    Legality = 2,

    /// <summary>The slotting rules, which only <c>--slotting</c> and the slotting tool ask for.</summary>
    Slotting = 4,

    /// <summary>The hard-limit and legality registries.</summary>
    All = HardLimits | Legality,
}
