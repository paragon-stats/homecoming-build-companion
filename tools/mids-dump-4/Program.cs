// MidsDump — test-only harness that loads the MidsReborn Homecoming database
// headless (no UI) and dumps the reference data the Python port (src/coh_engine)
// baselines its golden fixtures against.
//
// Usage: MidsDump4 <databases-dir> <output-dir>
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
using Mids_Reborn.Core.Compatibility;

namespace MidsDump;

internal static partial class Program
{
    private const string HarnessName = "MidsDump4";

    private static string UnpatchedMessage()
    {
        return
                "E15: the MidsReborn fork is unpatched: PlannerPowerPipeline still runs its per-power loops under "
                + "Parallel.For. The 22 sample dumps were byte-identical patched and unpatched, so this guards "
                + "determinism, not a demonstrated race, and keeps the order the 3.x goldens assume. Apply "
                + $"git -C <fork4> apply {PatchPath()} and rebuild the harness, or pass --allow-unpatched to "
                + "repeat the determinism comparison";
    }

    // Mids 4.0 reports load problems through modal dialogs, which block a headless run forever. The
    // notifier is private, so swap it by reflection for one that records the messages instead.
    private sealed class HeadlessNotifier : IBuildNotifier
    {
        public List<string> Notices { get; } = [];

        private void Note(string message) => Notices.Add(message.Replace("\r", " ").Replace("\n", " ").Trim());

        public void ShowError(string message) => Note(message);

        public DialogResult ShowErrorDialog(string message, string title)
        {
            Note(message);
            return DialogResult.OK;
        }

        public void ShowWarning(string message) => Note(message);

        public DialogResult ShowWarningDialog(string message, string title, bool showIgnore = false)
        {
            Note(message);
            return DialogResult.No;
        }

        public void ShowInfo(string message) => Note(message);

        public DialogResult ShowInfoDialog(string message, string title)
        {
            Note(message);
            return DialogResult.OK;
        }

        public void ShowCompatibilitySummary(CompatibilityLoadSummary summary) => Note(
            $"lineage {summary.Lineage}, legacy tag {summary.LegacyTag}: remapped powers {summary.RemappedPowerCount}, "
            + $"remapped enhancements {summary.RemappedEnhancementCount}, migrated Fitness powers {summary.MigratedFitnessPowerCount}, "
            + $"removed legacy Fitness pool {summary.RemovedLegacyFitnessPool}");

        public void ShowCompatibilityFailure(CompatibilityFailureReport failureReport) => Note(failureReport.BuildDisplayMessage());
    }

    private static readonly HeadlessNotifier Notifier = new();

    private static bool InstallHeadlessNotifier()
    {
        var field = typeof(BuildManager).GetField("_notifier", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            Console.Error.WriteLine("E16: BuildManager has no private field '_notifier'; the headless notifier cannot be installed, so build-load dialogs would block the run");
            return false;
        }

        field.SetValue(BuildManager.Instance, Notifier);
        return true;
    }

    // Notices a build raised while loading successfully (for example the LegacyLoadMap remaps): name -> text.
    private static readonly SortedDictionary<string, string> Remapped = new(StringComparer.Ordinal);

    private static bool LoadBuild(string mbdPath)
    {
        Notifier.Notices.Clear();
        var ok = BuildManager.Instance.LoadFromFile(mbdPath);
        if (ok && Notifier.Notices.Count > 0)
        {
            Remapped[Path.GetFileNameWithoutExtension(mbdPath)] = string.Join(" | ", Notifier.Notices);
        }

        return ok;
    }

    // Builds Mids 4.0 could not load: name -> the notice text, written to SKIPPED.txt.
    private static readonly SortedDictionary<string, string> Skipped = new(StringComparer.Ordinal);

    private static void WriteSkipped(string outDir)
    {
        File.WriteAllLines(
            Path.Combine(outDir, "SKIPPED.txt"),
            Skipped.Select(kv => $"{kv.Key}\t{kv.Value}"));
        File.WriteAllLines(
            Path.Combine(outDir, "REMAPPED.txt"),
            Remapped.Select(kv => $"{kv.Key}\t{kv.Value}"));
    }

    private static string PatchPath([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        return Path.Combine(Path.GetDirectoryName(source)!, "patches", "pipeline-sequential.patch");
    }

    // The patch replaces both Parallel.For lambdas in PlannerPowerPipeline with plain loops, so the compiled
    // fork has no lambda left whose name the compiler derives from the two phase methods.
    private static bool ForkIsPatched()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Type[] types;
        try { types = typeof(Toon).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
        return !types.SelectMany(t => t.GetMethods(all))
            .Any(m => m.Name.StartsWith("<ExecutePerPowerEnhancementMathPhase>", StringComparison.Ordinal)
                || m.Name.StartsWith("<ExecutePostBuffMultiplyPhase>", StringComparison.Ordinal));
    }

    // Mids 4.0 refuses some builds by design; they are listed in SKIPPED.txt, not counted as failures.
    private static int OnLoadFailure(string mbdPath)
    {
        var why = string.Join(" | ", Notifier.Notices);
        Console.Error.WriteLine($"E13: skipped {mbdPath}: {why}");
        Skipped[Path.GetFileNameWithoutExtension(mbdPath)] = why;
        return 0;
    }

    private static int DumpExtraVariants(IReadOnlyDictionary<string, string> byName, string outDir)
    {
        WriteSkipped(outDir);
        return 0;
    }
}
