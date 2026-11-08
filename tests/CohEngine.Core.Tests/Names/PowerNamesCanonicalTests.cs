using CohEngine.Core.Chain;
using CohEngine.Core.Names;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Names;

// Ported from tests/test_power_names.py, on the committed data/canonical tree.
public sealed class PowerNamesCanonicalTests
{
    private static readonly Lazy<PowerNameResolver> Resolver = new(() => PowerNameResolver.FromCanonical(Path.Combine(Fixtures.Store.RepositoryRoot, "data", "canonical")));

    public static TheoryData<string, string> Divergent => new()
    {
        { "Epic.VEAT_Mace_Mastery.Focused_Accuracy", "Shatter Armor" },
        { "Pool.Leaping.Long_Jump", "Super Jump" },
        { "Pool.Leaping.Leap", "Acrobatics" },
        { "Pool.Flight.Combat_Flight", "Hover" },
        { "Pool.Flight.Afterburner", "Evasive Maneuvers" },
        { "Pool.Flight.Fly_Boost", "Afterburner" },
        { "Pool.Leadership.Defense", "Maneuvers" },
        { "Pool.Teleportation.Long_Range_Teleport", "Fold Space" },
        { "Pool.Teleportation.Teleport_Foe", "Combat Teleport" },
        { "Pool.Teleportation.Recall_Friend", "Teleport Target" },
        { "Training_Gadgets.Bane_Spider_Training.Hide", "Cloaking Device" },
        { "Training_Gadgets.Bane_Spider_Training.Bane_Spider_Armor", "Bane Spider Armor Upgrade" },
        { "Controller_Control.Illusion_Control.Decoy", "Phantom Army" },
        { "Dominator_Control.Illusion_Control.Decoy", "Phantom Army" },
    };

    public static TheoryData<string> Leaves => ["Mace_Beam", "Pulverize", "Shatter"];

    [Theory]
    [MemberData(nameof(Divergent))]
    public void An_internal_name_resolves_to_its_display_name(string internalName, string display)
    {
        Assert.Equal(display, Resolver.Value.DisplayName(internalName));
    }

    [Theory]
    [MemberData(nameof(Divergent))]
    public void A_display_name_resolves_back_to_the_internal_name(string internalName, string display)
    {
        string powerset = internalName[..internalName.LastIndexOf('.')];

        Assert.Equal($"{internalName} {internalName} True", $"{Resolver.Value.Resolve(display, powerset)} {Resolver.Value.Resolve(display.ToUpperInvariant(), powerset)} {Resolver.Value.InternalNames(display).Contains(internalName, StringComparer.Ordinal)}");
    }

    [Fact]
    public void Display_names_repeat_across_the_archetype_versions_of_a_set()
    {
        Assert.Equal(
            "Controller_Control.Illusion_Control.Decoy,Dominator_Control.Illusion_Control.Decoy|Dominator_Control.Illusion_Control.Decoy",
            string.Join(',', Resolver.Value.InternalNames("phantom army")) + "|" + string.Join(',', Resolver.Value.InternalNames("Phantom Army", "Dominator_Control.Illusion_Control")));
    }

    [Fact]
    public void An_unknown_display_has_no_internal_names()
    {
        Assert.Equal("0 0", $"{Resolver.Value.InternalNames("Not A Power").Count} {Resolver.Value.InternalNames("Fold Space", "Pool.Leaping").Count}");
    }

    [Theory]
    [MemberData(nameof(Leaves))]
    public void Player_powers_win_over_npc_and_proxy_copies(string leaf)
    {
        string[] leaves = [.. Resolver.Value.Powersets.SelectMany(set => set.Powers).Select(power => power.Internal).Where(name => string.Equals(name[(name.LastIndexOf('.') + 1)..], leaf, StringComparison.OrdinalIgnoreCase))];

        Assert.True(leaves.Length > 0);
        Assert.DoesNotContain(leaves, name => name.StartsWith("V_Arachnos_Proxy.", StringComparison.Ordinal) || name.StartsWith("Warriors_New.", StringComparison.Ordinal) || name.StartsWith("Widow_Training_", StringComparison.Ordinal));
    }

