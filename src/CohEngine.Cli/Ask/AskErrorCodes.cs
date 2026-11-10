namespace CohEngine.Cli.Ask;

/// <summary>Error codes E170 to E179 are reserved for the <c>ask</c> command; the chat clients keep E130 to E149.</summary>
public static class AskErrorCodes
{
    /// <summary>The build path is not a readable Mids dump directory or <c>.mbd</c> file.</summary>
    public const string BuildUnreadable = "E170";

    /// <summary>The model was still calling tools when the round limit was reached.</summary>
    public const string RoundLimit = "E171";

    /// <summary>The provider could not be reached, or its reply could not be read.</summary>
    public const string ProviderReply = "E172";
}
