namespace CohEngine.Core.Expressions;

/// <summary>The build state magnitude expressions and conditional effects read.</summary>
internal sealed record ExpressionContext
{
    /// <summary>Mids' default combat setting; the reference dumps are taken at full health.</summary>
    public const int DefaultPlayerHpPercent = 100;

    /// <summary>Gets <c>Totals.HPMax</c> of the previous recompute.</summary>
    public required double PrevHpMax { get; init; }

    /// <summary>Gets the full names of every power in the build.</summary>
    public required IReadOnlySet<string> BuildPowerNames { get; init; }

    /// <summary>Gets the full names of the StatInclude powers.</summary>
    public required IReadOnlySet<string> ActivePowerNames { get; init; }

    /// <summary>Gets each power's VariableValue by full name.</summary>
    public required IReadOnlyDictionary<string, int> VariableValues { get; init; }

    /// <summary>Gets the build's archetype display name (<c>source.owner&gt;arch</c>), or null when the build has none.</summary>
    public string? ArchetypeDisplayName { get; init; }

    /// <summary>Gets the player's hit points as a percentage.</summary>
    public int PlayerHpPercent { get; init; } = DefaultPlayerHpPercent;
}
