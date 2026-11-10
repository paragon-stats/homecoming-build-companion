using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Chain;

namespace CohEngine.Core.Names;

/// <summary>One player power: its full internal name, the name the game shows, and its powerset.</summary>
/// <param name="Internal">The full internal name.</param>
/// <param name="Display">The display name.</param>
/// <param name="Powerset">The powerset's internal name.</param>
public sealed record PowerName(string Internal, string Display, string Powerset);
