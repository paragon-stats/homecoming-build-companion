using System.Text.Json;

using CohEngine.Ai;
using CohEngine.Ai.Anthropic;
using CohEngine.Core.BuildFile;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;
using CohEngine.Mcp;

using Microsoft.Extensions.AI;

namespace CohEngine.Cli.Ask;

/// <summary>The <c>coh-engine ask</c> command: a chat model answers a question about a build by calling the engine's tools.</summary>
public static class AskCommand
{
    /// <summary>The most model replies one question may take; a tool call and its answer count as one round.</summary>
    public const int MaxRounds = 8;

    /// <summary>The output limit per reply; the Anthropic client's default of 1024 cuts a long answer short.</summary>
    public const int MaxOutputTokens = 4096;

    private const string Usage = "usage: coh-engine ask --build <dump-dir|file.mbd> --model NAME [--provider anthropic|openai|mistral|local] [--base-url URL] [--api-key-env NAME] [--] <question>";

    /// <summary>Runs the command.</summary>
    /// <param name="args">The arguments after <c>ask</c>.</param>
    /// <param name="output">Where the answer goes.</param>
    /// <param name="error">Where a usage or run error goes.</param>
    /// <param name="mbdSource">Resolves a <c>.mbd</c> build; null reports the <c>.mbd</c> form as unavailable.</param>
    /// <param name="handler">Replaces the network layer; tests pass a fake, the program passes null.</param>
    /// <param name="cancellationToken">Stops the command.</param>
    /// <returns>0 with the answer on <paramref name="output"/>, 1 on a failed run, 2 on a bad command line.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, IMbdBuildSource? mbdSource, HttpMessageHandler? handler, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (!AskArguments.TryParse(args, out AskArguments? parsed, out string problem))
        {
            error.WriteLine(Usage);
            error.WriteLine("coh-engine ask: error: " + problem);
            return 2;
        }

        string message;
        try
        {
            IReadOnlyList<AIFunction> tools = LoadTools(parsed.BuildPath, mbdSource);
            using IChatClient client = ChatClientFactory.Create(new ChatProviderOptions(parsed.Provider, parsed.Preset, parsed.Model, parsed.BaseUrl, parsed.ApiKeyEnvironmentVariable, handler));
            AskReply reply = await AskLoop.RunAsync(client, tools, parsed.Question, MaxRounds, cancellationToken).ConfigureAwait(false);
            output.WriteLine(reply.Text);
            if (!reply.Truncated)
            {
                return 0;
            }

            message = $"{AskErrorCodes.ReplyCutOff}: the reply stopped at the output limit of {AskCommand.MaxOutputTokens} tokens, so the answer above is incomplete; ask a narrower question";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (OperationCanceledException)
        {
            message = $"{AskErrorCodes.ProviderReply}: the provider did not answer in time; check the base URL or try again";
        }
        catch (Exception exception) when (exception is EngineException or InvalidOperationException or ChatProviderException or AnthropicApiException)
        {
            // Each of these carries its own E-code message, written without the key or the response body.
            message = exception.Message;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            message = $"{AskErrorCodes.ProviderReply}: the provider did not give a usable reply ({exception.GetType().Name}); check the base URL and the model name";
        }

        error.WriteLine("error: " + message);
        return 1;
    }

    private static List<AIFunction> LoadTools(string buildPath, IMbdBuildSource? mbdSource)
    {
        EngineToolSpec rules = EngineToolCatalog.DumpTools.First(tool => string.Equals(tool.Name, "rules", StringComparison.Ordinal));
        if (string.Equals(Path.GetExtension(buildPath), ".mbd", StringComparison.OrdinalIgnoreCase))
        {
            // A .mbd holds no dump, so only the rules and the validation of the file itself are on offer.
            string validation = Load(buildPath, () => DiagnosticFormatter.FormatJson(ValidateCommand.RunMbd(mbdSource ?? throw new EngineException(".mbd builds are not available in this build"), buildPath, mbdSource.DefaultMidsDirectory)));
            return [Wrap(rules, buildPath), new EngineAiFunction("validate", "Validate the build file against the legality, hard-limit and format rules.", """{"type":"object","properties":{}}""", _ => validation)];
        }

        // The loader validate uses: a directory that is not a dump fails here, before any model call.
        Load(buildPath, () => ValidateCommand.RunDump(buildPath, DumpScoring.DefaultMidsDirectory(buildPath)));
        return [.. EngineToolCatalog.DumpTools.Select(tool => Wrap(tool, buildPath))];
    }

    private static T Load<T>(string path, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is EngineException or IOException or UnauthorizedAccessException or InvalidDataException or JsonException or KeyNotFoundException or ArgumentException)
        {
            throw new EngineException($"{AskErrorCodes.BuildUnreadable}: cannot read the build '{path}' ({exception.Message}); give a Mids dump directory or a readable .mbd", exception);
        }
    }

    private static EngineAiFunction Wrap(EngineToolSpec spec, string buildPath)
    {
        return new EngineAiFunction(spec.Name, spec.Description, spec.SchemaJson, arguments => EngineToolCatalog.Call(spec, buildPath, arguments));
    }
}
