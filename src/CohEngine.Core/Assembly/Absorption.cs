using CohEngine.Core.Data;

namespace CohEngine.Core.Assembly;

/// <summary><c>Power.AbsorbEffects</c> and the pet records it reads.</summary>
internal static class Absorption
{
    /// <summary><c>Power.AbsorbEffects</c>: all of the source's effects, or only <paramref name="chosen"/>, as absorbed copies.</summary>
    /// <param name="source">The power absorbed.</param>
    /// <param name="duration">The absorbing effect's duration.</param>
    /// <param name="classId">The class row the copies read.</param>
    /// <param name="inputs">The assembly inputs.</param>
    /// <param name="delay">The delay the copies take, if positive.</param>
    /// <param name="grant">Whether a grant absorbs, which keeps every effect.</param>
    /// <param name="chosen">The one effect to absorb, or null for all.</param>
    /// <returns>The absorbed copies.</returns>
    public static List<Effect> Absorb(AssemblyPower source, double duration, int classId, AssemblyInputs inputs, double delay = 0.0, bool grant = true, Effect? chosen = null)
    {
        double petDuration = PetDuration(source, inputs);
        if (petDuration > 0 && (duration < 0.01 || duration > petDuration))
        {
            duration = petDuration;
        }

        List<Effect> picked = chosen is null
            ? [.. source.Effects.Where(effect => grant || !IsOnly(source.EntitiesAffected, "Caster") || string.Equals(effect.EffectType, "EntCreate", StringComparison.Ordinal))]
            : [chosen];
        IReadOnlySet<string> autoHit = source.EntitiesAutoHit;
        bool toTarget = (autoHit.Contains("Friend") || autoHit.Contains("MyPet")) && (chosen is not null || !autoHit.Contains("Caster"));
        bool ticking = source.PowerType is "Auto_" or "Toggle";
        List<Effect> output = [];
        foreach (Effect effect in picked)
        {
            output.Add(effect with
            {
                AbsorbedEffect = true,
                AbsorbedPowerType = source.PowerType,
                AbsorbedClassNId = classId,
                AbsorbedPowerNid = source.Nid,
                Ticks = ticking ? SetTicks(duration, source.ActivatePeriod) : effect.Ticks,

                // Stacking Yes multiplies scale by the stack count, which assembly always passes as 1.
                ToWho = toTarget ? "Target" : effect.ToWho,
                DelayedTime = delay > 0 ? delay : effect.DelayedTime,
                NDuration = duration > 0 && petDuration > 0 ? duration : effect.NDuration,
            });
        }

        return output;
    }

    /// <summary>A pet powerset's powers in order, its empty slots left out.</summary>
    /// <param name="nid">The powerset index.</param>
    /// <param name="where">What reads it, for the message.</param>
    /// <param name="inputs">The assembly inputs.</param>
    /// <returns>The powers.</returns>
    /// <exception cref="NotSupportedException">E89 when the export does not hold the powerset.</exception>
    public static List<AssemblyPower> PetPowers(int nid, string where, AssemblyInputs inputs)
    {
        if (!inputs.Entities.Powersets.TryGetValue(nid, out AssemblyPowerset? powerset))
        {
            throw new NotSupportedException(
                $"E89: {where} needs powerset {nid}, which assembly_entities.json does not hold; expected every "
                + "powerset pet absorption reads; re-run the Mids dump harness so the export carries it");
        }

        return [.. powerset.PowerNids.Where(power => power > -1).Select(power => AssemblyInputs.Record(inputs.Powers, power, "pet power", powerset.FullName))];
    }

    /// <summary>A summoned entity the export must hold.</summary>
    /// <param name="entities">The entities.</param>
    /// <param name="nid">The entity index.</param>
    /// <param name="where">What creates it, for the message.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="NotSupportedException">E89 when the export does not hold the entity.</exception>
    public static AssemblyEntity Entity(AssemblyEntities entities, int nid, string where)
    {
        if (entities.Entities.TryGetValue(nid, out AssemblyEntity? entity))
        {
            return entity;
        }

        throw new NotSupportedException(
            $"E89: {where} creates entity {nid}, which assembly_entities.json does not hold; expected every "
            + "entity pet absorption reads; re-run the Mids dump harness so the export carries it");
    }

    /// <summary>Whether a set holds exactly one name.</summary>
    /// <param name="set">The set.</param>
    /// <param name="name">The name.</param>
    /// <returns>True for the one-element set.</returns>
    public static bool IsOnly(IReadOnlySet<string> set, string name)
    {
        return set.Count == 1 && set.Contains(name);
    }

    private static int SetTicks(double duration, double interval)
    {
        return interval > 0 ? 1 + (int)Math.Floor(duration / interval) : 0;
    }

    /// <summary>The delay of the last self SilentKill in a Pet source's powerset: the duration its effects are cut to.</summary>
    private static double PetDuration(AssemblyPower source, AssemblyInputs inputs)
    {
        if (!string.Equals(source.PowerSetType, "Pet", StringComparison.Ordinal))
        {
            return 0.0;
        }

        AssemblyPowerset owner = inputs.Entities.Powersets.Values.FirstOrDefault(set => set.PowerNids.Contains(source.Nid))
            ?? throw new NotSupportedException(
                $"E89: {source.FullName} is a Pet power whose powerset assembly_entities.json does not hold; "
                + "expected its powerset for the pet duration rule; re-run the Mids dump harness so the export carries it");
        double duration = 0.0;
        foreach (AssemblyPower power in PetPowers(owner.Nid, source.FullName, inputs))
        {
            foreach (Effect effect in power.Effects)
            {
                if (string.Equals(effect.EffectType, "SilentKill", StringComparison.Ordinal) && string.Equals(effect.ToWho, "Self", StringComparison.Ordinal) && effect.DelayedTime > 0)
                {
                    duration = effect.DelayedTime;
                }
            }
        }

        return duration;
    }
}
