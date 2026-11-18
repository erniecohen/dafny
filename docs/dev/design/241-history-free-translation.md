# Default dependency-local translation

The owner authorizes this one-time default change at
https://github.com/erniecohen/dafny/pull/262#issuecomment-6826006467.
It is a prerequisite to https://github.com/erniecohen/dafny/pull/262, not an
optional cache mode. The persistent result cache in
https://github.com/erniecohen/dafny/pull/278 stands independently.

A module's generated names, fresh counters, declaration/emission/conjunct order
must depend only on its own contents and dependencies. Adding, editing, resizing
or translating an unrelated module first must not change its translated text.
The implementation reuses the published scope/counter/key work from
https://github.com/erniecohen/dafny/pull/263. It does not save or restore global
counter values, seed local counters from global positions, or install a competing
naming algorithm.

## Common default path

Sequential resolution now uses the same dependency-local module views as the
published dependency-local ownership operation. The default calls this operation
directly on its existing resolver thread. Only positive `--resolution-threads`
values create scheduler workers; parallel scheduling is not a prerequisite. A module's height is its dependency depth, rather
than a global ordinal. Builtin shapes and bit widths are gathered from the
module's resolved dependency closure and emitted in a stable order. The existing
module naming scope supplies fresh counters for resolution and translation,
including imported function values, match labels, proof IDs, capture-state IDs
and lowered variables. Worker count selects scheduling only.

Each translator owns its proof/report manager. The module task result carries
that manager through preparation, CLI diagnostics, coverage and editor updates.
Modular termination scopes the supplied owner instead of replacing it with a
program-wide report map. Current whole-program validation remains mandatory;
none of its admission receipts is supplied by a translation-cache hit.

The translator retains an unprepared snapshot and materializes one mutable
consumer graph through the same copier in every mode. A translation table must
retain the snapshot without preparing it, and use precisely that materialization
operation for its cold and warm consumers. This preserves expression topology,
literal aliases, metadata and current verification-symbol bindings. Unknown
snapshot metadata rejects reuse. An unsupported snapshot follows the same fresh
translation fallback in default and cache modes. No parser round-trip substitutes
for reporting metadata.

The legacy language-server CloneId/system-shape builtin cache cannot certify
a dependency-local result. The default caller-thread path invokes the shared
content-keyed complete-output hook instead. The existing editor cache preserves
reuse when parse identity and exact prerequisite outputs still match, retaining
builtin, signature, member and translation-input views together. This change does
not publish or trust a persistent resolution snapshot. Final L/G/R identities
and detached persistence remain separate work.

## Regression and acceptance

`HistoryFreeTranslationTests` exercises both resolver engines, sequential and
parallel scheduling, module-alone versus full-program translation, reversing
translation order, and resizing an unrelated module from one function to 64.
The unrelated module introduces different bit widths and tuple shapes, appears
before or after the target, and is asserted absent from its dependency inventory.
Every other tested unit's complete Boogie text, including checksums, must match.
A dependency edit must change the client's text as a nonvacuity control.

A separate unit control captures the actual Boogie-generated raw SMT command
sequence across unrelated growth and earlier translation. Synthetic responses
serve only to drive command generation; this is an emission-order test, not
proof evidence. Native integration includes a verified imported-function lemma
and a reachable failing assertion.

The prerequisite intentionally changes default generated text once. Qualification
must compare the complete suite and standard library against the exact shipped
base, report every moved case and per-task RU delta, and list every outcome
change by declaration. Repair moved proofs where possible; otherwise record each
changed verdict's reason. Default expected verdicts are refreshed only from a
failed gating run artifact, in a separate explained commit per REVIEW.md.
Historical opt-in measurements and the historical 194-case classification are
not a current prerequisite qualification. Until current evidence is complete,
this remains a draft and must not merge.

After rebasing the translation table, option-off, enabled-cold and enabled-warm
must match byte for byte in printed Boogie, raw SMT, outcomes, RU, stdout/stderr
and exit. A differing graph-construction order is a defect even when printed
Boogie matches. If a construct inherently requires program-global translation
state, report the construct, measured size and effective options; do not disguise
that dependence by saving/restoring counters or weakening the equality check.

## Moved-proof repair

The filtered `Std.Base64.EncodeBVIsBase64` proof exhausts its existing one-million
RU bound with the refreshed resolver and additional axioms off after the default
translation changes. Assertion isolation retains the same contract and proof
body while checking the modulo, sequence-bound, padding and final membership
obligations separately. It adds no axiom, assumption, precondition or unchecked
fact. The selected declaration passes all four combinations of additional axioms
and consistent-obligation checks at the unchanged bound. This is focused repair
evidence; whole-library and dependent-client qualification still gate the PR.

The same preparation change makes `Std.JSON.ZeroCopy.Deserializer.Core.TryStructural`
exhaust its existing 100-million RU limit when checking its four well-formedness
obligations together. Assertion isolation checks those existing obligations
separately, preserving the function body and contract. All four additional-axiom
and consistent-obligation combinations pass without changing the bound.

`Std.BulkActions.BatchReader.Read` remains out of resource at its original bound.
The final validity assertion changed cost after its module's translated/pruned
text changed. Attempts to expose its validity conjuncts did not give a repair
that passes all four option combinations; those diagnostic edits are not kept.
Its moved verdict is recorded for the baseline review instead of increasing its
resource ceiling. The complete pre-repair cohort and every changed-task RU delta
are in [the qualification report](241-default-translation-results.md).

## Numeric normalization

Boogie normalized declaration order also participates in solver emission. Its
legacy numeric content hashes use process-dependent runtime hashes for large
integers. Two fresh invocations of the shipped compiler can therefore emit
different SMT despite identical Boogie. The default prerequisite selects the
existing `DeterministicLiteralHashes` path in the pinned Boogie, so integer, real,
bitvector and floating-point content hashes derive from their values. An explicit
`--boogie /deterministicLiteralHashes:0` remains a legacy override and is outside
the cross-process stable-input profile. No Boogie implementation or global-state
reset is introduced.

## Exact native input qualification

Compare raw SMT per proof task from its own empty solver checkpoint, rather than
per physical solver process. Include the task's complete ordered native setup,
prelude/options, assertion and adaptive model/error requests. A reset establishes
the empty boundary; compare the following bytes. A new process starts at its
fresh empty checkpoint. End the task before idle cleanup. Require identical task
bytes, outcomes, RU, stdout/stderr and exit across prerequisite/table-OFF and
OFF/cold/warm. Every observed task must have a complete record; pair names and
multiplicities without changing command order inside a record.

Report physical solver counts and whole-process hashes separately. Prior tasks,
physical process boundaries and idle-handshake recycling do not belong to a
task's comparison. The pre-existing 100 ms idle-handshake policy can retire a
healthy checker with the cache OFF too, as measured in
https://github.com/erniecohen/dafny/issues/287. This stack leaves ordinary pooling
and the Boogie lifecycle unchanged. Any task-local byte mismatch remains a blocker.

Logical reset provides an empty assertion context, not complete native-process
isolation. The [controlled history replay](241-native-process-history.md)
reproduces the unrelated-growth 24-RU difference with fixed task bytes and
fixed source. Fresh native target-only runs agree. This is classified separately
from task-input equality; ordinary pooled solving is unchanged.
