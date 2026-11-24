using CohEngine.Core.Chain;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Data;

/// <summary>The <c>extract-canonical</c> subcommand: rebuilds <c>data/canonical/</c> from the City of Data raw-data zip.</summary>
public static class CanonicalExtractCommand
{
    private const string Usage = "usage: coh-engine extract-canonical [--zip ZIP] [--out-dir DIR] [--force]";

    private const string ZipPattern = "raw_data_homecoming-*.zip";

    /// <summary>Runs the subcommand.</summary>
    /// <param name="args">The arguments after <c>extract-canonical</c>.</param>
    /// <param name="output">Where the summary goes.</param>
    /// <param name="error">Where errors go.</param>
    /// <param name="downloadsDirectory">Where the newest <c>raw_data_homecoming-*.zip</c> is picked from without <c>--zip</c>, or null for the user's Downloads folder.</param>
    /// <param name="repoRoot">The repository whose <c>data/canonical</c> is the target without <c>--out-dir</c>, or null to find it.</param>
    /// <returns>0 when extracted or already current, 1 on an input error, 2 on a usage error.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string? downloadsDirectory = null, string? repoRoot = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        string? zip = null;
        string? outDir = null;
        bool force = false;
        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            if (token is "--force")
            {
                force = true;
                continue;
            }

            if (token is not ("--zip" or "--out-dir"))
            {
                return UsageError(error, $"unrecognized arguments: {token}");
            }

            if (++i >= args.Count)
            {
                return UsageError(error, $"argument {token}: expected one argument");
            }

            zip = token is "--zip" ? args[i] : zip;
            outDir = token is "--out-dir" ? args[i] : outDir;
        }

        try
        {
            zip ??= NewestZip(downloadsDirectory ?? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            string destination = outDir ?? Path.Join(repoRoot ?? ChainPaths.RepoRoot(), "data", "canonical");
            (string sha256, bool extracted) = CanonicalZip.Extract(zip, destination, force);
            output.Write(extracted
                ? $"extracted {zip} (SHA256 {sha256}) into {destination} and wrote manifest.json\n"
                : $"{destination} already holds {zip} (SHA256 {sha256}, per manifest.json); pass --force to re-extract\n");
            return 0;
        }
        catch (EngineException failure)
        {
            return Fail(error, failure.Message);
        }
        catch (InvalidDataException failure)
        {
            return Fail(error, $"{ErrorCodes.E72}: cannot read {zip}: {failure.Message}; check that it is a City of Data raw-data zip");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            string where = failure is FileNotFoundException { FileName: { } file } ? file : "an input file";
            string reason = failure is FileNotFoundException or DirectoryNotFoundException ? "No such file or directory" : failure.Message;
            return Fail(error, $"{ErrorCodes.E72}: cannot read {where}: {reason}; check that the path exists and is readable");
        }
    }

    private static string NewestZip(string downloads)
    {
        FileInfo? newest = new DirectoryInfo(downloads).EnumerateFiles(ZipPattern).MaxBy(file => file.LastWriteTimeUtc);
        return newest?.FullName ?? throw new EngineException($"{ErrorCodes.E72}: cannot read {Path.Join(downloads, ZipPattern)}: No such file or directory; pass --zip <path>");
    }

    private static int Fail(TextWriter error, string message)
    {
        error.Write($"error: {message}\n");
        return 1;
    }

    private static int UsageError(TextWriter error, string message)
    {
        error.Write($"{Usage}\ncoh-engine extract-canonical: error: {message}\n");
        return 2;
    }
}
