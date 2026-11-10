namespace CohEngine.Core.BuildFile;

/// <summary>The <c>powers.json</c> fields the build database reads.</summary>
internal sealed record PowerDbRecord
{
    public required string FullName { get; init; }

    public required int NIDPower { get; init; }

    public required int StaticIndex { get; init; }

    public required string PowerType { get; init; }

    public required int Level { get; init; }

    public required IReadOnlyList<int> SetTypes { get; init; }

    public required IReadOnlyList<int> Enhancements { get; init; }

    public required IReadOnlyList<IReadOnlyList<string>> RequiresPowers { get; init; }

    public required IReadOnlyList<IReadOnlyList<string>> RequiresPowersNot { get; init; }

    public required IReadOnlyList<string> RequiresClass { get; init; }

    public required IReadOnlyList<string> RequiresClassNot { get; init; }

    public required string PowerSetType { get; init; }
}
