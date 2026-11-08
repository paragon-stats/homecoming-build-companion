# mids-dump-common: code shared by the Mids dump harnesses

`Dumps.cs` holds every dump the 3.x ([`mids-dump`](../mids-dump/README.md)) and 4.0 ([`mids-dump-4`](../mids-dump-4/README.md)) harnesses write the same way.
Each harness compiles it as linked source, not through a project reference, because each is built against a different MidsReborn fork and a library would have to reference both.
A harness names its fork's character class with a `ToonX` using alias in its project file, and the 4.0 build defines `MIDS4` for the few lines where the two forks' APIs differ.

What stays in a harness: `Main` and the database load sequence, the patch check, how a build is loaded (`LoadBuild`, `OnLoadFailure`) and the extra variants after the parity builds (`DumpExtraVariants`).
Like the harnesses, this folder is outside `CohEngine.slnx`.
