namespace CohEngine.Core.SetBonuses;

/// <summary>One set slotted in one power (<c>I9SetData.sSetInfo</c>), mutable while the build is tallied.</summary>
/// <param name="setIdx">The set index.</param>
internal sealed class SetInfo(int setIdx)
{
    /// <summary>Gets the set index.</summary>
    public int SetIdx { get; } = setIdx;

    /// <summary>Gets or sets the number of the set's pieces slotted in the power.</summary>
    public int SlottedCount { get; set; }

    /// <summary>Gets the slotted pieces, in slot order.</summary>
    public List<int> EnhIndexes { get; } = [];

    /// <summary>Gets or sets the activated set_bonus power ids.</summary>
    public List<int> PowerIds { get; set; } = [];
}
