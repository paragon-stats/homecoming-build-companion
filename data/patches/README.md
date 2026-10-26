# Patch timeline

One JSON file per patch-notes entry, written by `python -m scripts.forum_scrape.patch_notes`, from three sources that together span the game's history.
A claim in a `community/` capture is dated by its `date_posted`; any change to the same power or powerset with a later `published` date supersedes or qualifies it.

## Sources and coverage

| `source` | Where | Span | Entries |
| --- | --- | --- | --- |
| `release-notes` | Homecoming's Release Notes application, `https://forums.homecomingservers.com/patch-notes/`, browsed as Issue > Page categories | Issue 27 page 6 (April 2023) onward | one per published update |
| `forum-beta` | The forum's beta and staging patch-note sections, `/forum/73-open-beta-patch-notes/` and `/forum/100-staging-patch-notes/` | Homecoming's Issue 26 page 3 (2019) to Issue 27 page 5 (2022) | the final release candidate of each Issue/Page series, plus each interim patch thread |
| `paragonwiki` | The Paragon Wiki Archive's dated pages, `https://archive.paragonwiki.com/wiki/Patch_Notes/YYYY-MM-DD` | Launch (May 2004) to the 2012 sunset | one per dated page |

The live application wins: a beta series it also covers, or an interim beta thread dated inside its coverage, is not written.
Of a labelled Issue/Page series, only the last candidate in each beta section is fetched; a candidate whose title carries no series label counts as an interim patch.
Within the beta and wiki sources a change line is dated by its first appearance.
Staging reposts the beta notes days later, each beta thread restates the ones before it, and the wiki republished pages with corrections.
A later entry therefore keeps only the lines no earlier entry of its source recorded.
An entry left with no lines is indexed with an empty `file` and a `repeat_of` naming the file that holds them.
Both archive sources are frozen, so a re-run writes nothing new from them; it costs only the listing and index fetches.

The one span no source covers is the 2012 sunset to the 2019 relaunch.
Between the last beta series (Issue 27 page 5, October 2022) and the application's first entry (April 2023) the only beta cycle is Issue 27 page 6, which the application's entries cover.

## File shape

`<published-date>-<slug>.json`, where the slug is the application entry's (`october-20-update-r48`), `beta-` plus the thread's, or `paragonwiki-` plus the page date:

```json
{
  "title": "October 20 Update",
  "url": "https://forums.homecomingservers.com/patch-notes/issue-28/page-4/october-20-update-r48/",
  "published": "2026-10-20T14:05:01Z",
  "issue": "issue-28",
  "page": "page-4",
  "revision": 48,
  "source": "release-notes",
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

`archetype`, `powerset` and `power` are filled from an `AT > Set > Power -` path at the head of a change line.
A beta thread places its powerset and power as headings above its lines, and those fill the same fields; other lines keep the category and subsection only.
Wiki pages carry `issue` from their title when it names one, and no `page`; `revision` is 0 outside the application.
`index.jsonl` lists every written entry by URL, date, revision, source, issue, page and file; a re-run writes only entries whose URL is not yet in it.

## Use

Corrections that follow from a patch go in [`data/diff/current_overrides.json`](../diff/current_overrides.json), citing the entry file.
