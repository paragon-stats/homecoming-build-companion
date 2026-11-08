using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry;

/// <summary>A rule as the registry holds it: its identity and its pure function over the prebuilt context.</summary>
/// <typeparam name="TContext">The context the rule reads.</typeparam>
/// <param name="Info">The rule's identity.</param>
/// <param name="Run">The rule: a pure function from the context to its findings.</param>
internal sealed record RegisteredRule<TContext>(RuleInfo Info, Func<TContext, IEnumerable<Diagnostic>> Run);
