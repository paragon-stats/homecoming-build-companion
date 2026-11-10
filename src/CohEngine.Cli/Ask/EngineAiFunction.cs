using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace CohEngine.Cli.Ask;

/// <summary>An engine tool offered to the model. Written by hand because reflection-built functions are not Native AOT safe.</summary>
internal sealed class EngineAiFunction : AIFunction
{
    private readonly Func<IReadOnlyDictionary<string, string>, string> run;

    public EngineAiFunction(string name, string description, string schemaJson, Func<IReadOnlyDictionary<string, string>, string> run)
    {
        Name = name;
        Description = description;
        using JsonDocument schema = JsonDocument.Parse(schemaJson);
        JsonSchema = schema.RootElement.Clone();
        this.run = run;
    }

    public override string Name { get; }

    public override string Description { get; }

    public override JsonElement JsonSchema { get; }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        Dictionary<string, string> values = arguments
            .Where(argument => argument.Value is not null)
            .ToDictionary(argument => argument.Key, argument => Convert.ToString(argument.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.Ordinal);
        return ValueTask.FromResult<object?>(run(values));
    }
}
