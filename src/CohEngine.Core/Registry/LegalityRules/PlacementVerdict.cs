namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>The one placement decision, read by both the predicate and the diagnostic.</summary>
internal enum PlacementVerdict
{
    /// <summary>The power accepts the enhancement.</summary>
    Valid,

    /// <summary>A set IO whose set is not in the set database.</summary>
    UnknownSet,

    /// <summary>A set IO whose set type the power does not accept.</summary>
    SetNotAccepted,

    /// <summary>A standard IO whose class the power does not accept.</summary>
    ClassNotAccepted,
}
