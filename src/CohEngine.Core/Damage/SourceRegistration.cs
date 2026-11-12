using CohEngine.Core.Chain;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Damage;

/// <summary>A registered damage source and whether it fills the remainder of the timeline.</summary>
/// <param name="Run">The source.</param>
/// <param name="FillsRemainder">Whether it runs last.</param>
public sealed record SourceRegistration(Func<DamageContext, SourceResult?> Run, bool FillsRemainder);
