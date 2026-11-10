using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

public sealed class CanonicalJsonTests
{
    private const string Smite = "{\"full_name\":\"Set.Sub.Smite\"}";

    public static TheoryData<string, string> Reprs => new()
    {
        { "Plain", "'Plain'" },
        { "it's", "\"it's\"" },
        { "a\"b'c", "'a\"b\\'c'" },
        { "tab\tnew\nret\rslash\\", "'tab\\tnew\\nret\\rslash\\\\'" },
    };

    [Theory]
    [MemberData(nameof(Reprs))]
    public void Repr_quotes_text_like_python(string text, string expected)
    {
        Assert.Equal(expected, PyText.Repr(text));
    }

    [Fact]
    public void Repr_of_a_list_and_an_int()
    {
        Assert.Equal("['a', 'b'] 7", PyText.List(["a", "b"]) + " " + PyText.Repr(7));
    }

    [Fact]
    public void The_exception_types_keep_the_standard_constructors()
    {
        Assert.Equal(
            ["x", "inner", "'k'", "y"],
            [
                new EngineException("x").Message,
                new EngineException("inner", new MissingKeyException()).Message,
                new MissingKeyException("k").Message,
                new MissingKeyException("y", new EngineException()).Message,
            ]);
    }

    [Fact]
    public void Req_of_an_absent_key_is_a_missing_key()
    {
        Assert.Equal("'gone'", Assert.Throws<MissingKeyException>(() => CanonicalPowers.Parse("{}").Req("gone")).Message);
    }

    [Fact]
    public void Opt_treats_null_and_absent_alike()
    {
        JsonElement record = CanonicalPowers.Parse("{\"a\":null,\"b\":2}");

        Assert.Equal([true, true, false], [record.Opt("a") is null, record.Opt("c") is null, record.Opt("b") is null]);
    }

    [Fact]
    public void Defaults_apply_to_absent_members()
    {
        JsonElement record = CanonicalPowers.Parse("{\"n\":2.5,\"s\":\"x\",\"l\":[\"p\"]}");

        Assert.Equal(["x:2.5", "d:9", "p"], [record.Str("s") + ":" + record.Num("n", 0.0), record.Str("t", "d") + ":" + record.Num("m", 9.0), string.Join(",", record.Strings("l")) + string.Join(",", record.Strings("none"))]);
    }

    [Fact]
    public void Required_readers_return_the_value()
    {
        JsonElement record = CanonicalPowers.Parse("{\"n\":2.5,\"s\":\"x\",\"l\":[1,2]}");

        Assert.Equal("2.5 x 2", record.ReqNum("n") + " " + record.ReqStr("s") + " " + record.ReqItems("l").Count);
    }

    [Fact]
    public void ReadJson_tolerates_a_byte_order_mark()
    {
        using TempTree tree = new();
        string path = tree.Write("a.json", "﻿{\"k\":1}");

        Assert.Equal(1, CanonicalJson.ReadJson(path, ErrorCodes.E38).ReqNum("k"));
    }

    [Fact]
    public void ReadJson_names_the_code_and_path_of_a_corrupt_file()
    {
        using TempTree tree = new();
        string path = tree.Write("bad.json", "{oops");

        Assert.StartsWith($"{ErrorCodes.E38}: {path} is not valid JSON (", Assert.Throws<EngineException>(() => CanonicalJson.ReadJson(path, ErrorCodes.E38)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_power_path_lower_cases_segments_and_replaces_colons()
    {
        string expected = Path.Join("root", "set", "sub_x", "power.json");

        Assert.Equal(expected, CanonicalJson.CanonicalPowerPath("root", "Set.Sub:X.Power"));
    }

    [Fact]
    public void Powers_load_from_the_derived_path()
    {
        using TempTree tree = new();
        tree.Write("powers/set/sub/smite.json", Smite);

        Assert.Equal("Set.Sub.Smite", Assert.Single(CanonicalJson.LoadCanonicalPowers(tree.FullPath("powers"), ["Set.Sub.Smite"]).Keys));
    }

    [Fact]
    public void An_empty_placeholder_falls_back_to_a_file_name_search()
    {
        using TempTree tree = new();
        tree.Write("powers/set/sub/smite.json", string.Empty);
        tree.Write("powers/other/smite.json", Smite);

        Assert.Equal("Set.Sub.Smite", CanonicalJson.LoadCanonicalPowers(tree.FullPath("powers"), ["Set.Sub.Smite"])["Set.Sub.Smite"].ReqStr("full_name"));
    }

    [Fact]
    public void A_corrupt_same_named_file_is_skipped_when_another_matches()
    {
        using TempTree tree = new();
        tree.Write("powers/a/smite.json", "{oops");
        tree.Write("powers/b/smite.json", Smite);

        Assert.Single(CanonicalJson.LoadCanonicalPowers(tree.FullPath("powers"), ["Set.Sub.Smite"]));
    }

    [Fact]
    public void A_corrupt_only_candidate_raises_e38()
    {
        using TempTree tree = new();
        tree.Write("powers/set/sub/smite.json", "{oops");

        Assert.StartsWith(ErrorCodes.E38, Assert.Throws<EngineException>(() => CanonicalJson.LoadCanonicalPowers(tree.FullPath("powers"), ["Set.Sub.Smite"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_powers_are_named_together_in_e35()
    {
        using TempTree tree = new();
        tree.Write("powers/set/sub/smite.json", "{\"full_name\":\"Else.Where.Smite\"}");
        tree.Write("powers/keep.txt", "x");

        EngineException error = Assert.Throws<EngineException>(() => CanonicalJson.LoadCanonicalPowers(tree.FullPath("powers"), ["Set.Sub.Smite", "Set.Sub.Punch"]));

        Assert.EndsWith("for ['Set.Sub.Smite', 'Set.Sub.Punch']; check the full_name spelling or restore the file (0-byte files are skipped)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_resolver_gives_null_for_a_missing_or_empty_or_corrupt_file()
    {
        using TempTree tree = new();
        tree.Write("powers/set/sub/empty.json", string.Empty);
        tree.Write("powers/set/sub/bad.json", "{oops");
        PowerResolver resolve = CanonicalJson.CanonicalResolver(tree.FullPath("powers"));

        Assert.Equal([true, true, true], [resolve("Set.Sub.Gone") is null, resolve("Set.Sub.Empty") is null, resolve("Set.Sub.Bad") is null]);
    }

    [Fact]
    public void The_resolver_reads_powers_and_entities_and_caches()
    {
        using TempTree tree = new();
        tree.Write("powers/set/sub/smite.json", Smite);
        tree.Write("entities/thing.json", "{\"name\":\"Thing\"}");
        tree.Write("entities/empty.json", string.Empty);
        PowerResolver resolve = CanonicalJson.CanonicalResolver(tree.FullPath("powers"));

        Assert.Equal(["Set.Sub.Smite", "Thing", "Set.Sub.Smite", string.Empty], [resolve("Set.Sub.Smite")!.Value.ReqStr("full_name"), resolve("Thing")!.Value.ReqStr("name"), resolve("Set.Sub.Smite")!.Value.ReqStr("full_name"), resolve("Empty")?.ToString() ?? string.Empty]);
    }
}
