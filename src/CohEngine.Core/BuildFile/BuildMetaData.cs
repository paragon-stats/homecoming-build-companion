namespace CohEngine.Core.BuildFile;

/// <summary>The <c>BuiltWith</c> block: app and database provenance.</summary>
/// <param name="App">The application name.</param>
/// <param name="Version">The application version.</param>
/// <param name="Database">The database name.</param>
/// <param name="DatabaseVersion">The database version.</param>
public sealed record BuildMetaData(string App, string Version, string Database, string DatabaseVersion);
