// diff-goldens.cs - list every numeric field where the 3.x and 4.0 Mids reference dumps differ.
//
// Run:  dotnet run scripts/dev/diff-goldens.cs [-- --summary] [-- <dir-3x> <dir-4.0>]
//
// For each build present in both trees, compares every numeric leaf of totals.json and
// enhanced_powers.json after rounding both sides to float32, and prints one line per
// differing field:  <build>/<file>:<json path> 3x=<value> 4.0=<value>
// --summary prints the count of differing fields per file instead. Reports only: exit 0.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
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
string[] files = ["totals.json", "enhanced_powers.json"];

string builds3 = Path.Combine(dir3x, "builds");
string builds4 = Path.Combine(dir4, "builds");
if (!Directory.Exists(builds3) || !Directory.Exists(builds4))
{
    Console.Error.WriteLine($"E01: missing builds dir: {builds3} or {builds4}");
    return 0;
}

Dictionary<string, int> counts = files.ToDictionary(f => f, _ => 0);
int compared = 0;
foreach (string build in Directory.EnumerateDirectories(builds3).Select(Path.GetFileName).Order(StringComparer.Ordinal)!)
{
    foreach (string file in files)
    {
        string a = Path.Combine(builds3, build!, file);
        string b = Path.Combine(builds4, build!, file);
        if (!File.Exists(a) || !File.Exists(b))
        {
            continue;
        }

        compared++;
        Dictionary<string, JsonElement> leaves3 = Leaves(File.ReadAllText(a));
        Dictionary<string, JsonElement> leaves4 = Leaves(File.ReadAllText(b));
        foreach (string path in leaves3.Keys.Union(leaves4.Keys).Order(StringComparer.Ordinal))
        {
            bool has3 = leaves3.TryGetValue(path, out JsonElement v3);
            bool has4 = leaves4.TryGetValue(path, out JsonElement v4);
            // Non-numeric leaves (names, null) are compared only when the other side is numeric.
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

            counts[file]++;
            if (!summary)
            {
                Console.WriteLine($"{build}/{file}:{path} 3x={Show(has3, v3)} 4.0={Show(has4, v4)}");
            }
        }
    }
}

if (summary)
{
    Console.WriteLine($"compared {compared} file pairs");
    foreach (string file in files)
    {
        Console.WriteLine($"{file}: {counts[file]} differing fields");
    }
}

return 0;

static string Show(bool present, JsonElement v) => !present
    ? "<absent>"
    : v.ValueKind == JsonValueKind.Number
        ? ((float)v.GetDouble()).ToString("R", CultureInfo.InvariantCulture)
        : v.ValueKind == JsonValueKind.Null ? "null" : v.ToString();

static Dictionary<string, JsonElement> Leaves(string json)
{
    Dictionary<string, JsonElement> map = [];
    using JsonDocument doc = JsonDocument.Parse(json);
    Walk(doc.RootElement.Clone(), "$", map);
    return map;
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
