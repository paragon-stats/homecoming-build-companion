using CohEngine.Core.Chain;
using CohEngine.Core.Names;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Names;

public sealed class PowerNameResolverTests
{
    private static TempTree Canonical(string melee = "\"Scrapper_Melee.Set_A.Hit\",\"Scrapper_Melee.Set_A.Block\"", string displays = "\"Hit\",\"Block\"")
    {
        TempTree tree = new();
        tree.Write("powers_by_at.json", "{\"scrapper\":[[0,\"melee.set_a.hit\"],[1,\"epic.fire_mastery.blast\"]],\"tanker\":[[0,\"melee.set_a.hit\"]]}");
        tree.Write("archetypes/scrapper.json", "{\"primary_category\":\"Melee\",\"secondary_category\":\"Defense\",\"power_pool_category\":\"Pool\"}");
        tree.Write("archetypes/tanker.json", "{\"primary_category\":\"Melee\",\"secondary_category\":\"Defense\",\"power_pool_category\":\"Pool\"}");
        tree.Write("powers/melee/index.json", "{\"name\":\"Scrapper_Melee\"}");
        tree.Write("powers/melee/set_a/index.json", $"{{\"name\":\"Set_A\",\"display_name\":\"Set A\",\"power_names\":[{melee}],\"power_display_names\":[{displays}]}}");
        tree.Write("powers/defense/index.json", "{\"name\":\"Scrapper_Defense\"}");
        tree.Write("powers/defense/shield/index.json", "{\"name\":\"Shield\",\"display_name\":\"Shield\",\"power_names\":[\"Scrapper_Defense.Shield.Hit\"],\"power_display_names\":[\"Hit\"]}");
        tree.Write("powers/pool/index.json", "{\"name\":\"Pool\"}");
        tree.Write("powers/pool/fighting/index.json", "{\"name\":\"Fighting\",\"display_name\":\"Fighting\",\"power_names\":[\"Pool.Fighting.Boxing\"],\"power_display_names\":[\"Boxing\"]}");
        tree.Write("powers/epic/index.json", "{\"name\":\"Epic\"}");
        tree.Write("powers/epic/fire_mastery/index.json", "{\"name\":\"Fire_Mastery\",\"display_name\":\"Fire Mastery\",\"power_names\":[\"Epic.Fire_Mastery.Blast\"],\"power_display_names\":[\"Blast\"]}");
        return tree;
    }

    private static PowerNameResolver Resolver(TempTree tree)
    {
        return PowerNameResolver.FromCanonical(tree.Root);
    }

    [Fact]
    public void Powersets_are_sorted_by_internal_name_with_the_archetypes_that_take_them()
    {
        using TempTree tree = Canonical();

        Assert.Equal(
            "Epic.Fire_Mastery:scrapper|Pool.Fighting:scrapper,tanker|Scrapper_Defense.Shield:scrapper,tanker|Scrapper_Melee.Set_A:scrapper,tanker",
            string.Join('|', Resolver(tree).Powersets.Select(set => set.Internal + ":" + string.Join(',', set.Archetypes))));
    }

    [Fact]
    public void A_powerset_keeps_its_category_display_and_powers_in_index_order()
    {
        using TempTree tree = Canonical();

        PowersetName set = Resolver(tree).Powersets.Single(item => item.Internal.EndsWith("Set_A", StringComparison.Ordinal));

        Assert.Equal("Scrapper_Melee|Set A|Hit,Block", $"{set.Category}|{set.Display}|{string.Join(',', set.Powers.Select(power => power.Display))}");
    }

    [Fact]
    public void A_power_displays_as_the_game_shows_it()
    {
        using TempTree tree = Canonical();

        Assert.Equal("Block", Resolver(tree).DisplayName("Scrapper_Melee.Set_A.Block"));
    }

    [Fact]
    public void An_unknown_internal_name_is_e22()
    {
        using TempTree tree = Canonical();

        Assert.StartsWith("E22: internal power name 'Nope' is not a player power among the 5 indexed", Assert.Throws<ChainException>(() => Resolver(tree).DisplayName("Nope")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Display_names_match_case_insensitively_within_an_optional_powerset()
    {
        using TempTree tree = Canonical();

        Assert.Equal("Scrapper_Defense.Shield.Hit,Scrapper_Melee.Set_A.Hit|Scrapper_Melee.Set_A.Hit", string.Join(',', Resolver(tree).InternalNames("hit").Order(StringComparer.Ordinal)) + "|" + string.Join(',', Resolver(tree).InternalNames("HIT", "Scrapper_Melee.Set_A")));
    }

    [Fact]
    public void A_display_name_resolves_to_one_internal_name_in_its_powerset()
    {
        using TempTree tree = Canonical();

        Assert.Equal("Scrapper_Melee.Set_A.Block", Resolver(tree).Resolve("Block", "Scrapper_Melee.Set_A"));
    }

    [Fact]
    public void An_ambiguous_display_name_is_e22()
    {
        using TempTree tree = Canonical("\"Scrapper_Melee.Set_A.Hit\",\"Scrapper_Melee.Set_A.Hit_2\"", "\"Hit\",\"Hit\"");

        Assert.StartsWith("E22: display name 'Hit' matches 2 powers in powerset 'Scrapper_Melee.Set_A' ['Scrapper_Melee.Set_A.Hit', 'Scrapper_Melee.Set_A.Hit_2'], expected 1", Assert.Throws<ChainException>(() => Resolver(tree).Resolve("Hit", "Scrapper_Melee.Set_A")).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("display", "Hit")]
    [InlineData("internal", "Scrapper_Melee.Set_A.Hit")]
    [InlineData("both", "Hit (Scrapper_Melee.Set_A.Hit)")]
    public void A_power_renders_in_the_requested_form(string form, string expected)
    {
        using TempTree tree = Canonical();

        Assert.Equal(expected, Resolver(tree).Render("Scrapper_Melee.Set_A.Hit", form));
    }

    [Fact]
    public void The_default_form_is_the_display_name()
    {
        using TempTree tree = Canonical();

        Assert.Equal("Hit", Resolver(tree).Render("Scrapper_Melee.Set_A.Hit"));
    }

    [Fact]
    public void Name_and_display_lists_of_different_lengths_are_e22()
    {
        using TempTree tree = Canonical(displays: "\"Hit\"");

        Assert.StartsWith("E22: powerset Scrapper_Melee.Set_A lists 2 power names but 1 display names", Assert.Throws<ChainException>(() => Resolver(tree)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_repository_root_search_fails_when_no_start_is_inside_the_repository()
    {
        using TempTree tree = new();

        Assert.Throws<FileNotFoundException>(() => ChainPaths.RepoRoot([tree.Root]));
    }

    [Fact]
    public void The_repository_root_search_tries_each_start_in_order()
    {
        using TempTree tree = new();

        Assert.Equal(Fixtures.Store.RepositoryRoot, ChainPaths.RepoRoot([tree.Root, AppContext.BaseDirectory]));
    }
}
