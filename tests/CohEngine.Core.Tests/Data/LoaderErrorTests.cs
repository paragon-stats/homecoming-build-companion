using System.Text.Json;
using System.Text.Json.Nodes;

using CohEngine.Core.Data;

namespace CohEngine.Core.Tests.Data;

public sealed class LoaderErrorTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("coh-engine-").FullName;

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void A_null_document_is_refused()
    {
        string path = Write("totals.json", "null");

        Assert.Throws<InvalidDataException>(() => TotalsData.LoadTotals(path));
    }

    [Fact]
    public void Locate_fails_outside_the_repository()
    {
        Assert.Throws<DirectoryNotFoundException>(() => FixtureStore.Locate(directory));
    }

    [Fact]
    public void A_build_power_with_no_power_assembly_record_is_E86()
    {
        IReadOnlyList<Power> powers = Fixtures.Store.Build("Stalker (Fiery Melee - Invulnerability)").Powers;
        string assembly = Write("power_assembly.json", "[]");
        string slots = Write("slots.json", "[]");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AssemblyData.LoadBuildPowerAssembly(assembly, slots, powers));

        Assert.StartsWith("E86: power_assembly.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_build_power_with_no_slots_record_is_E86()
    {
        BuildFixture build = Fixtures.Store.Build("Stalker (Fiery Melee - Invulnerability)");
        string slots = Write("slots.json", "[]");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => AssemblyData.LoadBuildPowerAssembly(Path.Combine(build.Directory, "power_assembly.json"), slots, build.Powers));

        Assert.StartsWith("E86: slots.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_slot_entry_without_ProcInclude_is_E85()
    {
        BuildFixture build = Fixtures.Store.Build("shield_scrapper_noslots");
        string assembly = Write("power_assembly.json", "[]");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => AssemblyData.LoadBuildPowerAssembly(assembly, Path.Combine(build.Directory, "slots.json"), build.Powers));

        Assert.StartsWith("E85: slots.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_effect_without_extended_keys_is_E85()
    {
        BuildFixture build = Fixtures.Store.Build("Stalker (Fiery Melee - Invulnerability)");
        Power power = build.Powers.First(candidate => candidate.Effects.Count > 0);
        string record = $$"""
            [{"BuildIndex": {{power.BuildIndex}}, "PowerSetType": "Primary", "EntitiesAffected": [], "EntitiesAutoHit": [],
                "BaseRechargeTime": 0, "AoEModifier": 1, "HasGrantPowerEffect": false, "HasPowerOverrideEffect": false,
                "AbsorbSummonEffects": false, "AbsorbSummonAttributes": false, "IgnoreEnh": [], "SubPowers": [], "Effects": []}]
            """;
        string assembly = Write("power_assembly.json", record);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => AssemblyData.LoadBuildPowerAssembly(assembly, Path.Combine(build.Directory, "slots.json"), [power]));

        Assert.StartsWith("E85: power_assembly.json build power", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Summon")]
    [InlineData("EffectType")]
    public void An_assembly_power_effect_missing_a_key_is_E85(string key)
    {
        string path = Write("assembly_powers.json", DropEffectKey(Path.Combine(Fixtures.Store.MidsDirectory, "assembly_powers.json"), key));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AssemblyData.LoadAssemblyPowers(path));

        Assert.StartsWith("E85: assembly_powers.json is missing a key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_assembled_power_effect_missing_a_key_is_E85()
    {
        BuildFixture build = Fixtures.Store.Build("Stalker (Fiery Melee - Invulnerability)");
        string path = Write("assembled_powers.json", DropEffectKey(Path.Combine(build.Directory, "assembled_powers.json"), "Absorbed_Power_nID"));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AssemblyData.LoadAssembledPowers(path));

        Assert.StartsWith("E85: assembled_powers.json is missing a key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Requirement_groups_drop_blank_names_and_empty_groups()
    {
        Power stalker = Fixtures.Store.Build("Stalker (Fiery Melee - Invulnerability)").Powers[0];
        string json = JsonSerializer.Serialize<IReadOnlyList<Power>>(
            [stalker with { RequiresPowers = [["Pool.A", string.Empty], [string.Empty, string.Empty]], RequiresPowersNot = [] }],
            EffectJsonContext.Default.IReadOnlyListPower);
        string path = Write("powers_effects.json", json);

        Power loaded = Assert.Single(EffectData.LoadPowersEffects(path));

        Assert.Equal(["Pool.A"], Assert.Single(loaded.RequiresPowers));
        Assert.Empty(loaded.RequiresPowersNot);
    }

    [Fact]
    public void LoadBuildSlots_keys_slots_by_build_index()
    {
        BuildFixture build = Fixtures.Store.Build("shield_scrapper_slotted");

        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots = EnhancementData.LoadBuildSlots(Path.Combine(build.Directory, "slots.json"));

        Assert.Equal(build.Slots.Count, slots.Count);
        Assert.Equal(build.Slots[0].Slots, slots[build.Slots[0].BuildIndex]);
    }

    [Fact]
    public void Sub_powers_load_from_a_power_assembly_record()
    {
        // No committed dump has a build entry with sub-powers yet, so the shape is checked on a written record.
        string path = Write(
            "power_assembly.json",
            """[{"BuildIndex": 0, "PowerSetType": "Pool", "EntitiesAffected": ["Caster"], "EntitiesAutoHit": [], "BaseRechargeTime": 1, "AoEModifier": 1, "HasGrantPowerEffect": true, "HasPowerOverrideEffect": false, "AbsorbSummonEffects": false, "AbsorbSummonAttributes": false, "IgnoreEnh": [], "SubPowers": [{"NIDPower": 5, "StatInclude": true}], "Effects": []}]""");

        PowerAssemblyRecord record = Assert.Single(AssemblyData.LoadPowerAssemblyRecords(path));

        Assert.Equal(new SubPower(5, StatInclude: true), Assert.Single(record.SubPowers));
    }

    // The dump's first power that has an effect, with one key dropped from that effect: a stale dump in miniature.
    private static string DropEffectKey(string dumpPath, string key)
    {
        JsonArray powers = JsonNode.Parse(File.ReadAllText(dumpPath))!.AsArray();
        JsonNode power = powers.First(candidate => candidate!["Effects"]!.AsArray().Count > 0)!.DeepClone();
        Assert.True(power["Effects"]![0]!.AsObject().Remove(key), key);
        return new JsonArray(power).ToJsonString();
    }

    private string Write(string name, string text)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, text);
        return path;
    }
}
