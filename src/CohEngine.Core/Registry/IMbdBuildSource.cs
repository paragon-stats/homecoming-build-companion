namespace CohEngine.Core.Registry;

/// <summary>
/// Where a bare <c>.mbd</c> build file's powers, slots and format facts come from. The reader that resolves a file
/// through the powers table is ported separately; <c>validate</c> takes this seam so it can fold the same registries
/// over what that reader returns.
/// </summary>
public interface IMbdBuildSource
{
    /// <summary>Gets the shared-dump directory used when <c>--mids-dir</c> is not given (the repository's <c>tests/fixtures/mids</c> in the Python engine).</summary>
    string DefaultMidsDirectory { get; }

    /// <summary>Resolves a <c>.mbd</c> file against the shared dumps.</summary>
    /// <param name="path">The <c>.mbd</c> file.</param>
    /// <param name="midsDirectory">The shared-dump directory, which also holds <c>powers.json</c> and <c>archetypes.json</c>.</param>
    /// <returns>The resolved build.</returns>
    MbdBuild Load(string path, string midsDirectory);
}
