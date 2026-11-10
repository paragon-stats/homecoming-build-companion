using System.Diagnostics.CodeAnalysis;

/// <summary>Program.cs is a dispatch shim over tested commands; the published-binary check in CI is what runs it.</summary>
[ExcludeFromCodeCoverage(Justification = "Top-level dispatch shim; each command it calls is covered.")]
internal sealed partial class Program;