    [Fact]
    public void Npc_sets_are_not_player_powersets()
    {
        string[] names = [.. Resolver.Value.Powersets.Select(set => set.Internal)];

        Assert.DoesNotContain(names, name => name.StartsWith("V_Arachnos_Proxy.", StringComparison.Ordinal) || name.StartsWith("Warriors_New.", StringComparison.Ordinal) || name.StartsWith("Warriors.", StringComparison.Ordinal));
        Assert.True(names.Contains("Pool.Teleportation", StringComparer.Ordinal) && names.Contains("Scrapper_Melee.Claws", StringComparer.Ordinal) && names.Contains("Epic.VEAT_Mace_Mastery", StringComparer.Ordinal));
        Assert.StartsWith("E22", Assert.Throws<ChainException>(() => Resolver.Value.DisplayName("Warriors_New.Warrior_Mace_Lt.Mace_Beam")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Powersets_are_sorted_and_carry_their_archetypes()
    {
        IReadOnlyList<PowersetName> sets = Resolver.Value.Powersets;
        PowersetName pool = sets.Single(set => string.Equals(set.Internal, "Pool.Teleportation", StringComparison.Ordinal));

        Assert.Equal(string.Join(',', sets.Select(set => set.Internal).Order(StringComparer.Ordinal)), string.Join(',', sets.Select(set => set.Internal)));
        Assert.Equal("Teleportation Pool True", $"{pool.Display} {pool.Category} {pool.Archetypes.Contains("scrapper", StringComparer.Ordinal)}");
        Assert.Equal(string.Join(',', pool.Archetypes.Order(StringComparer.Ordinal)), string.Join(',', pool.Archetypes));
        Assert.All(pool.Powers, power => Assert.Equal(pool.Internal, power.Powerset));
        Assert.Equal("scrapper", string.Join(',', sets.Single(set => string.Equals(set.Internal, "Scrapper_Melee.Claws", StringComparison.Ordinal)).Archetypes));
        Assert.Equal("arachnos_soldier,arachnos_widow", string.Join(',', sets.Single(set => string.Equals(set.Internal, "Epic.VEAT_Mace_Mastery", StringComparison.Ordinal)).Archetypes));
    }

    [Fact]
    public void Resolve_needs_exactly_one_match()
    {
        Assert.Contains("matches 0", Assert.Throws<ChainException>(() => Resolver.Value.Resolve("Fold Space", "Pool.Leaping")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_refuses_a_display_name_that_repeats_within_a_set()
    {
        using TempTree tree = new();
        tree.Write("archetypes/blaster.json", """{"primary_category":"Pool","secondary_category":"Pool","power_pool_category":"Pool"}""");
        tree.Write("powers_by_at.json", """{"blaster":[]}""");
        tree.Write("powers/pool/index.json", """{"name":"Pool"}""");
        tree.Write("powers/pool/twins/index.json", """{"name":"Twins","display_name":"Twins","power_names":["Pool.Twins.A","Pool.Twins.B"],"power_display_names":["Same","Same"]}""");

        PowerNameResolver twins = PowerNameResolver.FromCanonical(tree.Root);

        Assert.Equal("Pool.Twins.A,Pool.Twins.B", string.Join(',', twins.InternalNames("Same")));
        Assert.Contains("matches 2", Assert.Throws<ChainException>(() => twins.Resolve("Same", "Pool.Twins")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_display_name_of_an_unknown_internal_name_fails_loud()
    {
        Assert.StartsWith("E22: internal power name 'Pool.Teleportation.Nope'", Assert.Throws<ChainException>(() => Resolver.Value.DisplayName("Pool.Teleportation.Nope")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_forms()
    {
        const string internalName = "Pool.Teleportation.Long_Range_Teleport";
        PowerNameResolver resolver = Resolver.Value;

        Assert.Equal($"Fold Space|Fold Space|{internalName}|Fold Space ({internalName})", string.Join('|', resolver.Render(internalName), resolver.Render(internalName, "display"), resolver.Render(internalName, "internal"), resolver.Render(internalName, "both")));
    }

    [Fact]
    public void Render_of_an_unknown_internal_name_fails_loud()
    {
        Assert.StartsWith("E22", Assert.Throws<ChainException>(() => Resolver.Value.Render("Pool.Nope.Nope", "both")).Message, StringComparison.Ordinal);
    }
}
