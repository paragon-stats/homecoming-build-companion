using CohEngine.Core.Chain;

namespace CohEngine.Core.PatchNotes;

/// <summary>One patch-notes entry: where it sits in the Issue &gt; Page tree, when, from which source, and what changed.</summary>
internal sealed class PatchEntry
{
    /// <summary>Gets or sets the title.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the URL.</summary>
    public required string Url { get; set; }

    /// <summary>Gets or sets the publish time or date.</summary>
    public required string Published { get; set; }

    /// <summary>Gets or sets the issue, such as <c>issue-28</c>.</summary>
    public required string Issue { get; set; }

    /// <summary>Gets or sets the page, such as <c>page-4</c>.</summary>
    public required string Page { get; set; }

    /// <summary>Gets or sets the application entry's revision; 0 elsewhere.</summary>
    public long Revision { get; set; }

    /// <summary>Gets or sets the source.</summary>
    public string Source { get; set; } = PatchParser.ReleaseNotes;

    /// <summary>Gets or sets the change lines.</summary>
    public List<Change> Changes { get; set; } = [];

    /// <summary>The record's JSON object, keys in field order.</summary>
    /// <returns>The object.</returns>
    public PyObject ToObject()
    {
        return new PyObject
        {
            { "title", Title },
            { "url", Url },
            { "published", Published },
            { "issue", Issue },
            { "page", Page },
            { "revision", Revision },
            { "source", Source },
            { "changes", Changes.Select(change => (object?)change.ToObject()).ToList() },
        };
    }
}
