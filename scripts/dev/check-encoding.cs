// check-encoding.cs - fail on non-ASCII characters in script/CLI output source.
//
// Run by CI:               dotnet run scripts/dev/check-encoding.cs              (tracked files, working tree)
// Run by the commit hook:  dotnet run scripts/dev/check-encoding.cs -- --staged  (staged content)
//
// Console/CLI output must be ASCII so it does not mojibake on a legacy-codepage
// Windows console (ASCII only). Scans the tracked tooling scripts and
// the CLI for non-ASCII characters (codepoint > U+007E, or a control char other than
// tab) and reports file:line:column - one offence per visible character. A leading
// UTF-8 BOM is ignored (a file marker, not output). Prose (docs), untracked files and
// build output are NOT scanned.
// CI tooling, not shipped product code: exempt from the solution-wide analyzers.
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property RunAnalyzers=false
#:include staged-files.cs

using System.Diagnostics;
using System.Text;

string[] roots = ["scripts/dev/", "src/CohEngine.Cli/"];
string[] extensions = [".cs", ".py", ".sh", ".ps1"];
bool InScope(string path) => roots.Any(root => path.StartsWith(root, StringComparison.Ordinal)) && extensions.Contains(Path.GetExtension(path));

List<(string Path, string? Id)> files;
string top = string.Empty;
if (args.Contains("--staged"))
{
    (List<(string Path, string Id)> entries, string? error) = StagedFiles.List();
    if (error is not null)
    {
        Console.Error.WriteLine($"[FAIL] {error}; the file list is unknown.");
        return 2;
    }

    files = [.. entries.Where(entry => InScope(entry.Path)).Select(entry => (entry.Path, (string?)entry.Id))];
}
else
{
    // Top-level pathspecs and names, so a run from a subfolder sees the same files; read through the way up.
    (int listCode, string listing) = Git("ls-files", "-z", "--full-name", "--", ":/scripts/dev", ":/src/CohEngine.Cli");
    (int upCode, string up) = Git("rev-parse", "--show-cdup");
    if (listCode != 0 || upCode != 0)
    {
        Console.Error.WriteLine($"[FAIL] git ls-files exited {listCode}; the file list is unknown.");
        return 2;
    }

    top = up.Trim();
    files = [.. listing.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => InScope(path) && File.Exists(Path.Join(top, path))).Select(path => (path, (string?)null))];
}

using Process? blobs = files.Any(file => file.Id is not null) ? StagedFiles.StartReader(files.Select(file => file.Id!)) : null;
List<string> offences = [];
foreach ((string path, string? id) in files)
{
    byte[]? bytes = id is null ? File.ReadAllBytes(Path.Join(top, path)) : StagedFiles.Next(blobs!);
    if (bytes is null)
    {
        offences.Add($"{path}:1:1: git cannot read the staged content of this file");
        continue;
    }

    // Read as File.ReadAllLines does: a leading BOM is detected and stripped, so the scan sees
    // characters (one offence per visible glyph, editor-accurate column) - not bytes.
    using StreamReader reader = new(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    int lineNumber = 0;
    for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
    {
        lineNumber++;
        for (int col = 0; col < line.Length; col++)
        {
            char c = line[col];
            if (c != '\t' && (c < ' ' || c > '~'))
            {
                offences.Add($"{path}:{lineNumber}:{col + 1}: non-ASCII U+{(int)c:X4}");
            }
        }
    }
}

if (offences.Count > 0)
{
    Console.Error.WriteLine("[FAIL] non-ASCII characters in script/CLI output source:");
    foreach (string offence in offences)
    {
        Console.Error.WriteLine($"  {offence}");
    }

    Console.Error.WriteLine("Keep output in these paths ASCII.");
    return 1;
}

Console.WriteLine("[OK] script/CLI output source is ASCII-clean.");
return 0;

static (int Code, string Output) Git(params string[] arguments)
{
    ProcessStartInfo info = new("git") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = new UTF8Encoding(false) };
    foreach (string argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }

    using Process git = Process.Start(info)!;
    string output = git.StandardOutput.ReadToEnd();
    git.WaitForExit();
    return (git.ExitCode, output);
}
