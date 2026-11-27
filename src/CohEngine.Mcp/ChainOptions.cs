using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CohEngine.Mcp;

/// <summary>The <c>chain</c> tool's planning options; a member left out or sent as null keeps the CLI's default.</summary>
/// <param name="Stance">The stance to plan: melee, ranged, hybrid or all; null for all.</param>
/// <param name="Form">The form to plan for, or null for the build's own.</param>
/// <param name="Target">The scenario: single, or a spawn as spawn:N; null for single.</param>
/// <param name="Move">How the character moves between attacks: run, jump or fly; null for run.</param>
/// <param name="Standoff">The ranged standoff in feet, or null for the default.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChainOptions(
    string? Stance = ChainOptions.AllStances,
    string? Form = null,
    string? Target = ChainOptions.SingleTarget,
    string? Move = ChainOptions.Running,
    double? Standoff = null)
{
    private const string AllStances = "all";
    private const string SingleTarget = "single";
    private const string Running = "run";

    /// <summary>Gets the stance to plan: melee, ranged, hybrid or all.</summary>
    [Description("The stance to plan: melee, ranged, hybrid or all.")]
    public string Stance { get; init; } = Stance ?? AllStances;

    /// <summary>Gets the form to plan for, or null for the build's own.</summary>
    [Description("The form to plan for; omit for the build's own.")]
    public string? Form { get; init; } = Form;

    /// <summary>Gets the scenario: single, or a spawn as spawn:N.</summary>
    [Description("The scenario: single, or spawn:N for a spawn of N.")]
    public string Target { get; init; } = Target ?? SingleTarget;

    /// <summary>Gets how the character moves between attacks: run, jump or fly.</summary>
    [Description("How the character moves between attacks: run, jump or fly.")]
    public string Move { get; init; } = Move ?? Running;

    /// <summary>Gets the ranged standoff in feet, or null for the default.</summary>
    [Description("The ranged standoff in feet; omit for the default.")]
    public double? Standoff { get; init; } = Standoff;
}
