using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CohEngine.Core.Data;

/// <summary>Loaders for the assembly export (the Python port's <c>assembly_data</c>).</summary>
public static class AssemblyData
{
    /// <summary>Parses <c>assembly_powers.json</c>, keyed by database index.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The powers by index.</returns>
    /// <exception cref="InvalidDataException">E85 for a record or effect missing a key.</exception>
    public static IReadOnlyDictionary<int, AssemblyPower> LoadAssemblyPowers(string path)
    {
        IReadOnlyList<AssemblyPower> powers = LoadExtended(path, AssemblyJsonContext.Default.IReadOnlyListAssemblyPower);
        IReadOnlyList<EffectExtensions> extensions = LoadExtended(path, AssemblyJsonContext.Default.IReadOnlyListEffectExtensions);
        string name = Path.GetFileName(path);
        return powers
            .Select((power, i) => power with { Effects = Enrich(power.Effects, power.FullName, extensions[i].Effects, $"{name} power {power.FullName}") })
            .ToLastWinsDictionary(power => power.Nid);
    }

    /// <summary>Parses <c>assembly_entities.json</c>.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The entities and powersets by index.</returns>
    public static AssemblyEntities LoadAssemblyEntities(string path)
    {
        AssemblyEntitiesFile raw = Json.Load(path, AssemblyJsonContext.Default.AssemblyEntitiesFile);
        return new AssemblyEntities(raw.Entities.ToLastWinsDictionary(entity => entity.Nid), raw.Powersets.ToLastWinsDictionary(set => set.Nid));
    }

    /// <summary>Parses a build's <c>power_assembly.json</c> records in file order.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The records.</returns>
    /// <exception cref="InvalidDataException">E85 for a record or effect missing a key.</exception>
    public static IReadOnlyList<PowerAssemblyRecord> LoadPowerAssemblyRecords(string path)
    {
        return LoadExtended(path, AssemblyJsonContext.Default.IReadOnlyListPowerAssemblyRecord);
    }

    /// <summary>Parses <c>assembled_powers.json</c>: Mids' assembled effects per StatInclude build power, by build index.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The effects by build index.</returns>
    /// <exception cref="InvalidDataException">E85 for a record or effect missing a key.</exception>
    public static IReadOnlyDictionary<int, IReadOnlyList<Effect>> LoadAssembledPowers(string path)
    {
        IReadOnlyList<AssembledPower> powers = LoadExtended(path, AssemblyJsonContext.Default.IReadOnlyListAssembledPower);
        IReadOnlyList<EffectExtensions> extensions = LoadExtended(path, AssemblyJsonContext.Default.IReadOnlyListEffectExtensions);
        string name = Path.GetFileName(path);
        return powers
            .Select((power, i) => (power.BuildIndex, Effects: (IReadOnlyList<Effect>)Enrich(power.Effects, power.FullName, extensions[i].Effects, $"{name} build power {power.BuildIndex}")))
            .ToLastWinsDictionary(power => power.BuildIndex, power => power.Effects);
    }

