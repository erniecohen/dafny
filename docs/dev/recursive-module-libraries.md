# Recursive module libraries and caches (issue 466, stage 6)

The semantics remain those of `design/xmod-recursion.md` and
`recursive-module-invariants.md`. A library cannot name its future clients, so a
recursive module group belongs entirely to one producer. This stage adds no
axiom and changes no verification obligation. Executable compilation remains
deferred to stage 7.

## Archive boundary

`LibraryBackend` serializes the retained parsed source, as for other libraries.
With `--recursive-modules`, it also writes a `recursive_modules` manifest table:
schema 1, the SHA-256 of the retained program, a digest of the manifest header
and recorded options, the canonical group inventory, and a digest of these fields.
Each inventory entry lists every member by its
qualified Dafny name and records whether the group is abstract. The digest uses
the UTF-8 `System.Text.Json` representation with its default escaping and
properties in declaration order. A producer
must retain every member of a group or none of them.

Admission checks the schema and all digests before parsing. Resolution then
recomputes the partition from the loaded imports and checks it against the
inventory. Every group that touches an archive must belong wholly to that
archive. Neither a client declaration nor a second archive can complete a
partial group, or join an existing one. A rehashed but inaccurate inventory is
also rejected. Refinement of one member remains legal from a higher component,
with the original sibling imports, as in stage 5.

This is the existing `.doo` trust model, not a signature or a proof certificate.
In particular, it does not authenticate a party that can replace the program
and recompute every digest, and it preserves the existing `--no-verify` library
workflow for programs outside the modular-termination design. Termination
certification remains a separate policy: when those checks are active, either
through `--modular-termination` or a use of the design under the default rules,
the complete-verification record and contracts are required in addition to the
group inventory. Whole-program archive admission and group partition checks run
before snapshot reuse. On a miss, the library's source is resolved again,
recomputing invariant extension checks, raw-member inference, export views and
the group call graph. On a hit, these semantic outputs come from the complete
content-addressed snapshot, validated by the same resolution equality oracle as
source groups.

A library built with the option requires it in its client. Older acyclic
libraries need no group inventory: after resolution their group partition is
empty. An archive with a group and no inventory is rejected. Option-off archives
omit the new table, preserving their previous serialization.

These checks admit a previously unsupported archive feature. They do not repair
an accepted library-group execution path: the base rejects all such paths.
There is therefore no new legacy soundness opt-out. The feature remains hidden,
experimental, and opt-in; existing soundness opt-outs retain their meanings.

## Cache ownership

The group's leader is already the scheduler node for the component. Its
prerequisites are the union of the lower dependencies of all declarations in the
group; its worker resolves all members together. Other declarations depend on
the leader. This stage frames each owned declaration's name, complete source,
effective options and resolver identity separately in the group input digest.
Every member's resolution key depends on that digest, directly or through the
leader. A private method body is an owned input too.

With `--resolution-cache`, the group is one persistent snapshot owned by its
leader. Its key frames every member and internal declaration, including each
source, effective options and name prefix, and all lower prerequisites. A sibling
cannot hit independently. Source frames and visibility-scope ownership cover the
whole group. Phase-local resolver and inference tables are discarded after
resolution; the semantic datatype, call-graph and invariant state is retained.

The snapshot keeps each declaration's signatures, member table and diagnostics
in one shared graph. Publication preserves member identity and reporter sharing,
rebases every member's contextual fields and source tokens, and registers every
internal declaration as a prerequisite of downstream modules. Export and
refinement copies retain the same clone-identity equivalence classes in the
current parse. Publishing an internal import also restores its accepted group
parent immediately, before a downstream refinement can observe it. A malformed or
incomplete snapshot misses and resolves the whole group. The existing
`--resolution-cache-check` oracle compares this entire graph with a fresh group
resolution, including sibling outputs and shared identities.

Without a reusable snapshot, resolution outputs for a group are rebuilt together.
Shared datatype, call-graph and invariant analysis state belongs to that new group. The owner-specific
dependence caches of issue 504 belong to the new extension check; they are not
retained between resolutions. Editing a sibling's function or datatype therefore
cannot retain a previous negative dependence answer.

Translation snapshots already key on the resolution key and the entire group's
translation closure, including source bindings. Every member is retranslated
after a member edit. An edit in an unrelated source file leaves the group's
inputs unchanged. Whole-program admission still runs before any snapshot hit.

As section 7 of the design requires, the verification cache continues to key on
the exact prepared proof task, including its assumptions. Replacing those keys
with an unconditional group epoch would lose the specified early cutoff:
unchanged tasks may hit after all siblings have been retranslated. The edit
control first verifies `if !B.Choice() { assert false; }` with `Choice()` true,
then changes only the sibling definition to false. The false assertion must
fail on rerun, even with both translation and persistent verification caches
warm. A separate private proof-body edit requires all siblings to be
retranslated while unchanged verification tasks still hit.

## Controls and mutations

`git-issue-466-stage6.dfy` runs archive round trips, reachable false assertions,
option compatibility, abstract and three-member groups, higher refinements,
modified records and source, missing inventories, inaccurate rehashed
inventories, split archives and client joins/extensions. It exercises both the
caller-thread and scheduled resolution paths. The legacy type resolver cannot
enable member invariants; the existing stage-2 control checks its rejection.

`RecursiveModuleCacheTests` checks private-body and lower-dependency edits for
two- and three-member groups, independence from unrelated modules, function and
datatype dependence edits, and partial producer output.
`RecursiveGroupEditCannotReuseASiblingsFalseProof` exercises the composed caches.

Mutation controls remove archive integrity admission, partition comparison,
archive ownership, and producer completeness separately. The corresponding
attack is accepted in each case; bypassing integrity also admits a modified
library lemma that lets its client prove false. Restricting the group key to
its leader breaks the member-key edit tests. Replacing a group's translation
descriptor with only the translated member's source allows the edit-and-rerun
false assertion to verify, and the cache regression fails. Each mutation is
built and run separately, then restored. Removing the missing-options guard
also breaks the malformed-manifest diagnostic control.

`git-issue-466-persistent-groups.dfy` compares fresh, cold, warm and audited
resolution, native Boogie graphs, VC verdicts and per-VC resource counts. It also
requires actual group hits, no resolver or solver work on composed warm runs,
whole-group invalidation after private and semantic edits, position rebasing,
unrelated-input independence, valid archive replacement, library admission on warm runs and corruption
fallback. The checks cover scheduled and caller-thread resolution and both
additional-axiom settings. Reachable false assertions remain failures on every
path. The in-process unit control repeats the equality oracle for groups whose
members have distinct source files and for both two- and three-member groups.

The persistent-group mutation controls separately disable group admission,
omit sibling key inputs, split source frames, omit sibling visibility scopes,
retain resolver or inference state, drop a member payload field, skip clone or
context rebinding, omit internal prerequisite registration, split the leader's
reporter, omit per-file option bindings and restore a parsed internal-import
parent. The warm-hit, key, graph-equality, diagnostic or higher-refinement
controls reject each mutation. Each mutant runs serially under a timeout derived
from its passing clean control, and its process group is reaped before the next.
