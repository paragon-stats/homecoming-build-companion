# Style guides

Committed, tool-neutral guides for this repository. Mechanical rules also run as checks in hooks and CI.

## Index

- [ai-assistance-policy.md](ai-assistance-policy.md): agent instruction files are local-only and never committed.
- [build-authoring.md](build-authoring.md): what a build is, the two artifacts, and the pre-flight order.
- [build-rules/build-creation.md](build-rules/build-creation.md): format spec, slot allocation, pre-flight checklist, lessons learned.
- [build-rules/exemplar-10.md](build-rules/exemplar-10.md): the exemplar-10 playability rule.
- [build-rules/hard-limits.md](build-rules/hard-limits.md): game-engine invariants.
- [build-rules/attack-slotting.md](build-rules/attack-slotting.md): accuracy in slot (A) and the first added slot.
- [build-rules/io-set-naming.md](build-rules/io-set-naming.md): full IO set names in prose.
- [build-rules/knowledge-navigation.md](build-rules/knowledge-navigation.md): load only the knowledge files a build request needs.
- [engine-architecture.md](engine-architecture.md): check registries, classify-once, and the fidelity boundary.
- [error-output.md](error-output.md): the diagnostic and error-output contract every check emits.
- [naming-and-words.md](naming-and-words.md): generic placeholders, banned words, plain words.
- [repository-conventions.md](repository-conventions.md): git, DevSecOps, Python and Markdown conventions specific to this repository.
- [dependencies.md](dependencies.md): dependency policy.
- [../repository-standards/style-guides/markdown-style-guide.md](../repository-standards/style-guides/markdown-style-guide.md): Markdown style.
- [../repository-standards/style-guides/python-style-guide.md](../repository-standards/style-guides/python-style-guide.md): Python style, until Python is removed.
- [../repository-standards/style-guides/error-output-style-guide.md](../repository-standards/style-guides/error-output-style-guide.md): the error-output SSOT.
- [../repository-standards/git-standards.md](../repository-standards/git-standards.md), [git-branching.md](../repository-standards/git-branching.md),
  [devsecops-workflow.md](../repository-standards/devsecops-workflow.md): git, branching and DevSecOps standards.

## AI assistance

Agent instruction files (`CLAUDE.md`, `.claude/`, and the like) are not committed. See [ai-assistance-policy.md](ai-assistance-policy.md).
