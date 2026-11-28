# Capturing forum guides and the patch timeline

The forum guide captures and the pre-sunset forum archive crawl are made by the forum-scrape tooling, which lives with the captures in
`paragon-stats/homecoming-source-material`; its documentation is there.
This repository keeps the patch timeline command, which paces its requests (robots.txt, one global delay, the declared user agent) and resumes from its index.

## Patch timeline

```bash
coh-engine patch-notes --out data/patches
```

Reads three sources into one dated JSON file per entry, the shape and coverage in
[`data/patches/README.md`](../../data/patches/README.md): the Release Notes application
(April 2023 onward), the forum's beta and staging patch-note threads (2019 to 2023, the final
release candidate of each Issue/Page series plus interim patches, skipped wherever the live
application covers the same series or date), and the Paragon Wiki Archive's dated pages
(2004 to 2012). Within those two sources a change line is dated by its first appearance, so a
later entry keeps only what no earlier one recorded and a word-for-word repost is indexed
without a file. `--source` selects one; the default is all three. Re-runs write only entries
not yet in the index, so the two archive sources cost nothing after their first run.

From a source checkout, `dotnet run --project src/CohEngine.Cli -- patch-notes ...` runs the same command.
Its errors carry their own numbers: a failed fetch is `E200`, a refused or unreadable
`robots.txt` `E201` or `E202`, an unreadable category browser or wiki index `E203` or `E204`, a missing beta
listing page `E205`, a canonical tree with no powerset names `E207`, which stops the run before anything is fetched or written, and a source not read completely `E208`.
A source not read completely keeps its records and index rows as they are, even under `--reparse`, and the run exits 1.
A listing that answers but names no link, such as a maintenance page, is `E227` and leaves its source not read completely.
A beta listing that declares more than 100 pages is read to page 100 only (`E226`), and its source counts as not read completely.
So does a re-parse that yields no records for a source whose records the folder holds.
An entry whose date or URL would not make a plain file name inside the output folder is skipped with `E209`, and the run exits 1.
Under `--reparse`, an old index row whose file name is not a plain name inside the output folder is `E209`, and the run exits 1.
No record of its source is written or removed, and its index rows are kept.
A link or a redirect to a host other than the forum or the wiki is not fetched (`E222`).
A page that takes a parsing pattern past its time limit stops its source with `E223`; that source counts as not read completely.
A page robots.txt disallows is skipped with `E220`, and every page of a site whose robots.txt was refused or unreadable with `E221`.
A file under the output folder or its cache that cannot be read or written stops the run with `E224`; a record or index file another process holds is tried three times first, and the message names it.
An index line or a record file that is not what the command writes (not JSON, the wrong shape, or not UTF-8) stops the run with `E225`, naming the file.
Every coded error line goes to standard error and opens with `error:`.
An entry with no change lines is not an error: it is recorded with no changes, and an information line on standard output reports it.
The line reads `info: E206: no change lines parsed from <url>; the entry is recorded with no changes`.
Every error code makes the run exit 1; `E206` leaves the exit code as it is.

Every entry page fetched is kept in `data/patches/.cache/` (gitignored; `--cache` moves it).
After a parser change, rebuild the records from it instead of deleting them:

```bash
coh-engine patch-notes --source paragonwiki --reparse
```

The sources are regenerated in a scratch folder, fetching only pages the cache lacks; then only
the records whose JSON differs are rewritten, records no page produces any more are removed, and
the run prints, per source, how many records changed, stayed the same, were removed and were
kept. A record is kept when its page now parses to no lines but the file holds some: those were
filled in by hand for a post written as paragraphs. A last pass dates each line by first
appearance across the source again, since the regenerated records did not see the kept ones.

## Sections

| Content | Where |
| --- | --- |
| Mechanics and general guides | `/forum/44-guides/`, `/forum/43-archetypes/` |
| Archetype guides and build threads | `/forum/13-blaster/` through `/forum/24-arachnos-soldier-amp-widow/` |
| Live patch notes | `/patch-notes/` (Issue 27 page 6 onward) |
| Beta patch notes, 2019 to 2023 | `/forum/73-open-beta-patch-notes/`, `/forum/100-staging-patch-notes/` |
| Pre-sunset patch notes, 2004 to 2012 | `https://archive.paragonwiki.com/wiki/Patch_Notes` |
