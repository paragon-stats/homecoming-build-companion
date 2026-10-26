# Capturing forum guides and the patch timeline

Two operator-run commands beside the build scraper. Both pace requests through the scraper's
own fetch layer (robots.txt, one global delay, the declared user agent) and resume from a ledger.

## Guides

```bash
python -m scripts.forum_scrape.guides --forum https://forums.homecomingservers.com/forum/44-guides/ --out community
```

Walks the listing, fetches each thread's opening post, keeps the ones shaped like a guide, and
writes each as `community/<folder>/<slug>.md` in the manual capture format
([`community/CAPTURE.md`](../../community/CAPTURE.md)): the same frontmatter, a `## Summary`
stub to fill in on review, and the opening post verbatim with tables kept as rows.

- A guide is an opening post of 500 or more words whose bold labels, list items and tables
  score 6 or more, with at least 2 build-vocabulary words (power, slot, recharge, proc,
  defense, incarnate, ...) per 100 words. The thresholds come from a probe of 1,430 opening
  posts: guides ran a median 1,800 words with 30 bold runs and 3.3 vocabulary hits per 100
  words; chatter a median 100 words with no structure; bind, badge, font and install guides
  under 1 hit per 100 words. `guides.jsonl` records the reason for every thread not captured.
- Placement: a thread from an archetype section, or whose title names an archetype, goes to
  `archetypes/<at>/`; a title naming a canonical powerset goes to `powersets/`; the rest to
  `mechanics/`. The folder's tag leads `topic_tags`, followed by the topics the text matches.
- `trust` is written as `single-author-opinion`; raising it is a review decision.
- A request, showcase or roleplay thread that passes the gates is captured with a `discussion`
  tag after the folder tag: kept for the information in it, never presented as a guide.
- A concept, theme, costume, power-colour or roleplay thread goes to `theme/` with `theme-rp`
  leading, the reference pool for the Theme/RP build type in
  [`docs/build-types-and-goals.md`](../build-types-and-goals.md). It is exempt from the
  build-vocabulary gate: its value is the look and the idea, not the numbers.
- Every capture carries `content_hash`, the digest of the rendered opening post. A re-run
  refetches a thread only when the listing shows newer activity, and rewrites the file only
  when that hash changed. A rewrite replaces the verbatim section and the source-derived
  frontmatter; the reviewer's `trust`, `contradicts_data`, Summary and any added sections
  survive, and the replaced hash is kept as `superseded_hash`. A frozen source (an archive)
  therefore costs its listing walk per run and nothing more.
- `guides.jsonl` in the output root records every thread looked at and why it was or was not
  captured, flushed per thread, so a stopped run resumes where it ended. Threads that could not
  be fetched or parsed are retried on the next run; ten such threads in a row stop the run
  (`E63`). The run ends with a count by outcome and exits 2 (`E62`) when a retry run is wanted,
  0 otherwise. `--pages N` caps the listing walk; `--force` refetches every thread.

After a run, regenerate `community/INDEX.md` with `scripts/regen-index.ps1` and review the
summaries. Replies are not captured; a guide whose value is in its thread still needs the browser route.

### The pre-sunset forum archive

The 2004 to 2012 official boards survive in full at `https://forumarchive.cityofheroes.dev/`, an
Invision import with the original sections (`index.php?/forum/44-player-guides/`, one section per
archetype). The same command reads it; the board is named with `--base`, which also sets the
capture's `source`, and the era is kept apart by its own output root and a tag:

```bash
python -m scripts.forum_scrape.guides --base https://forumarchive.cityofheroes.dev \
  --forum "https://forumarchive.cityofheroes.dev/index.php?/forum/44-player-guides/" \
  --tag pre-sunset --out community/pre-sunset
```

- The archive publishes no `robots.txt` and no terms; its operator is unnamed. A full crawl
  waits on one contact through the site's contact page, recorded here with its outcome.
- The content is frozen, so a second run costs the listing walk and writes nothing: no thread
  shows new activity and no opening post's hash changes.
- Old guides explain how a set plays; their numbers are dated by the patch timeline below, which
  is why `date_posted` carries the original post date, not the import date.
- The Wayback Machine also holds `boards.cityofheroes.com` (2009 to 2012), under session-id URLs
  with missing deep pages and heavy rate limiting: a fallback for a single missing thread, not a
  crawl target.

## Patch timeline

```bash
python -m scripts.forum_scrape.patch_notes --out data/patches
```

Reads three sources into one dated JSON file per entry, the shape and coverage in
[`data/patches/README.md`](../../data/patches/README.md): the Release Notes application
(April 2023 onward), the forum's beta and staging patch-note threads (2019 to 2023, the final
release candidate of each Issue/Page series plus interim patches, skipped wherever the live
application covers the same series or date), and the Paragon Wiki Archive's dated pages
(2004 to 2012). `--source` selects one; the default is all three. Re-runs write only entries
not yet in the index, so the two archive sources cost nothing after their first run.

## Sections

| Content | Where |
| --- | --- |
| Mechanics and general guides | `/forum/44-guides/`, `/forum/43-archetypes/` |
| Archetype guides and build threads | `/forum/13-blaster/` through `/forum/24-arachnos-soldier-amp-widow/` |
| Live patch notes | `/patch-notes/` (Issue 27 page 6 onward) |
| Beta patch notes, 2019 to 2023 | `/forum/73-open-beta-patch-notes/`, `/forum/100-staging-patch-notes/` |
| Pre-sunset patch notes, 2004 to 2012 | `https://archive.paragonwiki.com/wiki/Patch_Notes` |