    /// <summary>Joins a build's <c>power_assembly.json</c> and <c>slots.json</c> onto its <c>powers_effects.json</c> powers.</summary>
    /// <param name="powerAssemblyPath">The build's <c>power_assembly.json</c>.</param>
    /// <param name="slotsPath">The build's <c>slots.json</c>.</param>
    /// <param name="powers">The build's powers.</param>
    /// <returns>The assembly inputs by build index.</returns>
    /// <exception cref="InvalidDataException">E85 for an effect with no extended keys or a slot entry with no ProcInclude; E86 for a build power with no record.</exception>
    public static IReadOnlyDictionary<int, BuildPowerAssembly> LoadBuildPowerAssembly(string powerAssemblyPath, string slotsPath, IReadOnlyList<Power> powers)
    {
        ArgumentNullException.ThrowIfNull(powers);
        string name = Path.GetFileName(powerAssemblyPath);
        string slotsName = Path.GetFileName(slotsPath);
        Dictionary<int, PowerAssemblyRecord> records = LoadPowerAssemblyRecords(powerAssemblyPath).ToLastWinsDictionary(record => record.BuildIndex);
        Dictionary<int, bool> procInclude = EnhancementData.LoadSlotEntries(slotsPath).ToLastWinsDictionary(
            entry => entry.BuildIndex,
            entry => entry.ProcInclude ?? throw new InvalidDataException(
                $"E85: {slotsName} build power {entry.BuildIndex} is missing ['ProcInclude']; expected every key of the assembly export record; "
                + "re-run the Mids dump harness so the file carries them"));
        Dictionary<int, BuildPowerAssembly> result = [];
        foreach (Power power in powers)
        {
            if (!records.TryGetValue(power.BuildIndex, out PowerAssemblyRecord? record))
            {
                throw MissingRecord(name, power);
            }

            if (!procInclude.TryGetValue(power.BuildIndex, out bool include))
            {
                throw MissingRecord(slotsName, power);
            }

            result[power.BuildIndex] = new BuildPowerAssembly(
                power.BuildIndex,
                include,
                record.SubPowers,
                new AssemblyPower(record.EntitiesAffectedJson, record.EntitiesAutoHitJson, power.Enhancements.ToHashSet(), record.IgnoreEnhJson)
                {
                    Nid = power.NidPower,
                    FullName = power.FullName,
                    PowerType = power.PowerType,
                    PowerSetType = record.PowerSetType,
                    ActivatePeriod = power.ActivatePeriod,
                    RechargeTime = power.RechargeTime,
                    BaseRechargeTime = record.BaseRechargeTime,
                    CastTime = power.CastTime,
                    AoeModifier = record.AoeModifier,
                    HasGrantPowerEffect = record.HasGrantPowerEffect,
                    HasPowerOverrideEffect = record.HasPowerOverrideEffect,
                    AbsorbSummonEffects = record.AbsorbSummonEffects,
                    AbsorbSummonAttributes = record.AbsorbSummonAttributes,
                    Effects = Enrich(power.Effects, power.FullName, record.Effects, $"{name} build power {power.BuildIndex}"),
                });
        }

        return result;
    }

    /// <summary>Deserializes an extended-record file, reporting a missing key as E85 as the Python loader does.</summary>
    private static T LoadExtended<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return Json.Load(path, typeInfo);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"E85: {Path.GetFileName(path)} is missing a key ({exception.Message}); expected every key of the assembly export record; "
                + "re-run the Mids dump harness so the file carries them",
                exception);
        }
    }

    private static Effect[] Enrich(IReadOnlyList<Effect> effects, string fullName, IReadOnlyList<EffectExtension> extensions, string where)
    {
        Dictionary<int, EffectExtension> byIndex = [];
        foreach (EffectExtension extension in extensions)
        {
            byIndex[extension.Index] = extension;
        }

        int[] missing = [.. effects.Select(effect => effect.Index).Where(index => !byIndex.ContainsKey(index))];
        if (missing.Length > 0)
        {
            throw new InvalidDataException(
                $"E85: {where} has no extended keys for effect(s) [{string.Join(", ", missing)}] of {fullName}; expected one entry "
                + "per effect of powers_effects.json; re-run the Mids dump harness");
        }

        return
        [
            .. effects.Select(effect =>
            {
                EffectExtension extension = byIndex[effect.Index];
                return effect with
                {
                    IsEnhancementEffect = extension.IsEnhancementEffect,
                    Summon = extension.Summon,
                    NSummon = extension.NSummon,
                    Duration = extension.Duration,
                    EffectId = extension.EffectId,
                    Reward = extension.Reward,
                    Conditionals = extension.ActiveConditionals,
                    AbsorbedPowerNid = extension.AbsorbedPowerNid,
                };
            }),
        ];
    }

    private static InvalidDataException MissingRecord(string source, Power power)
    {
        return new InvalidDataException(
            $"E86: {source} has no record for build power {power.BuildIndex} ({power.FullName}); "
            + "expected one per power of powers_effects.json; re-run the Mids dump harness");
    }
}
