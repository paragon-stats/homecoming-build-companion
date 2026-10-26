# Patch timeline

One JSON file per live patch-notes entry, read from Homecoming's Release Notes application
(`https://forums.homecomingservers.com/patch-notes/`) by `python -m scripts.forum_scrape.patch_notes`.
The application holds Issue 27 page 6 onward; earlier patches exist only as forum threads in the beta sections.

## File shape

`<published-date>-<entry-slug>-r<revision>.json`:

```json
{
  "title": "October 20 Update",
  "url": "https://forums.homecomingservers.com/patch-notes/issue-28/page-4/october-20-update-r48/",
  "published": "2026-10-20T14:05:01Z",
  "issue": "issue-28",
  "page": "page-4",
  "revision": 48,
  "changes": [
    {
      "category": "Powers",
      "subsection": "Adjustments",
      "archetype": "All",
      "powerset": "Sonic Resonance",
      "power": "Clarity",
      "text": "Increased splash mez protection from scale 5 to 15 (same as Clear Mind)."
    }
  ]
}
```

`archetype`, `powerset` and `power` are filled when the change line starts with the notes' `AT > Set > Power -` path; other lines keep the category and subsection only.
`index.jsonl` lists every written entry by URL, date and revision; a re-run writes only entries not yet in it.

## Use

A claim in a `community/` capture is dated by its `date_posted`; any change to the same power or powerset with a later `published` date supersedes or qualifies it.
Corrections that follow from a patch go in [`data/diff/current_overrides.json`](../diff/current_overrides.json), citing the entry file.
