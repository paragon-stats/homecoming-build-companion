using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;

namespace CohEngine.Core;

/// <summary>The <c>score</c> command: the scoring metrics of a build dump, as JSON in the shape the MCP <c>score</c> tool answers with.</summary>
/// <param name="BuildDirectory">The build's Mids dump directory.</param>
/// <param name="MidsDirectory">The shared-dump directory, or null for the build directory's grandparent.</param>
/// <param name="AttribModPath"><c>AttribMod.json</c>, or null for the default.</param>
/// <param name="MathsPath"><c>Maths.mhd</c>, or null for the default.</param>
internal sealed record ScoreCommand(string BuildDirectory, string? MidsDirectory, string? AttribModPath, string? MathsPath)
{
    /// <summary>Parses the arguments after <c>score</c>.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parsed">The command, when parsing succeeded.</param>
    /// <param name="problem">The reason, when parsing failed.</param>
    /// <returns>True when the arguments parsed.</returns>
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out ScoreCommand? parsed, out string problem)
    {
        string? build = null;
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        parsed = null;
        problem = string.Empty;
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (arg.Split('=')[0] is "--mids-dir" or "--attribmod" or "--maths")
            {
                if (!ValidateArguments.TryTakeValue(args, ref index, out string name, out string value))
                {
                    problem = $"argument {name}: expected one argument";
                    return false;
                }

                options[name] = value;
            }
            else if (arg.StartsWith('-') || build is not null)
            {
                problem = "unrecognized arguments: " + arg;
                return false;
            }
            else
            {
                build = arg;
            }
        }

        if (build is null)
        {
            problem = "the following arguments are required: build_dir";
            return false;
        }

        parsed = new ScoreCommand(build, options.GetValueOrDefault("--mids-dir"), options.GetValueOrDefault("--attribmod"), options.GetValueOrDefault("--maths"));
        return true;
    }

    /// <summary>Computes the build's metrics.</summary>
    /// <returns>The JSON text.</returns>
    /// <exception cref="EngineException">The build is a <c>.mbd</c> file, or a named path does not exist.</exception>
    public string Run()
    {
        if (string.Equals(Path.GetExtension(BuildDirectory), ".mbd", StringComparison.OrdinalIgnoreCase))
        {
            throw new EngineException($"{ErrorCodes.E180}: build '{BuildDirectory}' is a .mbd file, which holds no power effects; give the build's Mids dump directory");
        }

        Require(Directory.Exists(BuildDirectory), "build_dir", BuildDirectory, "directory");
        string mids = MidsDirectory ?? DumpScoring.DefaultMidsDirectory(BuildDirectory);
        Require(Directory.Exists(mids), "--mids-dir", mids, "directory");
        Require(AttribModPath is null || File.Exists(AttribModPath), "--attribmod", AttribModPath, "file");
        Require(MathsPath is null || File.Exists(MathsPath), "--maths", MathsPath, "file");
        ComputedBuild build = DumpScoring.Compute(BuildDirectory, mids, AttribModPath, MathsPath);
        return new JsonObject
        {
            ["data_version"] = DataVersion.Of(mids, BuildDirectory, DumpScoring.AttribModPath(mids, AttribModPath), DumpScoring.MathsPath(mids, MathsPath)),
            ["class_name"] = build.ClassName,
            ["metrics"] = new JsonArray([.. build.Metrics.Select(MetricJson)]),
        }.ToJsonString();
    }

    private static JsonNode MetricJson(MetricReading metric)
    {
        JsonObject view = new() { ["name"] = metric.Name };
        view[metric.Value.IsFlag ? "flag" : "number"] = metric.Value.IsFlag ? metric.Value.IsTrue : metric.Value.Number;
        return view;
    }

    private static void Require(bool exists, string name, string? path, string kind)
    {
        if (!exists)
        {
            throw new EngineException($"{ErrorCodes.E164}: {name} '{path}' does not exist; give an existing {kind}");
        }
    }
}
