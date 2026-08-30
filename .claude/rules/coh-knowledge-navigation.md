# Rule: Navigate the CoH knowledge tree per build request

**Status:** MUST. Applies whenever a session is asked to draft, modify, evaluate, or compare a City of Heroes build.

The CoH knowledge tree at [`knowledge/coh/`](../../knowledge/coh/) is **not auto-loaded.**
Loading the entire 14-AT roster + every powerset + every preference + every build goal into
context for every build request would blow the context budget. Instead, the agent navigates
the tree per request and loads only the files that match.

## When this rule fires

Any user request that:

- Asks for a new build (specifies AT + primary + secondary + goal).
- Asks for a modification or v2 of an existing build.
- Asks for evaluation, comparison, or critique of a build.
- Asks "what would I build for X" / "how would I slot Y".

If the request doesn't involve a build, the knowledge tree stays unread.

## What to do

1. **Read [`knowledge/coh/INDEX.md`](../../knowledge/coh/INDEX.md) first.** This is the navigation document. It maps (AT-style → AT) and (build-goal) to the file paths to load.
2. **Identify the request's parameters:**
    - Archetype-style group (melee / ranged / ranged-support / control / pets / kheldian / arachnos-soldier / arachnos-widow).
    - Specific archetype within that group.
    - Primary powerset (with category sub-folder: melee / ranged / control / buff / pet).
    - Secondary powerset (with category sub-folder: defense / manipulation / support / assault).
    - Ancillary pool (if specified).
    - Pool powers picked (Speed, Leaping, Fighting, Leadership, etc.).
    - Build goal (max-single-target-dps, soft-cap-defense, perma-hasten, all-around, set-and-forget, etc.).
3. **Load only the files INDEX points to** for those parameters. Typical load for a single build request is **8–14 files**, never the whole tree.
4. **If a file the agent needs isn't authored yet,** stop and tell the user. Don't fabricate content from training memory.

## Trust order for facts

When the knowledge tree disagrees with another source, or when a file is missing:

1. **`data/canonical/`** — source of truth for power numbers, AT caps, IO set composition. Always wins on numeric claims.
2. **`data/diff/current_overrides.json`** — patch-history corrections for canonical data lag. Cite when relevant.
3. **`knowledge/coh/`** — curated guidance (mechanics summaries, preferences, build-goal posture). Authoritative for recommendations and tactics.
4. **`community/`** — captured forum guides and expert builds. Authoritative as community precedent; secondary on numbers.
5. **Training memory** — last resort. Always labeled "approximate; verify in MidsReborn" when used.

## What NOT to do

- Don't auto-load the entire `knowledge/coh/` tree at session start — that's the context-window failure mode this rule prevents.
- Don't load files outside the AT-style and build-goal the request actually involves.
- Don't substitute training memory when a file is missing — surface the gap, ask for direction.
- Don't cite the knowledge tree for numeric claims when `data/canonical/` would settle it.

## Reference

- [`build-creation.md`](./build-creation.md) — full build creation rules + pre-flight checklist.
- [`exemplar-10.md`](./exemplar-10.md) — exemplar-10 playability rule.
- [`hard-limits.md`](./hard-limits.md) — game-engine invariants.
- [`knowledge/coh/INDEX.md`](../../knowledge/coh/INDEX.md) — navigation document (created in Phase 4 of the consolidation plan; until authored, the agent stops and asks).
