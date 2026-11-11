namespace CohEngine.Cli.Ask;

/// <summary>What the model finally said, and whether the output limit cut it short.</summary>
/// <param name="Text">The final reply text.</param>
/// <param name="Truncated">True when the reply stopped at the output limit.</param>
internal sealed record AskReply(string Text, bool Truncated);
