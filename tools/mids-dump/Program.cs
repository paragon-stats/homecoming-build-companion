// MidsDump — test-only harness that loads the MidsReborn Homecoming database
// headless (no UI) and dumps the reference data the Python port (src/coh_engine)
// baselines its golden fixtures against.
//
// Usage: MidsDump <databases-dir> <output-dir>
//   <databases-dir>  e.g. <midsreborn-fork>/MidsReborn/Databases/Homecoming
//   <output-dir>     directory to write the JSON dumps into (created if missing)
//
// Load sequence mirrors MainModule.LoadDataAsync (MidsReborn MainModule.cs:145-218),
// minus graphics. Dialogs only fire on load errors.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mids_Reborn;
using Mids_Reborn.Core;
using Mids_Reborn.Core.Base.Data_Classes;
using Mids_Reborn.Core.Base.Master_Classes;
using Mids_Reborn.Core.BuildFile;

namespace MidsDump;

internal static partial class Program
{
    private const string HarnessName = "MidsDump";

    private static string UnpatchedMessage()
    {
        return
                "E15: the MidsReborn fork is unpatched: clsToonX.GenerateBuffedPowerArray still runs its pass loops "
                + "under Parallel.For, which races and silently gives wrong endurance numbers on incarnate and "
                + "set-bonus builds; apply tools/mids-dump/patches/gbpa-sequential.patch to the fork "
                + "(git -C <fork> apply ...) and rebuild the harness";
    }

    // The patch replaces both Parallel.For lambdas with plain loops, so the compiled fork has no
    // lambda left whose name the compiler derives from GenerateBuffedPowerArray.
    private static bool ForkIsPatched()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var types = new[] { typeof(clsToonX) }.Concat(typeof(clsToonX).GetNestedTypes(all));
        return !types.SelectMany(t => t.GetMethods(all))
            .Any(m => m.Name.StartsWith("<GenerateBuffedPowerArray>", StringComparison.Ordinal));
    }

    // Clicks-off baseline for the chain planner: Mids treats every StatInclude'd click as
    // always-on, so Aim / Build Up damage buffs sit permanently in DamagePerActivation and
    // totals. Each build is reloaded with StatInclude cleared on its self-targeted DamageBuff
    // click powers; the planner then applies buff uptime itself. Fresh toon per variant, so
    // no global state needs restoring.
    private static int DumpExtraVariants(IReadOnlyDictionary<string, string> byName, string outDir)
    {
        var failures = 0;
        foreach (var (name, mbdPath) in byName)
        {
            // Clear any previous run's output first so a failure below can never leave
            // stale data for the Python side to pick up.
            var variantDir = Path.Combine(outDir, "builds", $"{name}_clicksoff");
            if (Directory.Exists(variantDir))
            {
                Directory.Delete(variantDir, true);
            }

            var toon = new clsToonX();
            MainModule.MidsController.Toon = toon;
            if (!BuildManager.Instance.LoadFromFile(mbdPath))
            {
                Console.Error.WriteLine($"E23: LoadFromFile failed for {mbdPath} (clicks-off variant)");
                failures++;
                continue;
            }

            var off = new List<string>();
            foreach (var entry in toon.CurrentBuild.Powers)
            {
                if (entry?.Power == null || !entry.StatInclude || entry.Power.PowerType != Enums.ePowerType.Click)
                {
                    continue;
                }

                if (!entry.Power.Effects.Any(e => e.ToWho == Enums.eToWho.Self && e.EffectType == Enums.eEffectType.DamageBuff))
                {
                    continue;
                }

                entry.StatInclude = false;
                off.Add(entry.Power.FullName);
            }

            try
            {
                toon.GenerateBuffedPowerArray();
                Console.WriteLine($"clicksoff {name}: [{string.Join(", ", off)}]");
                DumpOneBuild(variantDir, toon);
                failures += DumpForumExport(variantDir, name);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"E23: clicks-off dump failed for {mbdPath}: {ex.Message}");
                failures++;
                if (Directory.Exists(variantDir))
                {
                    Directory.Delete(variantDir, true);
                }
            }
        }

        return failures;
    }

    private static bool LoadBuild(string mbdPath)
    {
        return BuildManager.Instance.LoadFromFile(mbdPath);
    }

    private static int OnLoadFailure(string mbdPath)
    {
        Console.Error.WriteLine($"E13: LoadFromFile failed for {mbdPath}");
        return 1;
    }
}
