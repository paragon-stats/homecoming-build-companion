using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CohEngine.Core.Chain;

/// <summary>Looks a power or entity up by name; null when the data has none.</summary>
/// <param name="name">A power full name (dotted) or an entity definition name.</param>
/// <returns>The record, or null.</returns>
public delegate JsonElement? PowerResolver(string name);
