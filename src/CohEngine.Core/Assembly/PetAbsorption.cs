using CohEngine.Core.Data;

namespace CohEngine.Core.Assembly;

/// <summary><c>Power.AbsorbPetEffects</c> with a stack count of 1.</summary>
internal static class PetAbsorption
{
    /// <summary>Absorbs the summoned pets' effects into <paramref name="effects"/>.</summary>
    /// <param name="power">The summoning power.</param>
    /// <param name="effects">Its effects so far, extended in place.</param>
    /// <param name="inputs">The assembly inputs.</param>
    /// <returns>The power, with any attributes it took.</returns>
    public static AssemblyPower AbsorbPets(AssemblyPower power, List<Effect> effects, AssemblyInputs inputs)
    {
        if (!(power.AbsorbSummonEffects || power.AbsorbSummonAttributes))
        {
            return power;
        }

        List<Effect> creates = [.. effects.Where(effect => string.Equals(effect.EffectType, "EntCreate", StringComparison.Ordinal) && effect.NSummon > -1 && Math.Abs(effect.Probability - 1) < 0.01)];

        // The attribute loop stops at the first pet power unless the database power hits only its caster or no one.
        bool firstOnly = !(power.EntitiesAutoHit.Count == 0 || Absorption.IsOnly(power.EntitiesAutoHit, "Caster"));
        foreach (Effect create in creates)
        {
            AssemblyEntity entity = Absorption.Entity(inputs.Entities, create.NSummon, power.FullName);
            if (entity.PowersetNids.Count == 0)
            {
                continue;
            }

            if (power.AbsorbSummonAttributes && entity.PowersetNids[0] > -1)
            {
                power = TakeAttributes(power, entity.PowersetNids[0], firstOnly, inputs);
            }

            if (!power.AbsorbSummonEffects)
            {
                continue;
            }

            foreach (int setNid in entity.PowersetNids)
            {
                if (setNid < 0)
                {
                    continue;
                }

                foreach (AssemblyPower pet in Absorption.PetPowers(setNid, power.FullName, inputs))
                {
                    effects.AddRange(AbsorbPetPower(pet, create, entity, inputs));
                }
            }
        }

        return power;
    }

    /// <summary>The pet powers' targeting and ignored aspects copied onto the absorbing power, the last copy winning.</summary>
    private static AssemblyPower TakeAttributes(AssemblyPower power, int powersetNid, bool firstOnly, AssemblyInputs inputs)
    {
        foreach (AssemblyPower pet in Absorption.PetPowers(powersetNid, power.FullName, inputs))
        {
            power = PowerAssembler.WithTargets(power, pet.EntitiesAffected, power.EntitiesAutoHit.Count != 0 ? pet.EntitiesAutoHit : power.EntitiesAutoHit, pet.IgnoreEnh);
            if (firstOnly)
            {
                break;
            }
        }

        return power;
    }

    /// <summary>One pet power's effects as the absorbing power takes them, then those of the entities it creates.</summary>
    private static List<Effect> AbsorbPetPower(AssemblyPower pet, Effect create, AssemblyEntity entity, AssemblyInputs inputs)
    {
        List<Effect> Take(AssemblyPower source)
        {
            return Absorption.Absorb(source, create.Duration, entity.ClassNid, inputs, delay: create.DelayedTime, grant: false);
        }

        List<Effect> output = Take(pet);
        foreach (Effect spawn in pet.Effects)
        {
            if (!string.Equals(spawn.EffectType, "EntCreate", StringComparison.Ordinal) || spawn.NSummon <= -1)
            {
                continue;
            }

            AssemblyEntity child = Absorption.Entity(inputs.Entities, spawn.NSummon, pet.FullName);
            if (child.PowersetNids.Count == 0 || child.PowersetNids[0] < 0)
            {
                continue;
            }

            foreach (AssemblyPower power in Absorption.PetPowers(child.PowersetNids[0], pet.FullName, inputs))
            {
                output.AddRange(Take(power));
            }
        }

        return output;
    }
}
