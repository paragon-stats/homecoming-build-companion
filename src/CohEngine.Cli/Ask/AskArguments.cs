using System.Diagnostics.CodeAnalysis;

using CohEngine.Ai;

namespace CohEngine.Cli.Ask;

/// <summary>The parsed <c>ask</c> command line.</summary>
/// <param name="Question">The question to answer.</param>
/// <param name="BuildPath">The Mids dump directory or <c>.mbd</c> file.</param>
/// <param name="Provider">The wire protocol.</param>
/// <param name="Preset">The server defaults for the OpenAI-compatible protocol.</param>
/// <param name="Model">The model name.</param>
/// <param name="BaseUrl">Replaces the provider's base URL, or null.</param>
/// <param name="ApiKeyEnvironmentVariable">Replaces the provider's key variable, or null; never the key itself.</param>
internal sealed record AskArguments(string Question, string BuildPath, ChatProvider Provider, ChatPreset Preset, string Model, string? BaseUrl, string? ApiKeyEnvironmentVariable)
{
    private static readonly string[] Options = ["--build", "--provider", "--model", "--base-url", "--api-key-env"];

    /// <summary>Parses the arguments after <c>ask</c>, accepting <c>--opt value</c> and <c>--opt=value</c>, and <c>--</c> before a question that starts with a dash.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="parsed">The parsed arguments, when parsing succeeded.</param>
    /// <param name="problem">The reason, when parsing failed.</param>
    /// <returns>True when the arguments parsed.</returns>
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out AskArguments? parsed, out string problem)
    {
        parsed = null;
        if (!TryScan(args, out string? question, out Dictionary<string, string> values, out problem))
        {
            return false;
        }

        string provider = values.GetValueOrDefault("--provider", "anthropic");
        (ChatProvider Wire, ChatPreset Preset)? choice = provider switch
        {
            "anthropic" => (ChatProvider.Anthropic, ChatPreset.OpenAi),
            "openai" => (ChatProvider.OpenAiCompatible, ChatPreset.OpenAi),
            "mistral" => (ChatProvider.OpenAiCompatible, ChatPreset.Mistral),
            "local" => (ChatProvider.OpenAiCompatible, ChatPreset.Local),
            _ => null,
        };
        problem = (question, choice, values.GetValueOrDefault("--build"), values.GetValueOrDefault("--model")) switch
        {
            (null or "", _, _, _) => "the following arguments are required: question",
            (_, null, _, _) => $"argument --provider: invalid choice: '{provider}' (choose from 'anthropic', 'openai', 'mistral', 'local')",
            (_, _, null, _) => "the following arguments are required: --build",
            (_, _, _, null) => "the following arguments are required: --model",
            _ => string.Empty,
        };
        if (problem.Length != 0)
        {
            return false;
        }

        parsed = new AskArguments(question!, values["--build"], choice!.Value.Wire, choice.Value.Preset, values["--model"], values.GetValueOrDefault("--base-url"), values.GetValueOrDefault("--api-key-env"));
        return true;
    }

    /// <summary>Splits the arguments into the question and the option values, and checks each value is usable.</summary>
    private static bool TryScan(IReadOnlyList<string> args, out string? question, out Dictionary<string, string> values, out string problem)
    {
        question = null;
        values = new(StringComparer.Ordinal);
        bool literal = false;
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (!literal && string.Equals(arg, "--", StringComparison.Ordinal))
            {
                literal = true;
                continue;
            }

            string name = literal ? string.Empty : arg.Split('=')[0];
            if (!Options.Contains(name, StringComparer.Ordinal))
            {
                bool looksLikeOption = !literal && arg.StartsWith('-');
                if (looksLikeOption || question is not null)
                {
                    // An unknown option is named without its value, which could be a key.
                    problem = "unrecognized arguments: " + (looksLikeOption ? arg.Split('=')[0] : arg);
                    return false;
                }

                question = arg;
                continue;
            }

            if (arg.Contains('=', StringComparison.Ordinal))
            {
                values[name] = arg[(name.Length + 1)..];
            }
            else if (++index < args.Count)
            {
                values[name] = args[index];
            }
            else
            {
                problem = $"argument {name}: expected one argument";
                return false;
            }
        }

        problem = string.Empty;
        if (values.FirstOrDefault(option => option.Value.Length == 0) is { Key: { } empty })
        {
            problem = $"argument {empty}: expected a non-empty value";
        }
        else if (values.TryGetValue("--api-key-env", out string? keyVariable) && !IsEnvironmentName(keyVariable))
        {
            // The name is echoed in the missing-key message, so a key passed here by mistake must not get that far.
            problem = "argument --api-key-env: takes the name of an environment variable, not a key";
        }

        return problem.Length == 0;
    }

    private static bool IsEnvironmentName(string name)
    {
        return (char.IsAsciiLetter(name[0]) || name[0] == '_') && name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }
}
