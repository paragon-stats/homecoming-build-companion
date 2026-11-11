using CohEngine.Core.Data;
using CohEngine.Core.Expressions;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Parity;

/// <summary>The engine inputs one build's dump yields, shaped as the Python parity tests build them.</summary>
internal sealed class BuildInputs
{
    public BuildInputs(string name)
    {
        Fixture = Fixtures.Store.Build(name);
        Powers = Fixture.Powers;
        Slots = Fixture.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots);
        ClassName = Fixture.Totals.Class;
        ArchetypeIndex = Fixtures.Store.Archetypes.NidFromUidClass(ClassName);
        Expression = new ExpressionContext
        {
            PrevHpMax = 0.0,
            ArchetypeDisplayName = Fixtures.Store.Archetypes.Classes[ArchetypeIndex].DisplayName,
            BuildPowerNames = Powers.Select(power => power.FullName).ToHashSet(StringComparer.Ordinal),
            ActivePowerNames = Powers.Where(power => power.StatInclude).Select(power => power.FullName).ToHashSet(StringComparer.Ordinal),
            VariableValues = Powers.ToLastWinsDictionary(power => power.FullName, power => power.VariableValue),
        };
    }

    public BuildFixture Fixture { get; }

    public IReadOnlyList<Power> Powers { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> Slots { get; }

    public string ClassName { get; }

    public int ArchetypeIndex { get; }

    public ExpressionContext Expression { get; }

    public Dictionary<int, bool> StatInclude => Powers.ToLastWinsDictionary(power => power.BuildIndex, power => power.StatInclude);
}
