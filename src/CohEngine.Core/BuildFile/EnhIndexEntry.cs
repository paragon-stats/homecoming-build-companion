namespace CohEngine.Core.BuildFile;

/// <summary>An enhancement's identity and type, keyed by <c>StaticIndex</c>.</summary>
/// <param name="Uid">The enhancement UID.</param>
/// <param name="TypeId">The type name; it decides how many bytes follow each reference in a <c>.mxd</c>.</param>
public sealed record EnhIndexEntry(string Uid, string TypeId);
