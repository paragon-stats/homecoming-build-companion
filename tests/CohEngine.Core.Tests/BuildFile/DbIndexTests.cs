using CohEngine.Core.BuildFile;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Port of <c>test_dbindex.py</c>: the StaticIndex maps the .mxd reader resolves through.</summary>
public sealed class DbIndexTests : IDisposable
{
    private static readonly string[] KnownTypes = ["None", "Normal", "InventO", "SpecialO", "SetO"];

    private readonly string directory = Directory.CreateTempSubdirectory("coh-engine-").FullName;

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void Power_index_resolves_a_static_index_to_a_full_name()
    {
        IReadOnlyDictionary<int, string> index = DbIndex.LoadPowerIndex(Path.Join(Fixtures.Store.MidsDirectory, "power_static_index.json"));

        Assert.NotEmpty(index);
        Assert.Equal("Arachnos_Soldiers.Arachnos_Soldier.Single_Shot", index[0]);
        Assert.DoesNotContain(-999999, index.Keys);
    }

    [Fact]
    public void Enhancement_index_resolves_a_static_index_to_uid_and_type()
    {
        IReadOnlyDictionary<int, EnhIndexEntry> index = DbIndex.LoadEnhancementIndex(Path.Join(Fixtures.Store.MidsDirectory, "enhancements.json"));

        Assert.Equal(new EnhIndexEntry("Magic_Accuracy", "Normal"), index[0]);
        Assert.All(index.Values, entry => Assert.Contains(entry.TypeId, KnownTypes, StringComparer.Ordinal));
    }

    [Fact]
    public void A_negative_static_index_is_dropped_and_a_repeat_keeps_the_last_record()
    {
        string powers = Path.Join(directory, "powers.json");
        File.WriteAllText(powers, """[{"StaticIndex":-1,"FullName":"A"},{"StaticIndex":2,"FullName":"B"},{"StaticIndex":2,"FullName":"C"}]""");
        string enhancements = Path.Join(directory, "enhancements.json");
        File.WriteAllText(enhancements, """[{"StaticIndex":-1,"UID":"A","TypeName":"None"},{"StaticIndex":3,"UID":"B","TypeName":"SetO"}]""");

        Assert.Equal([KeyValuePair.Create(2, "C")], DbIndex.LoadPowerIndex(powers));
        Assert.Equal([KeyValuePair.Create(3, new EnhIndexEntry("B", "SetO"))], DbIndex.LoadEnhancementIndex(enhancements));
    }

    [Fact]
    public void A_missing_file_raises()
    {
        Assert.Throws<FileNotFoundException>(() => DbIndex.LoadPowerIndex(Path.Join(directory, "nope.json")));
        Assert.Throws<FileNotFoundException>(() => DbIndex.LoadEnhancementIndex(Path.Join(directory, "nope.json")));
    }
}
