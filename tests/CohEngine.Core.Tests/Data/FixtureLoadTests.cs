using System.Text.Json;

using CohEngine.Core.Data;

namespace CohEngine.Core.Tests.Data;

[Trait("Category", "Fixtures")]
public sealed class FixtureLoadTests
{
    // Printed once by the Python loaders (load_powers_effects, load_build_slots, load_incarnates,
    // load_build_power_assembly, load_assembled_powers) over each build: powers, effects, slots, the sum of
    // f32(Scale) over every effect, powers not Chosen, requirement names after dropping blanks, incarnates,
    // incarnate sub-power effects; then, for the assembly-era dumps, joined powers, conditionals, ProcInclude
    // powers, assembled powers and assembled effects.
    private static readonly Dictionary<string, (int[] Counts, double ScaleSum, int[] Assembly)> Python = new(StringComparer.Ordinal)
    {
        ["Hollowmind - Dominator (Darkness Control - Psionic Assault)"] = ([34, 280, 98, 10, 58, 0, 0], -6508.73099225387, [34, 61, 0, 28, 206]),
        ["Laser - ALT+F4 - Controller (Illusion Control - Time Manipulation)"] = ([34, 322, 98, 10, 13, 0, 0], -8473.232167998329, [34, 38, 0, 28, 315]),
        ["Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)"] = ([33, 375, 98, 9, 8, 0, 0], -9769.838300775737, [33, 57, 0, 29, 329]),
        ["Laser - Accretion - Scrapper (Dark Melee - Shield Defense)"] = ([35, 413, 98, 11, 61, 0, 0], -9763.307191612199, [35, 125, 0, 30, 413]),
        ["Laser - Fallout - Sentinel (Fire Blast - Radiation Armor)"] = ([12, 73, 10, 9, 0, 0, 0], -10133.557000037283, [12, 7, 0, 7, 16]),
        ["Laser - HAZMAT - PvE - Brute (Radiation Melee - Fiery Aura)"] = ([50, 416, 103, 26, 17, 5, 50], -9729.410621963441, [50, 51, 0, 39, 460]),
        ["Laser - NUKE - Corruptor (Fire Blast - Radiation Emission)"] = ([35, 339, 98, 11, 65, 0, 0], -5368.604772558436, [35, 70, 5, 29, 342]),
        ["Laser - PRIME - Blaster (Energy Blast - Energy Manipulation)"] = ([37, 393, 98, 13, 67, 1, 42], -5211.053490880877, [37, 87, 7, 28, 366]),
        ["Laser - QUANTUM - Tanker (Super Reflexes - Energy Melee)"] = ([42, 378, 98, 18, 21, 4, 30], -9609.680998882279, [42, 54, 0, 32, 361]),
        ["Melee Arachnos Widow (Night Widow Training - Widow Teamwork)"] = ([33, 461, 98, 9, 18, 0, 0], -8183.364050041884, [33, 166, 0, 29, 374]),
        ["Petless Mastermind (Demon Summoning - Force Field)"] = ([34, 500, 98, 10, 33, 0, 0], -8951.783750299364, [34, 156, 0, 26, 379]),
        ["Stalker (Fiery Melee - Invulnerability)"] = ([34, 423, 98, 10, 11, 0, 0], -7535.1079298220575, [34, 94, 0, 30, 403]),
        ["arsenal_dominator_cardiac"] = ([36, 365, 31, 0, 59, 1, 20], -9718.347001144662, []),
        ["arsenal_dominator_incarnate"] = ([36, 365, 32, 0, 59, 1, 30], -9718.347001144662, []),
        ["arsenal_dominator_incarnate_slotted"] = ([36, 365, 37, 0, 59, 1, 30], -9718.347001144662, []),
        ["arsenal_dominator_noslots"] = ([35, 357, 32, 0, 0, 0, 0], -9726.347001144662, []),
        ["hasten_2rech"] = ([35, 413, 99, 0, 0, 0, 0], -9763.307191612199, []),
        ["shield_scrapper_global_endrdx"] = ([35, 413, 100, 0, 0, 0, 0], -9763.307191612199, []),
        ["shield_scrapper_mixed_sets"] = ([35, 413, 99, 0, 0, 0, 0], -9763.307191612199, []),
        ["shield_scrapper_noslots"] = ([35, 413, 98, 0, 0, 0, 0], -9763.307191612199, []),
        ["shield_scrapper_set_bonuses"] = ([35, 413, 95, 0, 0, 0, 0], -9763.307191612199, []),
        ["shield_scrapper_set_bonuses_exemplar25"] = ([35, 413, 95, 0, 0, 0, 0], -9763.307191612199, []),
        ["shield_scrapper_slotted"] = ([35, 413, 99, 0, 0, 0, 0], -9763.307191612199, []),
        ["wpss_tanker_leadership"] = ([35, 386, 98, 11, 13, 1, 28], -17873.936132313684, []),
    };

    public static TheoryData<string> Builds => Fixtures.Builds();

