using System.Text.Json;

using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;

namespace CohEngine.Core.Tests.Profiles;

public sealed class ProfileTests
{
    private static readonly string Strategy = Path.Join(Fixtures.Store.RepositoryRoot, "strategy");

    private static readonly JsonElement Breakpoints = ProfileResolver.LoadBreakpoints(Path.Join(Strategy, "breakpoints.json"));

    private static readonly JsonElement RawProfiles = ProfileResolver.LoadBuildProfiles(Path.Join(Strategy, "build_profiles.json"));

    private static BuildProfile Resolve(string name, string atKey = "Scrapper")
    {
        return ProfileResolver.Resolve(name, RawProfiles, Breakpoints, atKey);
    }

    private static EngineException BadRef(string refPath, string breakpointsJson = "")
    {
        using JsonDocument profiles = JsonDocument.Parse("{\"bad\":{\"display_name\":\"Bad\",\"priority\":[],\"targets\":[{\"metric\":\"x\",\"op\":\">=\",\"ref\":\"" + refPath + "\"}]}}");
        using JsonDocument custom = JsonDocument.Parse(breakpointsJson.Length == 0 ? "{}" : breakpointsJson);
        JsonElement points = breakpointsJson.Length == 0 ? Breakpoints : custom.RootElement;
        return Assert.Throws<EngineException>(() => ProfileResolver.Resolve("bad", profiles.RootElement, points, "Scrapper"));
    }

    [Fact]
    public void The_file_carries_exactly_the_nine_canonical_profiles()
    {
        Assert.Equal(9, ProfileResolver.CanonicalProfiles.Count);
        Assert.Equal(ProfileResolver.CanonicalProfiles.Order(StringComparer.Ordinal).ToArray(), ProfileResolver.ProfileNames(RawProfiles).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void There_is_no_hard_mode_profile()
    {
        string names = string.Join(' ', ProfileResolver.ProfileNames(RawProfiles)).ToUpperInvariant();

        Assert.DoesNotContain("HARD", names, StringComparison.Ordinal);
        Assert.DoesNotContain("STAR", names, StringComparison.Ordinal);
        Assert.DoesNotContain("ADVANCED", names, StringComparison.Ordinal);
    }

    [Fact]
    public void Soft_cap_resolves_the_universal_breakpoint()
    {
        BuildProfile profile = Resolve("soft-cap-defense");

        Assert.Equal("Soft-cap defense", profile.DisplayName);
        Assert.Equal(new Target("defense_positional_min", ">=", MetricValue.Of(45.0)), Assert.Single(profile.Targets));
    }

    [Theory]
    [InlineData("Scrapper", 75.0)]
    [InlineData("Tanker", 90.0)]
    public void Resist_cap_resolves_the_archetype_breakpoint(string archetype, double expected)
    {
        Assert.Equal(expected, Resolve("resist-cap", archetype).Targets[0].Value.Number);
    }

    [Fact]
    public void A_display_name_with_a_space_resolves_to_the_underscore_key()
    {
        double expected = Breakpoints.GetProperty("archetypes").GetProperty("Arachnos_Soldier").GetProperty("res_cap_pct").GetDouble();

        Assert.Equal(expected, Resolve("resist-cap", "Arachnos Soldier").Targets[0].Value.Number);
    }

    [Fact]
    public void A_boolean_target_keeps_its_literal()
    {
        Assert.Equal(new Target("perma_hasten", "==", MetricValue.Flag(true)), Assert.Single(Resolve("perma-hasten", "Dominator").Targets));
    }

    [Fact]
    public void The_theme_profile_has_no_targets()
    {
        Assert.Empty(Resolve("theme-rp").Targets);
    }

    [Fact]
    public void Select_returns_the_named_profile_with_its_priority()
    {
        BuildProfile profile = ProfileResolver.Select("perma-hasten", RawProfiles, Breakpoints, "Scrapper");

        Assert.Equal("perma-hasten", profile.Name);
        Assert.NotEmpty(profile.Priority);
    }

    [Fact]
    public void An_unknown_profile_is_p_goal_001()
    {
        EngineException exception = Assert.Throws<EngineException>(() => ProfileResolver.Select("hard-mode", RawProfiles, Breakpoints, "Scrapper"));

        Assert.StartsWith("P-GOAL-001: unknown build profile 'hard-mode'; expected one of ['soft-cap-defense', ", exception.Message, StringComparison.Ordinal);
        Assert.EndsWith("(hard-mode / 4-star is a documented gap, not a profile)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_breakpoint_path_is_e16()
    {
        Assert.Equal(
            "E16: profile target references breakpoint path 'universal.does_not_exist', but segment 'does_not_exist' is not in breakpoints.json; fix the ref or add the breakpoint",
            BadRef("universal.does_not_exist").Message);
    }

    [Fact]
    public void A_path_through_a_number_is_e16()
    {
        Assert.Contains("segment 'deeper' is not in breakpoints.json", BadRef("universal.max_levels.deeper").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ref_landing_on_a_sub_object_is_e16()
    {
        Assert.Contains("resolved to a non-numeric value {", BadRef("universal").Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"universal\":{\"leaf\":true}}", "True")]
    [InlineData("{\"universal\":{\"leaf\":\"text\"}}", "'text'")]
    [InlineData("{\"universal\":{\"leaf\":null}}", "None")]
    [InlineData("{\"universal\":{\"leaf\":[1,2.5,false]}}", "[1, 2.5, False]")]
    public void A_non_numeric_leaf_is_quoted_as_python_would(string breakpoints, string quoted)
    {
        Assert.EndsWith($"resolved to a non-numeric value {quoted}; point the ref at a numeric leaf in breakpoints.json (not a sub-object)", BadRef("universal.leaf", breakpoints).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sub_object_is_quoted_with_python_number_text()
    {
        Assert.Contains("{'a': 1, 'b': 2.0, 'c': 1e-05}", BadRef("universal", "{\"universal\":{\"a\":1,\"b\":2.0,\"c\":1e-5}}").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ref_with_an_archetype_placeholder_needs_the_archetype_table()
    {
        Assert.StartsWith("E19: archetype 'Scrapper' matches 0 of the 0 known keys []", BadRef("archetypes.{at}.res_cap_pct", "{\"universal\":{}}").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_archetype_table_that_is_not_an_object_counts_as_empty()
    {
        Assert.StartsWith("E19:", BadRef("archetypes.{at}.res_cap_pct", "{\"archetypes\":[]}").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => ProfileResolver.Resolve(null!, RawProfiles, Breakpoints, "Scrapper"));
        Assert.Throws<ArgumentNullException>(() => ProfileResolver.Resolve("theme-rp", RawProfiles, Breakpoints, null!));
    }
}
