using CohEngine.Core.Data;

namespace CohEngine.TestSupport;

/// <summary>One build's Mids reference dump; each file is read on first use.</summary>
public sealed class BuildFixture
{
    private readonly Lazy<IReadOnlyList<Power>> powers;
    private readonly Lazy<IReadOnlyList<BuildSlots>> slots;
    private readonly Lazy<IReadOnlyList<EnhancedPower>> enhancedPowers;
    private readonly Lazy<MidsTotals> totals;
    private readonly Lazy<IReadOnlyList<Incarnate>> incarnates;
    private readonly Lazy<SetBonusVirtualPower> setBonusVirtualPower;
    private readonly Lazy<IReadOnlyDictionary<int, IReadOnlyList<Effect>>> assembledPowers;
    private readonly Lazy<IReadOnlyDictionary<int, BuildPowerAssembly>> powerAssembly;

    /// <summary>Initializes a new instance of the <see cref="BuildFixture"/> class.</summary>
    /// <param name="directory">The build's dump directory.</param>
    public BuildFixture(string directory)
    {
        Directory = directory;
        Name = Path.GetFileName(directory);
        powers = new(() => EffectData.LoadPowersEffects(PathOf("powers_effects.json")));
        slots = new(() => EnhancementData.LoadSlotEntries(PathOf("slots.json")));
        enhancedPowers = new(() => TotalsData.LoadEnhancedPowers(PathOf("enhanced_powers.json")));
        totals = new(() => TotalsData.LoadTotals(PathOf("totals.json")));
        incarnates = new(() => IncarnateData.LoadIncarnates(PathOf("incarnates.json")));
        setBonusVirtualPower = new(() => SetBonusData.LoadSetBonusVirtualPower(PathOf("set_bonus_virtual_power.json")));
        assembledPowers = new(() => AssemblyData.LoadAssembledPowers(PathOf("assembled_powers.json")));
        powerAssembly = new(() => AssemblyData.LoadBuildPowerAssembly(PathOf("power_assembly.json"), PathOf("slots.json"), Powers));
    }

    /// <summary>Gets the build directory name.</summary>
    public string Name { get; }

    /// <summary>Gets the build's dump directory.</summary>
    public string Directory { get; }

    /// <summary>Gets <c>powers_effects.json</c>.</summary>
    public IReadOnlyList<Power> Powers => powers.Value;

    /// <summary>Gets <c>slots.json</c> in file order.</summary>
    public IReadOnlyList<BuildSlots> Slots => slots.Value;

    /// <summary>Gets <c>enhanced_powers.json</c>.</summary>
    public IReadOnlyList<EnhancedPower> EnhancedPowers => enhancedPowers.Value;

    /// <summary>Gets <c>totals.json</c>.</summary>
    public MidsTotals Totals => totals.Value;

    /// <summary>Gets <c>incarnates.json</c>.</summary>
    public IReadOnlyList<Incarnate> Incarnates => incarnates.Value;

    /// <summary>Gets <c>set_bonus_virtual_power.json</c>.</summary>
    public SetBonusVirtualPower SetBonusVirtualPower => setBonusVirtualPower.Value;

    /// <summary>Gets <c>assembled_powers.json</c> by build index; only the assembly-era dumps carry it.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<Effect>> AssembledPowers => assembledPowers.Value;

    /// <summary>Gets <c>power_assembly.json</c> joined onto the powers and slots; only the assembly-era dumps carry it.</summary>
    public IReadOnlyDictionary<int, BuildPowerAssembly> PowerAssembly => powerAssembly.Value;

    /// <summary>Whether the dump holds a file.</summary>
    /// <param name="file">The file name, for example <c>power_assembly.json</c>.</param>
    /// <returns>True if the file exists.</returns>
    public bool Has(string file)
    {
        return File.Exists(PathOf(file));
    }

    private string PathOf(string file)
    {
        return Path.Join(Directory, file);
    }
}
