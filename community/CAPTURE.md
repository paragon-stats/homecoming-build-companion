# Community Knowledge Capture Workflow

This is the procedure for adding forum posts, wiki articles, and other community-curated content to the project.
Capture is operator-run, by one of two routes that produce the same file:
the Chrome browser + Claude extension for a single thread, or `python -m scripts.forum_scrape.guides` for the guide-shaped
opening posts of whole forum sections ([`docs/automation/forum-guides-and-patch-notes.md`](../docs/automation/forum-guides-and-patch-notes.md)).
The forum's content signal is `ai-train=no, use=reference`; both routes keep attribution and the source URL on every capture, and the autonomous AI crawlers the site blocks are not what either route is.

---

## What goes here

In scope:

- Per-AT meta-build threads (Build Workshop, per-AT subforums).
- Powerset-specific guides ("Definitive Invuln Tanker Guide", "Beam/Devices Sentinel Guide", etc.).
- Mechanics deep-dives (PPM math, ED, recharge breakpoints, "rule of 5" stacking, attack chain calculators).
- Tools/calculators threads.
- Patch-note archives (so Claude knows what's current).
- Wiki entries on canonical mechanics (Enhancement Diversification, Procs Per Minute, Schedule A/B/C/D, Arcanatime, Purple Patch).

Out of scope:

- Personal flame wars, off-topic threads, signatures.
- Anything posted in a private channel without explicit sharer permission.
- Content from forums other than Homecoming's official forum without good reason.

---

## The workflow

1. **Browse the forum/wiki page in Chrome.** You're already logged in if needed.
2. **Open the Claude browser extension** alongside the page.
3. **Ask Claude to extract the post** as a markdown file with the frontmatter format below. Be explicit about what to keep (substantive content) and what to skip (signatures, off-topic replies,
    flames).
4. **Save the markdown** to `community/<topic>/<short-slug>.md` — pick the topic folder by primary tag.
5. **Re-run the index regenerator** so the new file shows up:

    ```powershell
    & "$env:USERPROFILE\repos\homecoming-build-companion\scripts\regen-index.ps1"
    ```

6. (If the post includes a build file or DataLink) **Save the build attachment** to `community/<topic>/attachments/<slug>.mxd` (or `.txt` for DataLink URLs / MBD chunks).

---

## File format

Every capture has YAML frontmatter and a structured body. The frontmatter drives `regen-index.ps1`.

```markdown
---
title: <thread or post title>
url: <full URL>
source: forums.homecomingservers.com   # or homecoming.wiki, wiki.homecomingservers.com
authors: [<forum_handle_1>, <forum_handle_2>]
date_posted: 2024-08-15
date_captured: 2026-04-27
captured_by: local-capture
topic_tags: [tanker, invulnerability, soft-cap-defense]   # first tag drives index grouping
trust: community-consensus   # first-party | community-consensus | single-author-opinion
contradicts_data: none       # or "Claim: foot stomp recharge 18sec; data/canonical: 20sec"
---

## Summary

3-5 sentence takeaway in Claude-friendly bullet form. State the conclusions; don't tease.

- Top-line claim 1 (one line)
- Top-line claim 2 (one line)
- Top-line claim 3 (one line)

## Verbatim excerpt

> Quoted substantive paragraphs from the original post.
> Skip flames, off-topic replies, signatures. If you trim, mark with [...].
> Preserve attribution: "as @user_name argued in reply 17, ..."

## Build attachments

- [tanker_invuln_ss.mxd](attachments/tanker_invuln_ss.mxd) — referenced in the OP
- DataLink: [paste in MidsReborn Ctrl+I](attachments/tanker_invuln_ss.datalink.txt)
```

---

## Topic folders

Pick the **primary** topic for the file's folder placement. Cross-reference via `topic_tags[]` for index grouping.

```text
community/
├── archetypes/         # per-AT guides (tanker.md, blaster.md, defender.md, ...)
├── powersets/          # per-powerset guides (invulnerability.md, fire-blast.md, ...)
├── mechanics/          # PPM, ED, attack chains, rule-of-5, etc.
├── meta-builds/        # named meta builds and DPS leaderboards
├── tools/              # Mids/Pines/calculator threads
├── patches/            # patch notes and game changes
├── validation/         # cross-checks against canonical data (for the diff-spotcheck workflow)
└── attachments/        # binary build files, DataLinks, screenshots
```

A guide that fits multiple folders goes in the most-specific one and tags the others. Don't duplicate.

---

## Trust levels

The `trust:` frontmatter drives how Claude weights the content when sources disagree.

- **first-party** — official Homecoming dev statements (patch notes, dev posts, wiki entries marked official). Most authoritative for game-truth claims.
- **community-consensus** — referenced by multiple respected authors, math-based, demonstrably aligned with in-game behavior. Authoritative for strategy.
- **single-author-opinion** — one author's preference or interpretation. Useful but treat as one perspective.

---

## Conflict resolution rules

When community wisdom contradicts the canonical data:

1. **Game data wins on numbers.** If a post says "Foot Stomp recharge is 18 sec" and `data/canonical/powers/.../foot_stomp.json` says 20 sec, the canonical wins. Set `contradicts_data:` and add a
    note.
2. **Posts older than 2 years** should be re-checked against current data before relying on them. Tag age via `topic_tags: [aged]`.
3. **Strategy claims** are weighted higher when they cite their methodology (parsing, Pylon test, formula derivation) or are referenced by multiple authors.
4. **If a reputable post still disagrees with canonical data** after a check, the post may have caught a Mids data lag. Worth flagging in `data/diff/SUMMARY.md` for the user to investigate later.

---

## Capture quality tips

- **Lead with the conclusion.** The Summary section should answer "what did this thread teach?" without scrolling.
- **Trim aggressively in the verbatim excerpt.** A 50-post thread might have 10 substantive paragraphs. Capture those.
- **Preserve numbers exactly.** If the author says "+167.5% global recharge for perma-Hasten," don't round to 168%.
- **Link the OP and key replies.** Forum URLs include reply IDs (`#post-12345`); use them to cite directly.
- **One thread per file.** Don't bundle threads on the same topic — easier to revise / replace later.

---

## Quick capture template

When asking Claude (in the Chrome extension) to extract a forum thread, paste this prompt:

> Read this forum thread. Produce a markdown capture in the format used by community/CAPTURE.md.
>
> **Multi-post guides.** Many threads spread the guide across several sequential posts by the same author (post 1: intro / table of contents, post 2: section A, post 3: section B, ...). Walk the
> entire thread and treat ALL posts by the original poster (and any users the OP explicitly names as co-authors) as one continuous guide. Concatenate them in chronological order, preserving the OP's
> section structure and headings. If the OP edits a post over time, capture the latest version. Replies from other users are community discussion: include them only if they materially correct,
> extend, or codify the guide content (with attribution). Skip flames, signatures, brief praise, off-topic chatter.
>
> **Embedded builds.** When a forum post contains a complete build dump (a Hero Plan / Villain Plan forum-export block, a `[b]Hero Plan...[/b]` BBCode block, or a DataLink URL), extract it as a
> SEPARATE markdown block in your response, immediately after the parent capture. Each build is a loadable artifact and gets its own file when ingested, so it carries enough context to stand alone.
> When the thread has zero embedded builds, emit only the parent capture.
>
> **Output**: Emit one PARENT capture block followed by zero or more BUILD sub-capture blocks. All output is fenced markdown blocks; no other commentary.
>
> **PARENT frontmatter**: title, url, source, authors (every author whose content you included), date_posted (OP's first-post date), date_captured (today), captured_by (local-capture), topic_tags
> (3-5 tags, first tag from: archetypes, powersets, mechanics, meta-builds, tools, patches, validation, _meta), trust (one of: first-party, community-consensus, single-author-opinion — never the
> forum's user-rank label), multi_post (true/false), post_count, contradicts_data (none unless something stands out).
>
> **PARENT body**: Summary (3-5 bullets covering the WHOLE guide); Verbatim excerpt (substantive paragraphs chronologically, multi-post sections marked `### Post N — Section: <name>`); Notable
> replies (optional); Build attachments (lists every embedded build emitted in per-build blocks below, plus DataLink-only references and .mxd attachments).
>
> **BUILD sub-capture frontmatter** (one per embedded build): title, parent_guide (relative path to the parent capture), parent_url, post_url (specific post URL with `#post-N` anchor when available),
> build_author, date_posted, date_captured (today), captured_by (local-capture), archetype, primary, secondary, ancillary (or "none"), build_goal (one phrase), build_format (forum-export | datalink |
> mxd | mixed), verified (false), contradicts_data (none unless flag), suggested_filename (`builds/<archetype-lowercase>/<slug>.md`).
>
> **BUILD body**: Context (2-4 sentences pulled from the parent guide so the build reads cleanly on its own); Verbatim build (the complete forum-export block, BBCode and all, copied verbatim, source
> formatting preserved); DataLink (when posted); Notes from the author (when present in the same post).

For ready-to-copy per-URL prompts with the URL, date, topics, and trust pre-filled, run [scripts/generate-prompts.ps1](../../homecoming-build-companion/scripts/generate-prompts.ps1) and use the
resulting [PROMPTS.md](../../homecoming-build-companion/inbox/PROMPTS.md).

---

## Maintenance

- The index at [INDEX.md](INDEX.md) is regenerated by `scripts/regen-index.ps1` from the frontmatter — never hand-edit.
- If you remove a capture, just delete the file and re-run the regenerator.
- If a post gets updated, replace the file and bump `date_captured`.
