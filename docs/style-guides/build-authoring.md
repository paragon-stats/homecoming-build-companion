# Build authoring

A guide to designing a City of Heroes (Homecoming) character build against MidsReborn data, for a person or a tool.
A build loads into MidsReborn for verification and is benchmarked against community builds.
Where this guide and its linked rule guides conflict with a prior assumption or a training-data shortcut, the guides win.

## What a build is

Every build is a sequence of `(power picked at level X)` and `(slot placed at level Y in power Z)` records. It matches MidsReborn's forum export format:

- `(N)` is the character level when the slot was placed in this power.
- `(A)` is the inherent slot (free with the power).
- IO level, when shown, goes after the name as `: Level XX`.

The full mechanics and the pre-flight checklist are in [build-creation.md](build-rules/build-creation.md).

## Two artifacts

Every build proposal produces both:

- **JSON**, schema-valid per [`build-format/schema.json`](../../build-format/schema.json). This is the loadable artifact and the source of truth.
- **Markdown** in MidsReborn forum-export format, for review and forum sharing. It is a view of the JSON.

They agree on every power, slot and enhancement.

## Principles

- **Optimize for level 50 and keep exemplar 10 playable.** Verify attacks, defense and travel by level 12 without trading level-50 quality. See [exemplar-10.md](build-rules/exemplar-10.md).
- **Respect the hard limits during the draft, not after.** The limits are 50 levels, 24 power picks, 67 added slots, and 6 slots per power (1 inherent plus 5 added).
  Also 4 power pools, pool tier prerequisites, pick and slot levels, Enhancement Diversification, the Rule of Five, and archetype caps.
  Keep a running slot tally and per-power count, and re-budget early. See [hard-limits.md](build-rules/hard-limits.md).
- **Cite canonical data; treat training memory as approximate.** For specific numbers the order is `data/canonical/`, then `data/diff/current_overrides.json`.
  Next come the captures in the source root (see the [source material manifest](../../data/source-material/README.md), lands with #91).
  Last is training memory, labelled "approximate; verify in MidsReborn".
  For format details read `MidsReborn/clsOutput.cs` (the emitter) and `build-format/schema.json`.
- **Slot attacks accuracy-first.** See [attack-slotting.md](build-rules/attack-slotting.md).
- **Use full IO set names in prose.** Abbreviations appear only inside verbatim Mids forum-export blocks. See [io-set-naming.md](build-rules/io-set-naming.md).
- **Put real design tensions in the Trade-offs section.** Examples are defense vs damage, AoE vs single target, and ATO 6-piece vs procs.
  Errors get fixed before emit; unverified specifics go in "What I would verify".
- **Label capture trust.** A capture sets `trust:` to `first-party`, `community-consensus` or `single-author-opinion`.
  Forum user-rank labels map to the canonical value at save, and ingestion fixes drift on contact.
- **Refer to the user generically.** See [naming-and-words.md](naming-and-words.md).

## Pre-flight order for emitting a build

1. Identify the archetype, primary, secondary and build goal.
2. Read [build-creation.md](build-rules/build-creation.md) and confirm the pre-flight checklist applies.
3. Navigate the knowledge tree for the request: [knowledge-navigation.md](build-rules/knowledge-navigation.md).
4. Pull the powerset data from `data/canonical/powers/`.
5. Pull the archetype caps from `data/canonical/archetypes/<at>.json` or `strategy/breakpoints.json`.
6. Check `data/diff/current_overrides.json` for patched values affecting the chosen powers.
7. If a capture exists for the archetype or powerset in the source root, read it.
8. Draft the build with a running slot count and per-power tally.
9. Walk the pre-flight checklist; every item passes or the build does not emit.
10. Emit JSON and markdown, then surface real trade-offs and verification gaps.

The engine validates the mechanical part of the checklist; its architecture is in [engine-architecture.md](engine-architecture.md).
