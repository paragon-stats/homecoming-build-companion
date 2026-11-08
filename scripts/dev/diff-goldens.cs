// diff-goldens.cs - list every numeric field where the 3.x and 4.0 Mids reference dumps differ.
//
// Run:  dotnet run scripts/dev/diff-goldens.cs [-- --summary] [-- <dir-3x> <dir-4.0>]
//
// For each build present in both trees, compares the numeric leaves of totals.json and
// enhanced_powers.json after rounding both sides to float32, and prints one line per
// differing field:  <build>/<file>:<json path> 3x=<value> 4.0=<value>
//
// enhanced_powers.json is keyed, not positional, because 4.0 remaps and reorders powers:
//   - a power is keyed by FullName (a repeated FullName gets #2, #3, ...);
//   - an effect is keyed by (EffectType, MezType, ETModifies) plus its occurrence
//     number among the power's effects with the same tuple (DamageType is left out: 4.0 fills it on
//     effects that carry none in 3.x, for example Melee on JumpHeight);
//   - BuildIndex and Index are positions, not values, and are not compared.
// A power present in only one tree is one line "only in 3x" or "only in 4.0", not one per field.
//
// --summary prints the per-file, per-build and only-in-one-tree counts as Markdown tables.
// Reports only: exit 0. CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false

using System.Globalization;
using System.Text.Json;

string[] positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
bool summary = args.Contains("--summary");
string dir3x = positional.Length > 0
    ? positional[0]
    : new[] { Path.Combine("tests", "fixtures", "mids"), Path.Combine("src", "coh_engine", "tests", "fixtures", "mids") }
        .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "builds"))) ?? Path.Combine("tests", "fixtures", "mids");
string dir4 = positional.Length > 1 ? positional[1] : Path.Combine("tests", "fixtures", "mids4");

string builds3 = Path.Combine(dir3x, "builds");
string builds4 = Path.Combine(dir4, "builds");
if (!Directory.Exists(builds3) || !Directory.Exists(builds4))
{
    Console.Error.WriteLine($"E01: missing builds dir: {builds3} or {builds4}");
    return 0;
}

List<string> lines = [];
List<(string Build, int Totals, int Enhanced, int Only3, int Only4)> rows = [];
int compared = 0;
int shared = 0;
foreach (string build in Directory.EnumerateDirectories(builds3).Select(d => Path.GetFileName(d)!).Order(StringComparer.Ordinal))
{
    int totals = 0;
    int enhanced = 0;
    int only3 = 0;
    int only4 = 0;

    string t3 = Path.Combine(builds3, build, "totals.json");
    string t4 = Path.Combine(builds4, build, "totals.json");
    if (File.Exists(t3) && File.Exists(t4))
    {
        compared++;
        totals += Compare($"{build}/totals.json", Leaves(File.ReadAllText(t3), "$"), Leaves(File.ReadAllText(t4), "$"));
    }

    string e3 = Path.Combine(builds3, build, "enhanced_powers.json");
    string e4 = Path.Combine(builds4, build, "enhanced_powers.json");
    if (File.Exists(e3) && File.Exists(e4))
    {
        compared++;
        Dictionary<string, Dictionary<string, JsonElement>> p3 = Powers(File.ReadAllText(e3));
        Dictionary<string, Dictionary<string, JsonElement>> p4 = Powers(File.ReadAllText(e4));
        foreach (string name in p3.Keys.Union(p4.Keys).Order(StringComparer.Ordinal))
        {
            bool in3 = p3.TryGetValue(name, out Dictionary<string, JsonElement>? a);
            bool in4 = p4.TryGetValue(name, out Dictionary<string, JsonElement>? b);
            if (in3 && in4)
            {
                shared++;
                enhanced += Compare($"{build}/enhanced_powers.json", a!, b!, name);
            }
            else
            {
                lines.Add($"{build}/enhanced_powers.json:{name} only in {(in3 ? "3x" : "4.0")}");
                _ = in3 ? only3++ : only4++;
            }
        }
    }

    rows.Add((build, totals, enhanced, only3, only4));
}

