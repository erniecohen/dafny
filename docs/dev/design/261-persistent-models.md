# Persistent native model reports

With `--persistent-translation --persistent-counterexamples`, a resource-bounded
failed task can retain its native model when `--extract-counterexample` is set.
The model is reporting data, never proof input. The existing no-time-limit and
complete-module publication requirements still apply.

The payload records registered elements in native order and separately records
unregistered literals and datatype expressions reachable from functions and states.
Wire indices use object identity: unregistered native elements can share a native
ID with a registered element. Both the native ID and registration membership are
retained. Functions and applications keep their original order, with explicit
argument/result/default wire indices and ordered reverse references. Reverse references are stored explicitly because native substitutions
can change application targets without rebuilding those lists. Captured states
retain their original order, local variable order and element aliases; inherited
bindings come from the same preceding states as in a fresh native model. Explicit
unknown bindings remain null rather than becoming invented elements.

State names remain stable translation names. Their descriptions are separate
source-range records obtained from native `captureState` commands and validated
against current parsed tokens and excerpts. Replay formats the current ranges
under current reporting options. Descriptions belong to each restored `DafnyModel`,
without installing old locations in shared options. Each replay task exposes its
restored models separately from its terminal diagnostics; it has no fabricated
Boogie split or executable counterexample graph.

Every payload field is required, including explicit nulls. Restoration checks
indices, element kinds and numbering, duplicate names, arities, ranges and the
complete reconstructed data before a module hit is published. Unsupported native
factory shapes miss. Unregistered datatype expressions preserve constructor
arguments through the pinned native parser constructor; if that explicit
constructor is unavailable, the payload misses. Cycles and inconsistent native
construction order also miss. The
independent persistent-translation audit compares the complete model payload,
render decision and terminal diagnostic packet with fresh results.

`PersistentModelsTests` checks native graph identity and malformed/schema controls.
`git-issue-261-persistent-models.dfy` compares independent fresh, cold, warm and
audit verdicts, per-VC resource counts and complete non-timing reports, including
corruption and source relocation. IDE assertion/trace adapters and lifecycle
acceptance remain separate work; a CLI replay hit does not establish those gates.
