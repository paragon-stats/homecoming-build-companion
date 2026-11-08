using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Chain;

namespace CohEngine.Core.Names;

/// <summary>One player powerset with the archetypes that can take it and its powers in index order.</summary>
/// <param name="Internal">The internal name.</param>
/// <param name="Display">The display name.</param>
/// <param name="Category">The category.</param>
/// <param name="Archetypes">The archetypes.</param>
/// <param name="Powers">The powers.</param>
public sealed record PowersetName(string Internal, string Display, string Category, IReadOnlyList<string> Archetypes, IReadOnlyList<PowerName> Powers);
