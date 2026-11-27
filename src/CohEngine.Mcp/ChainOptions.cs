using System.ComponentModel;

namespace CohEngine.Mcp;

/// <summary>The <c>chain</c> tool's planning options; a member left out keeps the CLI's default.</summary>
/// <param name="Stance">The stance to plan: melee, ranged, hybrid or all.</param>
/// <param name="Form">The form to plan for, or null for the build's own.</param>
/// <param name="Target">The scenario: single, or a spawn as spawn:N.</param>
/// <param name="Move">How the character moves between attacks: run, jump or fly.</param>
/// <param name="Standoff">The ranged standoff in feet, or null for the default.</param>
public sealed record ChainOptions(
    [property: Description("The stance to plan: melee, ranged, hybrid or all.")] string Stance = "all",
    [property: Description("The form to plan for; omit for the build's own.")] string? Form = null,
    [property: Description("The scenario: single, or spawn:N for a spawn of N.")] string Target = "single",
    [property: Description("How the character moves between attacks: run, jump or fly.")] string Move = "run",
    [property: Description("The ranged standoff in feet; omit for the default.")] double? Standoff = null);