    [Fact]
    public void Every_build_directory_is_enumerated()
    {
        Assert.Equal(Python.Keys.Order(StringComparer.Ordinal), Fixtures.Store.BuildNames(), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Every_file_of_a_build_loads_with_the_dump_record_counts(string name)
    {
        BuildFixture build = Fixtures.Store.Build(name);

        Assert.Equal(name, build.Name);
        Assert.Equal(ArrayLength(build, "powers_effects.json"), build.Powers.Count);
        Assert.Equal(ArrayLength(build, "slots.json"), build.Slots.Count);
        Assert.Equal(ArrayLength(build, "enhanced_powers.json"), build.EnhancedPowers.Count);
        Assert.Equal(ArrayLength(build, "incarnates.json"), build.Incarnates.Count);
        Assert.Equal(build.SetBonusVirtualPower.EffectCount, build.SetBonusVirtualPower.Effects.Count);
        Assert.Equal(16, build.Totals.Totals.Def.Count);
        Assert.Equal(16, build.Totals.TotalsCapped.Res.Count);
        if (build.Has("power_assembly.json"))
        {
            Assert.Equal(ArrayLength(build, "assembled_powers.json"), build.AssembledPowers.Count);
            Assert.Equal(build.Powers.Count, build.PowerAssembly.Count);
        }
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void A_build_loads_the_values_the_python_loaders_read(string name)
    {
        BuildFixture build = Fixtures.Store.Build(name);
        (int[] counts, double scaleSum, int[] assembly) = Python[name];
        Effect[] effects = [.. build.Powers.SelectMany(power => power.Effects)];

        int[] actual =
        [
            build.Powers.Count,
            effects.Length,
            build.Slots.Sum(entry => entry.Slots.Count),
            build.Powers.Count(power => !power.Chosen),
            build.Powers.Sum(power => power.RequiresPowers.Sum(group => group.Count)),
            build.Incarnates.Count,
            build.Incarnates.Sum(incarnate => incarnate.SubPowers.Sum(sub => sub.Effects.Count)),
        ];
        Assert.Equal(counts, actual);
        Assert.Equal(scaleSum, effects.Sum(effect => effect.Scale));
        if (assembly.Length > 0)
        {
            int[] actualAssembly =
            [
                build.PowerAssembly.Count,
                build.PowerAssembly.Values.Sum(power => power.Power.Effects.Sum(effect => effect.Conditionals.Count)),
                build.PowerAssembly.Values.Count(power => power.ProcInclude),
                build.AssembledPowers.Count,
                build.AssembledPowers.Values.Sum(list => list.Count),
            ];
            Assert.Equal(assembly, actualAssembly);
        }
    }

    [Fact]
    public void The_shared_dumps_load_the_values_the_python_loaders_read()
    {
        FixtureStore store = Fixtures.Store;

        Assert.Equal(61, store.Archetypes.Classes.Count);
        Assert.Equal(3, store.Archetypes.NidFromUidClass("class_scrapper"));
        Assert.Equal(-1, store.Archetypes.NidFromUidClass("Class_Nobody"));
        Assert.Equal(1827, store.AssemblyPowers.Count);
        Assert.Equal(241, store.AssemblyEntities.Entities.Count);
        Assert.Equal(81, store.AssemblyEntities.Powersets.Count);
        Assert.Equal(1345, store.EnhancementEffects.Count);
        Assert.Equal(227, store.SetBonuses.Sets.Count);
        Assert.Equal(233, store.SetBonuses.Powers.Count);
        Assert.Equal(1, store.SetBonuses.Powers.Values.Count(power => power.MyPetOnly));
        Assert.Equal(25, store.SetBonuses.Powers.Values.Sum(power => power.Effects.Count(effect => string.Equals(effect.BuffMode, "Debuff", StringComparison.Ordinal))));
        Assert.Equal(27, store.Levels.SlotGrantLevels.Count);
        Assert.Equal(67, store.Levels.TotalSlotsGranted);
        Assert.Equal([0, 0, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 34, 37, 40, 43, 46, 48], store.Levels.PowerPickLevels);
        Assert.Equal(67, store.ServerData.MaxSlots);
        Assert.Equal(50, store.Config.ForceLevel);
        Assert.Equal(0.75, store.Config.ScalingToHit);
    }

    // Printed once by load_enum_maps("tests/fixtures/mids/enums.json"): len(maps), size("eDamage"),
    // size("eEffectType"), maps["eDamage"]["Psionic"], inverse["eDamage"][8].
    [Fact]
    public void The_enum_maps_load_with_their_inverse()
    {
        EnumMaps enums = Fixtures.Store.Enums;

        Assert.Equal(19, enums.Maps.Count);
        Assert.Equal(16, enums.Size("eDamage"));
        Assert.Equal(86, enums.Size("eEffectType"));
        Assert.Equal(8, enums.Maps["eDamage"]["Psionic"]);
        Assert.Equal("Psionic", enums.Inverse["eDamage"][8]);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Dumped_totals_load_quantized(string name)
    {
        MidsTotals totals = Fixtures.Store.Build(name).Totals;

        foreach (TotalStatistics side in new[] { totals.Totals, totals.TotalsCapped })
        {
            double[] cells = [.. side.Def, .. side.Res, .. side.Mez, .. side.MezRes, .. side.DebuffRes, .. side.Elusivity, side.HpRegen, side.HpMax, side.EndRec, side.BuffHaste];
            Assert.All(cells, cell => Assert.Equal((double)(float)cell, cell));
        }
    }

    [Fact]
    public void The_attribmod_tables_load_quantized()
    {
        AttribMods mods = Fixtures.Store.AttribMods;
        int ones = mods.NidFromUidAttribMod("MELEE_ONES");

        Assert.True(ones >= 0);
        Assert.Equal(-1, mods.NidFromUidAttribMod("No_Such_Table"));
        Assert.Equal(-10.0, mods.Tables[0].Table[0][0]);
        Assert.All(mods.Tables.SelectMany(table => table.Table).SelectMany(row => row), cell => Assert.Equal((double)(float)cell, cell));
    }

    private static int ArrayLength(BuildFixture build, string file)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(build.Directory, file)));
        return document.RootElement.GetArrayLength();
    }
}
