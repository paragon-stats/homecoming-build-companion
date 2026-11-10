namespace CohEngine.Core.BuildFile;

/// <summary>A whole build: the picks, the auto-granted powers that carry slots, the incarnates.</summary>
public sealed record BuildSpec
{
    /// <summary>Gets the class name.</summary>
    public required string ClassName { get; init; }

    /// <summary>Gets the origin.</summary>
    public required string Origin { get; init; }

    /// <summary>Gets the alignment.</summary>
    public required string Alignment { get; init; }

    /// <summary>Gets the build name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the comment.</summary>
    public required string Comment { get; init; }

    /// <summary>Gets the power set names.</summary>
    public required IReadOnlyList<string> PowerSets { get; init; }

    /// <summary>Gets the picks.</summary>
    public required IReadOnlyList<PickSpec> Picks { get; init; }

    /// <summary>Gets the inherent powers that carry slots.</summary>
    public IReadOnlyList<PickSpec> Inherents { get; init; } = [];

    /// <summary>Gets the incarnate power names.</summary>
    public IReadOnlyList<string> Incarnates { get; init; } = [];

    /// <summary>Gets the provenance block.</summary>
    public BuildMetaData Meta { get; init; } = Mbd.Homecoming;

    /// <summary>Gets the character level; the file stores it minus one.</summary>
    public int Level { get; init; } = 50;
}
