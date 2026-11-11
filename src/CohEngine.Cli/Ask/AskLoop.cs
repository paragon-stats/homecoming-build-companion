using CohEngine.Core.Diagnostics;

using Microsoft.Extensions.AI;

namespace CohEngine.Cli.Ask;

/// <summary>Asks the model, runs the tools it calls and sends the results back until it answers in text.</summary>
internal static class AskLoop
{
    private const string SystemPrompt = "You answer questions about one Homecoming build. Call the tools for every number and rule; do not guess. Answer briefly in plain text.";

    public static async Task<AskReply> RunAsync(IChatClient client, IReadOnlyList<AIFunction> tools, string question, int maxRounds, CancellationToken cancellationToken)
    {
        List<ChatMessage> messages = [new(ChatRole.System, SystemPrompt), new(ChatRole.User, question)];
        ChatOptions options = new() { Tools = [.. tools], MaxOutputTokens = AskCommand.MaxOutputTokens };
        for (int round = 0; round < maxRounds; round++)
        {
            ChatResponse response = await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            messages.AddRange(response.Messages);
            List<FunctionCallContent> calls = [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()];
            if (calls.Count == 0)
            {
                return string.IsNullOrWhiteSpace(response.Text)
                    ? throw new EngineException($"{AskErrorCodes.ProviderReply}: the model returned no text; try again or use another model")
                    : new AskReply(response.Text, response.FinishReason == ChatFinishReason.Length);
            }

            List<AIContent> results = [];
            foreach (FunctionCallContent call in calls)
            {
                results.Add(new FunctionResultContent(call.CallId, await RunToolAsync(tools, call, cancellationToken).ConfigureAwait(false)));
            }

            messages.Add(new ChatMessage(ChatRole.Tool, results));
        }

        throw new EngineException($"{AskErrorCodes.RoundLimit}: the model was still calling tools after {maxRounds} rounds; ask a narrower question or use a stronger model");
    }

    private static async Task<object?> RunToolAsync(IReadOnlyList<AIFunction> tools, FunctionCallContent call, CancellationToken cancellationToken)
    {
        if (call.Exception is not null)
        {
            return $"error: the arguments for {call.Name} could not be read ({call.Exception.Message}); the tool was not run";
        }

        AIFunction? tool = tools.FirstOrDefault(candidate => string.Equals(candidate.Name, call.Name, StringComparison.Ordinal));
        return tool is null
            ? $"error: no tool named {call.Name}"
            : await tool.InvokeAsync(new AIFunctionArguments(call.Arguments, StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
    }
}
