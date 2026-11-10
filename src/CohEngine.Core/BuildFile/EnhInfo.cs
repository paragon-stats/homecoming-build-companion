namespace CohEngine.Core.BuildFile;

/// <summary>One enhancement: its nID, type, owning set and the InventO IO-level bounds.</summary>
/// <param name="Nid">The enhancement index.</param>
/// <param name="TypeName">The type name.</param>
/// <param name="SetNid">The owning set index, or -1.</param>
/// <param name="LevelMin">The lowest IO level.</param>
/// <param name="LevelMax">The highest IO level.</param>
public sealed record EnhInfo(int Nid, string TypeName, int SetNid, int LevelMin, int LevelMax);
