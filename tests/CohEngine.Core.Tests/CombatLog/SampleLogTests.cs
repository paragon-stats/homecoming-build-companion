using CohEngine.Core.CombatLog;

namespace CohEngine.Core.Tests.CombatLog;

public sealed class SampleLogTests
{
    private static readonly string[] PlantedUnknowns =
    [
        "You increase your regeneration rate and gain an endurance discount.",
        "Your Energy Drain increases your defense!",
    ];

    private static readonly Lazy<CombatEvent[]> Sample = new(() =>
    {
        string path = Path.Join(Fixtures.Store.Root, "combatlog", "sample-trimmed.log");
        return [.. new CombatLogClassifier("Player").ClassifyAll(CombatLogReader.ReadFile(path))];
    });

    [Fact]
    public void The_sample_classifies_with_only_the_two_planted_lines_unknown()
    {
        string[] unknown = [.. Sample.Value.OfType<UnknownEvent>().Select(combatEvent => combatEvent.Line.Text)];

        Assert.Equal(PlantedUnknowns, unknown, StringComparer.Ordinal);
    }

    [Fact]
    public void Every_event_type_appears_in_the_sample()
    {
        HashSet<Type> concrete =
        [
            .. typeof(CombatEvent).Assembly.GetTypes().Where(type => type.IsAssignableTo(typeof(CombatEvent)) && !type.IsAbstract),
        ];
        HashSet<Type> seen = [.. Sample.Value.Select(combatEvent => combatEvent.GetType())];

        Assert.Equal(concrete.Select(type => type.Name).Order(StringComparer.Ordinal), seen.Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_recognised_event_class_appears_at_least_twice()
    {
        var thin = Sample.Value
            .OfType<RecognisedEvent>()
            .GroupBy(combatEvent => combatEvent.GetType().Name, StringComparer.Ordinal)
            .Where(group => group.Count() < 2)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(thin);
    }

    [Fact]
    public void The_tactics_notice_in_the_sample_is_ignored()
    {
        CombatEvent[] tactics =
        [
            .. Sample.Value.Where(combatEvent => combatEvent.Line.Text.Contains("increases your team's chance to hit", StringComparison.Ordinal)),
        ];

        Assert.Equal(3, tactics.Length);
        Assert.All(tactics, combatEvent => Assert.Equal(CombatOutcome.Ignored, combatEvent.Outcome));
    }

    [Fact]
    public void Self_targeted_rolls_in_the_sample_are_flagged_and_foe_rolls_are_not()
    {
        AttackRoll[] rolls = [.. Sample.Value.OfType<AttackRoll>()];

        Assert.Equal(6, rolls.Count(roll => roll.IsSelf));
        Assert.All(rolls.Where(roll => roll.IsSelf), roll => Assert.Equal("Player", roll.Counterpart));
        Assert.All(rolls.Where(roll => roll.Counterpart.StartsWith("Big Mob", StringComparison.Ordinal)), roll => Assert.False(roll.IsSelf));
        Assert.Contains(rolls, roll => roll.IsSelf && roll.Direction == CombatDirection.Outgoing && roll.Autohit);
        Assert.Contains(rolls, roll => roll.IsSelf && roll.Direction == CombatDirection.Incoming && roll.Autohit);
    }

    [Fact]
    public void The_sample_holds_a_streakbreaker_hit_a_crit_a_proc_and_a_dot()
    {
        Assert.Contains(Sample.Value.OfType<AttackRoll>(), roll => roll.ForcedByStreakbreaker);
        Assert.Contains(Sample.Value.OfType<DamageHit>(), damage => string.Equals(damage.Modifier, "JUDGEMENT CRITICAL", StringComparison.Ordinal));
        Assert.Contains(Sample.Value.OfType<DamageHit>(), damage => damage.IsProc);
        Assert.Contains(Sample.Value.OfType<DamageHit>(), damage => damage.IsDot);
    }
}
