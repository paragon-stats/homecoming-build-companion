using System.Collections.Concurrent;

using CohEngine.Core.Data;
using CohEngine.Core.Maths;

namespace CohEngine.TestSupport;

/// <summary>
/// The Mids reference dumps under <c>tests/fixtures</c>, loaded lazily and shared: each file is read once,
/// on first use. Build dumps live in <c>tests/fixtures/mids/builds/&lt;name&gt;/</c>.
/// </summary>
public sealed class FixtureStore
{
    private const string SolutionFile = "CohEngine.slnx";

    private readonly ConcurrentDictionary<string, BuildFixture> builds = new(StringComparer.Ordinal);
    private readonly Lazy<ArchetypeDb> archetypes;
    private readonly Lazy<AttribMods> attribMods;
    private readonly Lazy<EnumMaps> enums;
    private readonly Lazy<IReadOnlyDictionary<int, AssemblyPower>> assemblyPowers;
    private readonly Lazy<AssemblyEntities> assemblyEntities;
    private readonly Lazy<IReadOnlyDictionary<int, EnhancementRecord>> enhancementEffects;
    private readonly Lazy<SetBonusDb> setBonuses;
    private readonly Lazy<LevelSchedule> levels;
    private readonly Lazy<ServerData> serverData;
    private readonly Lazy<EngineConfig> config;
    private readonly Lazy<MathTables> maths;

    /// <summary>Initializes a new instance of the <see cref="FixtureStore"/> class.</summary>
    /// <param name="repositoryRoot">The directory that holds <c>CohEngine.slnx</c>.</param>
    public FixtureStore(string repositoryRoot)
    {
        RepositoryRoot = repositoryRoot;
        Root = Path.Join(repositoryRoot, "tests", "fixtures");
        MidsDirectory = Path.Join(Root, "mids");
        archetypes = new(() => ArchetypeData.LoadArchetypes(Mids("archetypes.json")));

        attribMods = new(() => ArchetypeData.LoadAttribMod(Path.Join(Root, "attribmod", "AttribMod.json")));
        enums = new(() => TotalsData.LoadEnumMaps(Mids("enums.json")));
        assemblyPowers = new(() => AssemblyData.LoadAssemblyPowers(Mids("assembly_powers.json")));
        assemblyEntities = new(() => AssemblyData.LoadAssemblyEntities(Mids("assembly_entities.json")));
        enhancementEffects = new(() => EnhancementData.LoadEnhancementEffects(Mids("enhancement_effects.json")));
        setBonuses = new(() => SetBonusData.LoadSetBonusDb(Mids("enhancement_sets.json"), Mids("set_bonus_powers.json")));
        levels = new(() => LevelData.LoadLevelSchedule(Mids("levels.json")));
        serverData = new(() => TotalsData.LoadServerData(Mids("server_data.json")));
        config = new(() => TotalsData.LoadEngineConfig(Mids("config.json")));
        maths = new(() => MathTables.Load(Path.Join(Root, "Maths.mhd")));
    }

    /// <summary>Gets the repository root.</summary>
    public string RepositoryRoot { get; }

    /// <summary>Gets <c>tests/fixtures</c>.</summary>
    public string Root { get; }

    /// <summary>Gets <c>tests/fixtures/mids</c>, the shared database dumps.</summary>
    public string MidsDirectory { get; }

    /// <summary>Gets the class table.</summary>
    public ArchetypeDb Archetypes => archetypes.Value;

    /// <summary>Gets the AttribMod tables.</summary>
    public AttribMods AttribMods => attribMods.Value;

    /// <summary>Gets the enum name and ordinal maps.</summary>
    public EnumMaps Enums => enums.Value;

    /// <summary>Gets the database-wide assembly powers.</summary>
    public IReadOnlyDictionary<int, AssemblyPower> AssemblyPowers => assemblyPowers.Value;

    /// <summary>Gets the summoned entities and their powersets.</summary>
    public AssemblyEntities AssemblyEntities => assemblyEntities.Value;

    /// <summary>Gets the enhancement records by index.</summary>
    public IReadOnlyDictionary<int, EnhancementRecord> EnhancementEffects => enhancementEffects.Value;

    /// <summary>Gets the set-bonus database.</summary>
    public SetBonusDb SetBonuses => setBonuses.Value;

    /// <summary>Gets the level schedule.</summary>
    public LevelSchedule Levels => levels.Value;

    /// <summary>Gets the server constants.</summary>
    public ServerData ServerData => serverData.Value;

    /// <summary>Gets the engine configuration the dumps were made under.</summary>
    public EngineConfig Config => config.Value;

    /// <summary>Gets the Maths.mhd tables.</summary>
    public MathTables Maths => maths.Value;

    /// <summary>Finds the repository by walking up from a directory to the one holding <c>CohEngine.slnx</c>.</summary>
    /// <param name="startDirectory">Where to start, typically the test assembly's directory.</param>
    /// <returns>A store rooted there.</returns>
    /// <exception cref="DirectoryNotFoundException">No ancestor holds <c>CohEngine.slnx</c>.</exception>
    public static FixtureStore Locate(string startDirectory)
    {
        for (DirectoryInfo? directory = new(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Join(directory.FullName, SolutionFile)))
            {
                return new FixtureStore(directory.FullName);
            }
        }

        throw new DirectoryNotFoundException($"no directory at or above {startDirectory} holds {SolutionFile}; run from inside the repository");
    }

    /// <summary>Lists the build dump names in ordinal order.</summary>
    /// <returns>The directory names under <c>tests/fixtures/mids/builds</c>.</returns>
    public IReadOnlyList<string> BuildNames()
    {
        return [.. Directory.GetDirectories(Path.Join(MidsDirectory, "builds")).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
    }

    /// <summary>Gets one build's dump, shared across callers.</summary>
    /// <param name="name">The build directory name.</param>
    /// <returns>The build's lazily loaded files.</returns>
    public BuildFixture Build(string name)
    {
        return builds.GetOrAdd(name, key => new BuildFixture(Path.Join(MidsDirectory, "builds", key)));
    }

    private string Mids(string file)
    {
        return Path.Join(MidsDirectory, file);
    }
}
