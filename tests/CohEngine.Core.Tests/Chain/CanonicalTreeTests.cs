using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Names;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

/// <summary>Guards on the committed <c>data/canonical</c> tree: no empty placeholders, every colon power resolvable, every supplemented set whole.</summary>
public sealed class CanonicalTreeTests
{
    private static readonly string Canonical = Path.Join(FixtureStore.Locate(AppContext.BaseDirectory).RepositoryRoot, "data", "canonical");

    public static TheoryData<string> SupplementDirectories()
    {
        TheoryData<string> directories = [];
        foreach (string directory in Read("manifest.json").Items("supplements").SelectMany(supplement => supplement.Strings("directories")))
        {
            directories.Add(directory);
        }

        return directories;
    }

    [Fact]
    public void No_canonical_file_is_zero_bytes()
    {
        string[] empty = [.. Directory.EnumerateFiles(Canonical, "*", SearchOption.AllDirectories).Where(file => new FileInfo(file).Length == 0).Select(file => Path.GetRelativePath(Canonical, file))];

        Assert.Empty(empty);
    }

    [Fact]
    public void Every_colon_power_resolves()
    {
        string powers = Path.Join(Canonical, "powers");
        string[] lowered = [.. Read("all_power_search.json").EnumerateArray().Select(row => row[1].GetString()!).Where(name => name.Contains(':', StringComparison.Ordinal))];
        Assert.NotEmpty(lowered);

        string[] names = [.. lowered.Select(name => ReadFile(CanonicalJson.CanonicalPowerPath(powers, name)).Str("full_name"))];

        Assert.Equal(lowered, names.Select(name => name.ToLowerInvariant()), StringComparer.Ordinal);
        Assert.Equal(names.Order(StringComparer.Ordinal), CanonicalJson.LoadCanonicalPowers(powers, names).Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SupplementDirectories))]
    public void A_supplemented_set_loads_and_its_links_resolve(string directory)
    {
        string[] names = [.. ReadFile(Path.Join(Canonical, directory, "index.json")).Strings("power_names")];
        IReadOnlyDictionary<string, JsonElement> powers = CanonicalJson.LoadCanonicalPowers(Path.Join(Canonical, "powers"), names);

        Assert.Empty(powers.Where(pair => pair.Value.Req("allowed_boostset_cats").ValueKind != JsonValueKind.Array).Select(pair => pair.Key));
        string category = directory.Split('/')[1];
        if (category is not ("redirects" or "pets"))
        {
            string internalName = names[0][..names[0].LastIndexOf('.')];
            PowersetName set = Assert.Single(PowerNameResolver.FromCanonical(Canonical).Powersets, candidate => string.Equals(candidate.Internal, internalName, StringComparison.Ordinal));
            Assert.Equal(names, set.Powers.Select(power => power.Internal), StringComparer.Ordinal);
        }

        PowerResolver resolve = CanonicalJson.CanonicalResolver(Path.Join(Canonical, "powers"));
        IEnumerable<string> links = powers.Values
            .SelectMany(power => power.Items("effects"))
            .SelectMany(effect => effect.Items("templates"))
            .Select(template => template.Opt("params"))
            .Where(parameters => parameters is not null)
            .SelectMany(parameters => parameters!.Value.Strings("power_names").Concat(parameters.Value.Strings("redirects")))
            .Distinct(StringComparer.Ordinal);
        Assert.Empty(links.Where(link => resolve(link) is null).Order(StringComparer.Ordinal));
    }

    private static JsonElement Read(string file)
    {
        return ReadFile(Path.Join(Canonical, file));
    }

    private static JsonElement ReadFile(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.Clone();
    }
}
