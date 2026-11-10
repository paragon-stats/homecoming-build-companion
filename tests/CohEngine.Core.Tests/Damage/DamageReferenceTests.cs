using System.Globalization;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.DamageLayers;

public sealed class DamageReferenceTests
{
    private static readonly string[] CoreTypes = ["Smashing", "Lethal", "Fire", "Cold", "Energy", "Negative", "Toxic", "Psionic"];

    private static string Structural(string name, int index, params string[] effects)
    {
        return $"{{\"BuildIndex\":{index},\"FullName\":\"{name}\",\"ActivatePeriod\":0.0,\"PowerType\":\"Click\",\"StatInclude\":true,\"Effects\":[{string.Join(",", effects)}]}}";
    }

    private static string Enhanced(int index, params string[] effects)
    {
        return $"{{\"BuildIndex\":{index},\"DamagePerActivation\":10.5,\"CastTime\":1.0,\"RechargeTime\":2.0,\"EndCost\":3.0,\"Effects\":[{string.Join(",", effects)}]}}";
    }

    private static string Structure(int index, string kind, string who, string type)
    {
        return $"{{\"Index\":{index},\"EffectType\":\"{kind}\",\"ToWho\":\"{who}\",\"DamageType\":\"{type}\"}}";
    }

    private static string Magnitude(int index, double mag, double duration)
    {
        return $"{{\"Index\":{index},\"Math_Mag\":{mag.ToString("R", CultureInfo.InvariantCulture)},\"Math_Duration\":{duration.ToString("R", CultureInfo.InvariantCulture)}}}";
    }

    private static TempTree Dump(string enhanced, string structural)
    {
        TempTree tree = new();
        tree.Write("enhanced_powers.json", $"[{enhanced}]");
        tree.Write("powers_effects.json", $"[{structural}]");
        return tree;
    }

    [Fact]
    public void A_missing_file_is_e40()
    {
        using TempTree tree = new();

        Assert.StartsWith("E40:", Assert.Throws<EngineException>(() => DamageReference.LoadDamageRefs(tree.Root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_build_index_in_one_file_only_is_e41()
    {
        using TempTree tree = Dump(Enhanced(1) + "," + Enhanced(3), Structural("A.B.C", 1) + "," + Structural("A.B.D", 2));

        Assert.StartsWith("E41: BuildIndex differs between dump files (only in enhanced_powers.json: [3]; only in powers_effects.json: [2])", Assert.Throws<EngineException>(() => DamageReference.LoadDamageRefs(tree.Root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_effect_missing_from_the_enhanced_file_is_e42()
    {
        using TempTree tree = Dump(Enhanced(1), Structural("A.B.C", 1, Structure(0, "DamageBuff", "Self", "Fire")));

        Assert.StartsWith("E42: A.B.C effect Index 0 (DamageBuff Fire) is in powers_effects.json but not enhanced_powers.json", Assert.Throws<EngineException>(() => DamageReference.LoadDamageRefs(tree.Root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_corrupt_file_is_e43()
    {
        using TempTree tree = Dump(Enhanced(1), Structural("A.B.C", 1));
        tree.Write("enhanced_powers.json", "[oops");

        Assert.StartsWith("E43:", Assert.Throws<EngineException>(() => DamageReference.LoadDamageRefs(tree.Root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_power_carries_its_buffed_figures()
    {
        using TempTree tree = Dump(Enhanced(1), Structural("A.B.C", 1));

        PowerDamageRef reference = DamageReference.LoadDamageRefs(tree.Root)["A.B.C"];

        Assert.Equal("10.5 1 2 3 Click True", $"{reference.DamagePerActivation} {reference.CastTime} {reference.RechargeTime} {reference.EndCost} {reference.PowerType} {reference.StatInclude}");
    }

    [Fact]
    public void Self_damage_buffs_sum_per_type_and_the_largest_type_wins()
    {
        using TempTree tree = Dump(
            Enhanced(1, Magnitude(0, 0.25, 5.0), Magnitude(1, 0.5, 9.0), Magnitude(2, 0.3, 4.0)),
            Structural("A.B.C", 1, Structure(0, "DamageBuff", "Self", "Fire"), Structure(1, "DamageBuff", "Self", "Fire"), Structure(2, "DamageBuff", "Self", "Cold")));

        PowerDamageRef reference = DamageReference.LoadDamageRefs(tree.Root)["A.B.C"];

        Assert.Equal("0.75 9", $"{reference.SelfDamageBuff} {reference.SelfDamageBuffDuration}");
    }

    [Fact]
    public void A_debuff_on_every_core_type_reports_the_weakest_type()
    {
        string[] enhanced = [.. CoreTypes.Select((_, index) => Magnitude(index, -(0.1 * (index + 1)), 10.0 + index))];
        string[] structural = [.. CoreTypes.Select((type, index) => Structure(index, "Resistance", "Target", type))];
        using TempTree tree = Dump(Enhanced(1, enhanced), Structural("A.B.C", 1, structural));

        PowerDamageRef reference = DamageReference.LoadDamageRefs(tree.Root)["A.B.C"];

        Assert.Equal("0.10000000149011612 10", $"{reference.TargetResDebuff} {reference.TargetResDebuffDuration}");
    }

    [Fact]
    public void A_later_type_with_a_larger_self_buff_wins()
    {
        using TempTree tree = Dump(
            Enhanced(1, Magnitude(0, 0.25, 5.0), Magnitude(1, 0.5, 9.0)),
            Structural("A.B.C", 1, Structure(0, "DamageBuff", "Self", "Fire"), Structure(1, "DamageBuff", "Self", "Cold")));

        Assert.Equal(0.5, DamageReference.LoadDamageRefs(tree.Root)["A.B.C"].SelfDamageBuff);
    }

    [Fact]
    public void A_later_type_closer_to_zero_is_the_weakest_debuff()
    {
        string[] enhanced = [.. CoreTypes.Select((_, index) => Magnitude(index, -(0.1 * (8 - index)), 10.0 + index))];
        string[] structural = [.. CoreTypes.Select((type, index) => Structure(index, "Resistance", "Target", type))];
        using TempTree tree = Dump(Enhanced(1, enhanced), Structural("A.B.C", 1, structural));

        Assert.Equal("0.10000000149011612 17", $"{DamageReference.LoadDamageRefs(tree.Root)["A.B.C"].TargetResDebuff} {DamageReference.LoadDamageRefs(tree.Root)["A.B.C"].TargetResDebuffDuration}");
    }

    [Fact]
    public void A_debuff_missing_a_core_type_counts_as_none()
    {
        using TempTree tree = Dump(Enhanced(1, Magnitude(0, -0.5, 10.0)), Structural("A.B.C", 1, Structure(0, "Resistance", "Target", "Fire")));

        Assert.Equal(0.0, DamageReference.LoadDamageRefs(tree.Root)["A.B.C"].TargetResDebuff);
    }

    [Fact]
    public void Effects_of_other_kinds_and_positional_types_are_ignored()
    {
        using TempTree tree = Dump(Enhanced(1), Structural("A.B.C", 1, Structure(0, "DamageBuff", "Self", "Melee"), Structure(1, "Defense", "Self", "Fire")));

        Assert.Equal(0.0, DamageReference.LoadDamageRefs(tree.Root)["A.B.C"].SelfDamageBuff);
    }
}
