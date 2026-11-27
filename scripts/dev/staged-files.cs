// staged-files.cs - the staged regular files and their staged content, for the pre-commit checks that judge
// what is staged rather than the working tree. The checks include it with #:include; it does not run alone.

using System.Diagnostics;
using System.Text;

static class StagedFiles
{
    // The staged regular files (added, copied, modified or type-changed) with their blob ids, or git's error.
    public static (List<(string Path, string Id)> Files, string? Error) List()
    {
        // --no-relative: top-level paths from any folder, whatever diff.relative says.
        string[] listing = ["diff", "--cached", "--raw", "--no-abbrev", "--no-renames", "--no-relative", "--diff-filter=ACMT", "-z"];
        ProcessStartInfo info = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in listing)
        {
            info.ArgumentList.Add(argument);
        }

        byte[] output;
        try
        {
            using Process process = Process.Start(info)!;
            using MemoryStream buffer = new();
            Task<string> error = process.StandardError.ReadToEndAsync();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return ([], $"git {string.Join(' ', listing)} failed ({process.ExitCode}): {error.GetAwaiter().GetResult().Trim()}");
            }

            output = buffer.ToArray();
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return ([], $"git {string.Join(' ', listing)} failed (127): {exception.Message}");
        }

        // Raw records alternate ":<old mode> <new mode> <old id> <new id> <status>" and the path.
        // A symlink (120000) holds a target and a submodule (160000) a commit: neither is text to judge.
        string[] records = Encoding.UTF8.GetString(output).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        List<(string Path, string Id)> files = [];
        for (int i = 0; i + 1 < records.Length; i += 2)
        {
            string[] meta = records[i].Split(' ');
            if (meta[1] is "100644" or "100755")
            {
                files.Add((records[i + 1], meta[3]));
            }
        }

        return (files, null);
    }

    // One git process serves every staged blob, so a commit of thousands of files starts git once, not per file.
    public static Process StartReader(IEnumerable<string> ids)
    {
        ProcessStartInfo info = new("git", "cat-file --batch")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        Process process = Process.Start(info)!;
        List<string> pending = [.. ids];

        // Written from another thread: git stops reading ids while its answers wait to be read.
        _ = Task.Run(() =>
        {
            foreach (string id in pending)
            {
                process.StandardInput.Write(id + "\n");
            }

            process.StandardInput.Close();
        });
        return process;
    }

    // The next blob, in the order the ids were given, or null when git does not have the object.
    public static byte[]? Next(Process reader)
    {
        Stream stream = reader.StandardOutput.BaseStream;
        StringBuilder header = new();
        for (int next = stream.ReadByte(); next is not ('\n' or -1); next = stream.ReadByte())
        {
            header.Append((char)next);
        }

        // "<id> blob <size>", or "<id> missing" when the object is not in the repository.
        if (header.ToString().Split(' ') is not [_, "blob", string size])
        {
            return null;
        }

        byte[] blob = new byte[int.Parse(size, System.Globalization.CultureInfo.InvariantCulture)];
        stream.ReadExactly(blob);
        _ = stream.ReadByte();
        return blob;
    }
}
