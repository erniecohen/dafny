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
published worker pipeline. A module's height is its dependency depth, rather
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

The old language-server builtin cache uses CloneId/system shape; it cannot certify
a dependency-local result and is bypassed on the common path. This change does
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
