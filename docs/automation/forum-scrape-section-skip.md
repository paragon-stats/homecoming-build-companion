# Ruling out an unchanged forum section

How [`scripts/forum_scrape`](../../scripts/forum_scrape/) decides it can leave a whole forum
section alone, what that rests on, and what it measured when it was tested against the live
forum. Written down because the shortcut trades requests for an assumption about how Invision
orders a listing, and an assumption that goes unrecorded is one nobody re-checks.

## The problem

A re-crawl has to enumerate a section before it can conclude that nothing in it changed. Tanker
runs to 84 listing pages, Brute to 83. Paying 83 requests to discover that all 2,061 threads
behind them are untouched is most of the cost of the crawl, for none of its value.

## What the shortcut rests on

Invision orders a forum listing by last post, newest first. If that holds, the most recently
touched thread in a section is always on page one, so page one alone answers "has anything
happened here since last time?" for every page behind it.

That was checked against the crawl's own output rather than taken on trust. Each run's
`topics.jsonl` records threads in listing order with the last-post time the listing showed, so
the ordering is directly observable in data already on disk:

| Section | Threads with a recorded time | Rows newer than the row above |
| --- | --- | --- |
| 17-tanker | 1,247 | 0 |
| 22-brute | 1,786 | 0 |

Zero exceptions in 3,033 threads. The listing is strictly newest-first.

## The decision

A section runs through the same `is_covered` that decides a single thread — same formats asked
for, nothing posted since. One function serves both, so section skipping cannot drift away from
thread skipping as either changes.

Two things withdraw the shortcut, both erring towards looking:

- **A page-one row with no last-post time.** The section's newest could be that row, so its age
    cannot be ruled on.
- **A prior walk stopped short by `--pages`.** It records no `last_activity` at all, because its
    newest thread says nothing about the pages it never enumerated.

An unknown on either side means look. Guessing "unchanged" there would freeze a section at
whatever it held the first time it was seen.

## Measured against the live forum

Two sections, both passes run against `forums.homecomingservers.com` on 2026-09-13.

**155-fort-trident**, 14 threads, one listing page:

| Run | Outcome |
| --- | --- |
| `--formats mbd`, first pass | 14 threads walked, row records `last_activity` |
| `--formats mbd` again | 3 seconds, one request, no thread re-fetched |
| `--formats mbd,mxd` | not skipped — walked all 14, recorded the widened row |

**22-brute**, 2,061 threads, 83 listing pages:

| Run | Cost | Outcome |
| --- | --- | --- |
| Pass A | 83 listing pages, ~8 min | 2,061 threads unchanged, 0 artifacts, proper row written |
| Pass B | **1 request, 7 seconds** | `no new posts since last crawl` |

Pass A could not shortcut because Brute's section row had been backfilled by hand and carried no
formats and no freshness — the refusal is the guard above working, not a failure. That pass also
served as the necro re-check for the whole section: every one of the 2,061 threads re-examined,
nothing posted since it was crawled.

## What it does not cover

- A section whose listing is sorted some other way. Nothing in the crawl detects that; it would
    show up as a section that stops yielding new threads. Re-run the ordering check above against
    a fresh `topics.jsonl` if a section starts looking suspiciously quiet.
- Edits to an existing post that do not bump the thread's last-post time. Those are invisible to
    any freshness check, at thread level as much as at section level.

## Reference

- [`scripts/forum_scrape/scrape.py`](../../scripts/forum_scrape/scrape.py) — `crawl_forum`,
    `is_covered`, `record_section`.
- [`scripts/forum_scrape/crawl-status.ps1`](../../scripts/forum_scrape/crawl-status.ps1) — reads
    the same section rows to report progress.
