using CohEngine.Core.Data;

namespace CohEngine.TestSupport;

/// <summary>A tiny canonical tree, class table and AttribMod tables for the pet tests.</summary>
public static class PetWorld
{
    /// <summary>The entity every summon in the tree creates.</summary>
    public const string EntityJson = "{\"name\":\"Thing\",\"defaults\":{\"character_class_name\":\"minion_pets\",\"power_full_names\":[\"Pet.Set.Bite\"]},\"levels\":[{\"display_names\":[\"Imp\"]}]}";

    /// <summary>The class table: one pet class, then a caster class.</summary>
    /// <returns>The class table.</returns>
    public static ArchetypeDb Classes()
    {
        return new ArchetypeDb([Class(0, "Class_Minion_Pets", 0), Class(1, "Class_Caster", 1)]);
    }

    /// <summary>Tables of 50 identical rows: the pet level (49), the melee damage (10) and Ones (1).</summary>
    /// <param name="level">The pet level the level table reports.</param>
    /// <returns>The tables.</returns>
    public static AttribMods Mods(double level = 49.0)
    {
        return new AttribMods([Table("Pet_Levelminus1", level), Table("Melee_Damage", 10.0), Table("Ones", 1.0)]);
    }

    /// <summary>Writes a summon power that creates the entity, and the entity with a single damaging pet power.</summary>
    /// <param name="tree">The scratch tree to use as the canonical root.</param>
    /// <param name="table">The pet-level table the summon names.</param>
    /// <param name="duration">The entity duration text.</param>
    /// <param name="effectExtra">More members of the Create_Entity effect group, starting with a comma.</param>
    public static void Write(TempTree tree, string table = "Pet_Levelminus1", string duration = "60 seconds", string effectExtra = "")
    {
        ArgumentNullException.ThrowIfNull(tree);
        string create = CanonicalPowers.Template(["Create_Entity"], "Absolute", "Self", table, 1.0, $",\"duration\":\"{duration}\",\"params\":{{\"entity_def\":\"Thing\"}}");
        tree.Write("powers/mm/summon/spawn.json", CanonicalPowers.Text("Mm.Summon.Spawn", effects: [CanonicalPowers.Effect([create], effectExtra)], categories: ["Pet Damage"]));
        tree.Write("entities/thing.json", EntityJson);
        tree.Write("powers/pet/set/bite.json", Bite());
    }

    /// <summary>A pet attack power: a click dealing foe damage through the melee table.</summary>
    /// <param name="effectExtra">More members of the damage effect group, starting with a comma.</param>
    /// <param name="templateExtra">More members of the damage template, starting with a comma.</param>
    /// <returns>The power JSON.</returns>
    public static string Bite(string effectExtra = "", string templateExtra = "")
    {
        string hit = CanonicalPowers.Template(["Smashing_Dmg"], "Absolute", "Foe", "Melee_Damage", 1.0, ",\"magnitude\":1.0,\"duration\":\"0 seconds\",\"application_period\":0.0" + templateExtra);
        return CanonicalPowers.Text("Pet.Set.Bite", effects: [CanonicalPowers.Effect([hit], ",\"chance\":1.0,\"ppm\":0.0" + effectExtra)], extra: ",\"activation_time\":1.0,\"recharge_time\":4.0,\"activate_period\":0.0");
    }

    private static Archetype Class(int index, string name, int column)
    {
        return new Archetype
        {
            Index = index,
            ClassName = name,
            DisplayName = name,
            ClassType = "Hero",
            Column = column,
            Playable = true,
            Hitpoints = 1000,
            HpCap = 1500.0,
            ResCap = 0.75,
            DamageCap = 4.0,
            RechargeCap = 5.0,
            RecoveryCap = 5.0,
            RegenCap = 20.0,
            PerceptionCap = 1153.0,
            BaseRecovery = 1.67,
            BaseRegen = 1.0,
            BaseThreat = 1.0,
            Ancillary = [],
            InherentPowers = [],
        };
    }

    private static ModifierTable Table(string name, double value)
    {
        return new ModifierTable { Id = name, BaseIndex = 0, Table = [.. Enumerable.Repeat<IReadOnlyList<double>>([value, value], 50)] };
    }
}
