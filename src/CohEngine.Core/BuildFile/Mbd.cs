namespace CohEngine.Core.BuildFile;

/// <summary>The <c>.mbd</c> reader, writer and the resolution of a build against the Mids dump.</summary>
public static class Mbd
{
    /// <summary>Gets the eEnhRelative ordinals as the <c>.mbd</c> and <c>.mxd</c> name them.</summary>
    public static IReadOnlyList<string> EnhRelative { get; } =
    [
        "None",
        "MinusThree",
        "MinusTwo",
        "MinusOne",
        "Even",
        "PlusOne",
        "PlusTwo",
        "PlusThree",
        "PlusFour",
        "PlusFive",
    ];

    /// <summary>Gets the eEnhGrade ordinals as the <c>.mbd</c> and <c>.mxd</c> name them.</summary>
    public static IReadOnlyList<string> EnhGrade { get; } = ["None", "TrainingO", "DualO", "SingleO"];
}
