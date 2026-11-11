namespace CohEngine.Mcp;

/// <summary>One engine tool in a form other front ends (the <c>ask</c> command) can offer a model.</summary>
/// <param name="Name">The tool name, as the MCP server exposes it.</param>
/// <param name="Description">What the tool does, in a sentence for the model.</param>
/// <param name="SchemaJson">The JSON schema of the arguments the model supplies; the build path is not among them.</param>
/// <param name="Run">Runs the tool on a build dump directory with the model's string arguments and returns the result as JSON.</param>
public sealed record EngineToolSpec(string Name, string Description, string SchemaJson, Func<string, IReadOnlyDictionary<string, string>, string> Run);
