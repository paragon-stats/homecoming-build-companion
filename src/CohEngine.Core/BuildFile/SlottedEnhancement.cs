namespace CohEngine.Core.BuildFile;

/// <summary>A slotted enhancement.</summary>
/// <param name="Uid">The enhancement UID.</param>
/// <param name="Grade">The eEnhGrade name.</param>
/// <param name="IoLevel">The IO level.</param>
/// <param name="RelativeLevel">The eEnhRelative name.</param>
/// <param name="Obtained">Whether the enhancement is marked obtained.</param>
public sealed record SlottedEnhancement(string Uid, string Grade = "None", int IoLevel = 1, string RelativeLevel = "Even", bool Obtained = false);
