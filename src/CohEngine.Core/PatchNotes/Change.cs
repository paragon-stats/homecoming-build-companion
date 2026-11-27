using CohEngine.Core.Chain;

namespace CohEngine.Core.PatchNotes;

/// <summary>One change line, with the headings it sat under and the power path if it named one.</summary>
/// <param name="Category">The category heading.</param>
/// <param name="Subsection">The subsection heading.</param>
/// <param name="Archetype">The archetype part of a power path.</param>
/// <param name="Powerset">The powerset.</param>
/// <param name="Power">The power.</param>
/// <param name="Text">The change.</param>
internal sealed record Change(string Category, string Subsection, string Archetype, string Powerset, string Power, string Text)
{
    /// <summary>The record's JSON object, keys in field order.</summary>
    /// <returns>The object.</returns>
    public PyObject ToObject()
    {
        return new PyObject
        {
            { "category", Category },
            { "subsection", Subsection },
            { "archetype", Archetype },
            { "powerset", Powerset },
            { "power", Power },
            { "text", Text },
        };
    }
}
