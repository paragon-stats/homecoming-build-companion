using System.Globalization;
using System.Text.Json;

using CohEngine.Core.Damage;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>The inputs of <see cref="ChainReport.BuildChainSetup"/>.</summary>
public sealed record ChainRequest
{
    /// <summary>Gets the Mids dump build directory.</summary>
    public required string BuildDir { get; init; }

    /// <summary>Gets the shared dump directory holding <c>archetypes.json</c>.</summary>
    public required string MidsDir { get; init; }

    /// <summary>Gets the clicks-off dump, or null.</summary>
    public string? ClicksOffDir { get; init; }

    /// <summary>Gets the canonical data root, or null for the repository's.</summary>
    public string? Canonical { get; init; }

    /// <summary>Gets the directory holding <c>AttribMod.json</c>, or null.</summary>
    public string? MidsDb { get; init; }

    /// <summary>Gets the scenario.</summary>
    public Scenario? Scenario { get; init; }

    /// <summary>Gets the movement mode: run, jump or fly.</summary>
    public string Move { get; init; } = "run";

    /// <summary>Gets the ranged standoff in feet, or null for the default.</summary>
    public double? StandoffFt { get; init; }

    /// <summary>Gets the assumption overrides.</summary>
    public IReadOnlyDictionary<string, double>? Assumptions { get; init; }

    /// <summary>Gets the repository root, or null to locate it.</summary>
    public string? RepoRoot { get; init; }
}
