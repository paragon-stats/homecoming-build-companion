using CohEngine.Core.Diagnostics;

using Microsoft.Extensions.AI;

namespace CohEngine.Cli.Ask;

/// <summary>Asks the model, runs the tools it calls and sends the results back until it answers in text.</summary>
internal static class AskLoop
{
    private const string SystemPrompt = "You answer questions about one Homecoming build. Call the tools for every number and rule; do not guess. Answer briefly in plain text.";

    public static async Task<string> RunAsync(IChatClient client, IReadOnlyList<AIFunction> tools, string question, int maxRounds, CancellationToken cancellationToken)
    {
        List<ChatMessage> messages = [new(ChatRole.System, SystemPrompt), new(ChatRole.User, question)];
        ChatOptions options = new() { Tools = [.. tools] };
        for (int round = 0; round < maxRounds; round++)
        {
            ChatResponse response = await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            messages.AddRange(response.Messages);
            List<FunctionCallContent> calls = [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()];
            if (calls.Count == 0)
            {
                return response.Text;
            }

            List<AIContent> results = [];
            foreach (FunctionCallContent call in calls)
            {
                AIFunction? tool = tools.FirstOrDefault(candidate => string.Equals(candidate.Name, call.Name, StringComparison.Ordinal));
                object? result = tool is null
                    ? $"error: no tool named {call.Name}"
                    : await tool.InvokeAsync(new AIFunctionArguments(call.Arguments, StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
                results.Add(new FunctionResultContent(call.CallId, result));
            }

            messages.Add(new ChatMessage(ChatRole.Tool, results));
        }

        throw new EngineException($"{AskErrorCodes.RoundLimit}: the model was still calling tools after {maxRounds} rounds; ask a narrower question or use a stronger model");
    }
}
