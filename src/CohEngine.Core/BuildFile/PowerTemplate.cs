namespace CohEngine.Core.BuildFile;

/// <summary>A power from <c>powers.json</c>: the database fields, without per-build state or effects.</summary>
/// <param name="FullName">The power's full name.</param>
/// <param name="Nid">The power's database index.</param>
/// <param name="StaticIndex">The power's static index.</param>
/// <param name="PowerType">The ePowerType name.</param>
/// <param name="Level">The database minimum pick level.</param>
/// <param name="SetTypes">The eSetType ordinals the power accepts.</param>
/// <param name="Enhancements">The EnhancementClass ids the power accepts.</param>
/// <param name="RequiresPowers">The prerequisite groups.</param>
/// <param name="RequiresPowersNot">The forbidden groups.</param>
/// <param name="RequiresClass">The classes the power requires.</param>
/// <param name="RequiresClassNot">The classes the power excludes.</param>
/// <param name="PowerSetType">The power set type name.</param>
public sealed record PowerTemplate(
    string FullName,
    int Nid,
    int StaticIndex,
    string PowerType,
    int Level,
    IReadOnlyList<int> SetTypes,
    IReadOnlyList<int> Enhancements,
    IReadOnlyList<IReadOnlyList<string>> RequiresPowers,
    IReadOnlyList<IReadOnlyList<string>> RequiresPowersNot,
    IReadOnlyList<string> RequiresClass,
    IReadOnlyList<string> RequiresClassNot,
    string PowerSetType);
