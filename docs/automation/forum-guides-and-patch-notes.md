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
- `guides.jsonl` in the output root records every thread looked at; a re-run refetches only
  threads with newer activity. `--pages N` caps the listing walk; `--force` refetches all.

After a run, regenerate `community/INDEX.md` with `scripts/regen-index.ps1` and review the
summaries. Replies are not captured; a guide whose value is in its thread still needs the browser route.

## Patch timeline

```bash
python -m scripts.forum_scrape.patch_notes --out data/patches
```

Reads the Release Notes application (not the forum's beta patch-note threads, which re-post
every release candidate in full) into one dated JSON file per entry; the shape is in
[`data/patches/README.md`](../../data/patches/README.md). Re-runs write only new entries.

## Sections

| Content | Where |
| --- | --- |
| Mechanics and general guides | `/forum/44-guides/`, `/forum/43-archetypes/` |
| Archetype guides and build threads | `/forum/13-blaster/` through `/forum/24-arachnos-soldier-amp-widow/` |
| Live patch notes | `/patch-notes/` (Issue 27 page 6 onward) |
