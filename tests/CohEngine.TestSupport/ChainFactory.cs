using CohEngine.Core.DamageLayers;

namespace CohEngine.TestSupport;

/// <summary>Synthetic chain-model records with the Python suite's defaults.</summary>
public static class ChainFactory
{
    /// <summary>A profile for a power delivered at range from a single target.</summary>
    /// <param name="role">The role.</param>
    /// <param name="delivery">The delivery.</param>
    /// <param name="shape">The shape.</param>
    /// <param name="maxTargets">The target cap.</param>
    /// <param name="modes">The modes required.</param>
    /// <param name="name">The full name.</param>
    /// <returns>The profile.</returns>
    public static AttackProfile Profile(Role role = Role.Chain, Delivery delivery = Delivery.Ranged, Shape shape = Shape.SingleTarget, int maxTargets = 0, string[]? modes = null, string name = "Test.Set.Power")
    {
        return new AttackProfile(name, delivery, shape, role, 80.0, 0.0, 0.0, maxTargets, [], modes ?? []);
    }

    /// <summary>An attack with a profile of the given role.</summary>
    /// <param name="name">The full name.</param>
    /// <param name="role">The role.</param>
    /// <param name="damage">The damage per activation.</param>
    /// <param name="cast">The cast time.</param>
    /// <param name="recharge">The recharge time.</param>
    /// <param name="end">The endurance cost.</param>
    /// <param name="delivery">The delivery.</param>
    /// <param name="shape">The shape.</param>
    /// <returns>The attack.</returns>
    public static ChainAttack Attack(string name, Role role = Role.Chain, double damage = 100.0, double cast = 1.0, double recharge = 5.0, double end = 4.0, Delivery delivery = Delivery.Ranged, Shape shape = Shape.SingleTarget)
    {
        return new ChainAttack
        {
            FullName = name,
            Damage = damage,
            CastTime = cast,
            RechargeTime = recharge,
            EndCost = end,
            Profile = Profile(role, delivery, shape, name: name),
        };
    }

    /// <summary>A context over the attacks with a roomy endurance budget and a 40 ft standoff at 20 ft/s.</summary>
    /// <param name="attacks">The attacks.</param>
    /// <param name="stance">The stance.</param>
    /// <param name="archetypeClass">The class name.</param>
    /// <param name="assumptions">The assumptions.</param>
    /// <returns>The context.</returns>
    public static DamageContext Context(IReadOnlyList<ChainAttack> attacks, Stance stance = Stance.Melee, string archetypeClass = "Class_Test", IReadOnlyDictionary<string, double>? assumptions = null)
    {
        ArgumentNullException.ThrowIfNull(attacks);
        return new DamageContext
        {
            Attacks = attacks,
            Budget = new EnduranceBudget(100.0, 4.0, 1.0),
            Stance = stance,
            Scenario = new Scenario(),
            Movement = new Movement(20.0, 40.0),
            ArchetypeClass = archetypeClass,
            Assumptions = assumptions ?? new Dictionary<string, double>(StringComparer.Ordinal),
        };
    }
}
