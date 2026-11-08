using System.Globalization;
using System.Text.Json;

using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.Core.Statistics;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Chain;

/// <summary>Replacement points the tests use to force classifier and pet outcomes.</summary>
internal sealed record ChainHooks
{
    /// <summary>Gets a replacement for the attack classifier.</summary>
    public Func<JsonElement, double, PowerResolver?, AttackProfile>? Classify { get; init; }

    /// <summary>Gets a replacement for the pet builder.</summary>
    public Func<IReadOnlyList<ChainAttack>, string, AttribMods, ArchetypeDb, IReadOnlyList<PetSummon>>? BuildPets { get; init; }

    /// <summary>Gets a replacement for the attack builder.</summary>
    public Func<IReadOnlyDictionary<string, PowerDamageRef>, string, double, (IReadOnlyList<ChainAttack> Attacks, IReadOnlyList<(string Name, string Reason)> Skipped)>? Attacks { get; init; }
}