if (summary)
{
    Console.WriteLine($"Compared {compared} file pairs and {shared} powers present in both trees.");
    Console.WriteLine();
    Console.WriteLine("| File | Differing fields | Powers only in 3.x | Powers only in 4.0 |");
    Console.WriteLine("| --- | --- | --- | --- |");
    Console.WriteLine($"| totals.json | {rows.Sum(r => r.Totals)} | | |");
    Console.WriteLine($"| enhanced_powers.json | {rows.Sum(r => r.Enhanced)} | {rows.Sum(r => r.Only3)} | {rows.Sum(r => r.Only4)} |");
    Console.WriteLine();
    Console.WriteLine("| Build | totals.json | enhanced_powers.json | Powers only in 3.x | Powers only in 4.0 |");
    Console.WriteLine("| --- | --- | --- | --- | --- |");
    foreach ((string build, int totals, int enhanced, int only3, int only4) in rows)
    {
        Console.WriteLine(Directory.Exists(Path.Combine(builds4, build))
            ? $"| `{build}` | {totals} | {enhanced} | {only3} | {only4} |"
            : $"| `{build}` | no 4.0 dump | no 4.0 dump | | |");
    }
}
else
{
    foreach (string line in lines)
    {
        Console.WriteLine(line);
    }
}

return 0;

// Compares two leaf maps; adds one line per differing numeric field and returns how many.
int Compare(string label, Dictionary<string, JsonElement> leaves3, Dictionary<string, JsonElement> leaves4, string? prefix = null)
{
    int count = 0;
    foreach (string path in leaves3.Keys.Union(leaves4.Keys).Order(StringComparer.Ordinal))
    {
        bool has3 = leaves3.TryGetValue(path, out JsonElement v3);
        bool has4 = leaves4.TryGetValue(path, out JsonElement v4);
        bool num3 = has3 && v3.ValueKind == JsonValueKind.Number;
        bool num4 = has4 && v4.ValueKind == JsonValueKind.Number;
        if (!num3 && !num4)
        {
            continue;
        }

        if (num3 && num4 && (float)v3.GetDouble() == (float)v4.GetDouble())
        {
            continue;
        }

        count++;
        lines.Add($"{label}:{(prefix == null ? string.Empty : prefix + " ")}{path} 3x={Show(has3, v3)} 4.0={Show(has4, v4)}");
    }

    return count;
}

static string Show(bool present, JsonElement v) => !present
    ? "<absent>"
    : v.ValueKind == JsonValueKind.Number
        ? ((float)v.GetDouble()).ToString("R", CultureInfo.InvariantCulture)
        : v.ValueKind == JsonValueKind.Null ? "null" : v.ToString();

static Dictionary<string, JsonElement> Leaves(string json, string root)
{
    Dictionary<string, JsonElement> map = [];
    using JsonDocument doc = JsonDocument.Parse(json);
    Walk(doc.RootElement.Clone(), root, map);
    return map;
}

// Powers keyed by FullName; each map holds the power's own leaves plus its effects' leaves.
static Dictionary<string, Dictionary<string, JsonElement>> Powers(string json)
{
    Dictionary<string, Dictionary<string, JsonElement>> result = [];
    using JsonDocument doc = JsonDocument.Parse(json);
    foreach (JsonElement power in doc.RootElement.EnumerateArray())
    {
        string name = power.GetProperty("FullName").GetString() ?? "?";
        string key = name;
        for (int n = 2; result.ContainsKey(key); n++)
        {
            key = $"{name}#{n}";
        }

        Dictionary<string, JsonElement> map = [];
        Dictionary<string, int> seen = [];
        foreach (JsonProperty p in power.EnumerateObject())
        {
            if (p.Name == "BuildIndex")
            {
                continue;
            }

            if (p.Name != "Effects")
            {
                Walk(p.Value.Clone(), $"$.{p.Name}", map);
                continue;
            }

            foreach (JsonElement fx in p.Value.EnumerateArray())
            {
                string tuple = string.Join(
                    "|",
                    new[] { "EffectType", "MezType", "ETModifies" }.Select(f => fx.GetProperty(f).GetString()));
                seen[tuple] = seen.GetValueOrDefault(tuple) + 1;
                foreach (JsonProperty f in fx.EnumerateObject())
                {
                    if (f.Name is "Index" or "EffectType" or "DamageType" or "MezType" or "ETModifies")
                    {
                        continue;
                    }

                    Walk(f.Value.Clone(), $"$.Effects[{tuple}#{seen[tuple]}].{f.Name}", map);
                }
            }
        }

        result[key] = map;
    }

    return result;
}

static void Walk(JsonElement e, string path, Dictionary<string, JsonElement> map)
{
    switch (e.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (JsonProperty p in e.EnumerateObject())
            {
                Walk(p.Value, $"{path}.{p.Name}", map);
            }

            break;
        case JsonValueKind.Array:
            int i = 0;
            foreach (JsonElement item in e.EnumerateArray())
            {
                Walk(item, $"{path}[{i++}]", map);
            }

            break;
        default:
            map[path] = e;
            break;
    }
}
