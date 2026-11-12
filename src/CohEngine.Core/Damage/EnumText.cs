using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>The Python enum values of the model enums.</summary>
public static class EnumText
{
    private static readonly Stance[] Stances = [Stance.Melee, Stance.Ranged, Stance.Hybrid];

    /// <summary>The Python value of a stance.</summary>
    /// <param name="stance">The stance.</param>
    /// <returns>Its text.</returns>
    public static string Value(this Stance stance)
    {
        return stance switch
        {
            Stance.Melee => "melee",
            Stance.Ranged => "ranged",
            _ => "hybrid",
        };
    }

    /// <summary>The Python value of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>Its text.</returns>
    public static string Value(this Role role)
    {
        return role switch
        {
            Role.Chain => "chain",
            Role.Aura => "aura",
            Role.Patch => "patch",
            Role.PetSummon => "pet_summon",
            Role.BuffClick => "buff_click",
            Role.Support => "support",
            _ => "persistent_debuff",
        };
    }

    /// <summary>The Python value of a delivery.</summary>
    /// <param name="delivery">The delivery.</param>
    /// <returns>Its text.</returns>
    public static string Value(this Delivery delivery)
    {
        return delivery == Delivery.Melee ? "melee" : "ranged";
    }

    /// <summary>Parses a stance value.</summary>
    /// <param name="text">The text.</param>
    /// <param name="stance">The stance.</param>
    /// <returns>Whether the text names a stance.</returns>
    public static bool TryParseStance(string text, out Stance stance)
    {
        foreach (Stance candidate in Stances.Where(candidate => string.Equals(candidate.Value(), text, StringComparison.Ordinal)))
        {
            stance = candidate;
            return true;
        }

        stance = Stance.Melee;
        return false;
    }
}
