# Complete current obligation-check gates

**Newest correction pending:** empty certified-WF branches now retain their
guard in the tautology `G ==> G`, without an empty control-flow split. The
focused generated-Boogie diagnosis verifies Power at all selected seeds, but
this edit still needs its own build and native result; it inherits no earlier
acceptance. Known regressions remain the iteration scope.

**Preceding measured candidate:** semantic correction `338b82396`, product pin
`69dc750b5`, build `861472a9d` passes all ten build stages, 92 structural
observations in both modes, the independent 287-group inventory and all 370 core
unit tests. Its [focused known-regression diagnostic](obligation-guarded-preparation-focused.md)
completes 146 observations: selected default-off vectors agree exactly, but five
of six suite targets still fail at some seeds and Power fails at every seed.
Original issue/subset positives and genuine invalid negative controls retain their
expected results. No new whole-suite or whole-library run was launched.
The complete gates below describe the preceding product `072175269`; they cannot
validate this new candidate. Keep both PRs as drafts and retain the owner's
approval requirement before porting to the development line.

This records the completed supported-line comparisons for the certified
preparation correction `98b0dd509`, compiled product `072175269`, build source
`8d23f3fc8`, native version `4.11.0+fcb2042d.review.77d7a235`, against baseline
`ab210b78b` with Z3 5.1.0. It supersedes preceding revisions' full-gate status.
Original proof files, hints, resource ceilings, gate runners, expected tables,
command options and batching policy were retained. None of the four raw gates
accepts enabled mode. No expectation, resource tolerance or proof hint was
changed to obtain acceptance.

## Complete scope and default-off result

| Complete scope | Additional axioms | Rows / declarations per mode | Batches baseline / off / enabled | Default-off verdicts | Default-off resource vectors | Raw gate |
| --- | --- | --- | --- | --- | --- | --- |
| Verifier suite | Off | 1,161 / 6,909 | 7,605 / 7,605 / 7,605 | Equal | Equal | Rejected |
| Verifier suite | On | 1,161 / 6,909 | 7,605 / 7,605 / 7,605 | Equal | Equal | Rejected |
| Seven-part library | Off | 2,197 / 2,190 | 7,724 / 7,724 / 7,756 | Equal | Different | Rejected |
| Seven-part library | On | 2,197 / 2,190 | 7,724 / 7,724 / 7,756 | Equal | Different | Rejected |

All three modes finish every part. There are no safety timeouts or crashes.
Both baseline and default-off also match the committed expected verdict tables
in all four gates. The [enabled verdict appendix](obligation-current-verdict-changes.md)
records all 60 changed suite rows in each setting and every changed library
declaration, including improvements and diagnostic/count movements.
Both suite runs satisfy exact default-off compatibility. Both library runs
retain identical default-off verdicts and operational Boogie but fail the
fork's strict resource-equality requirement. The [captured-query ordering
experiment](obligation-default-off-query-diagnosis.md) explains one sample,
including a cost movement between unchanged baseline repeats. It does not
establish full native query identity or waive the complete library requirement.

## Enabled suite failures and negative-program boundary

Both axiom settings have the same six newly resource-exhausted declarations:

- `dafny1/ExtensibleArray.dfy`: `ExtensibleArray.Append`.
- `dafny2/SnapshotableTrees.dfy`: `SnapTree.Iterator.MoveNext`.
- `dafny4/FlyingRobots.dfy`: `FormArmy`.
- `dafny4/Primes.dfy`: `AltPrimeDefinition`, `Composite`, `RemoveFactor`.

The cost-review set contains ten declarations with additional axioms off and
eleven with them on. Its union is `NthAppendB`, `INDUCTION_EVEN_ODD`,
`Search` in the TwoDuplicates example, `FibSumManual`, `Fill_All`, `Fill_True`,
`FlyRobotArmy`, `TheoremSeq`, `PriorityQueue_direct.AboutMin`, `FindWinner`
and `sorted_ascending`. Submitted diagnostic matrices retain each original
full-run command and ceiling and cover the failure and cost-review unions over
three seeds, both axiom settings and baseline/default-off/enabled modes.
The [completed seed record](obligation-current-seed-classification.md) classifies
all four failure/cost unions in 648 observations. Suite failure, suite cost and
library cost matrices have no seed-zero outcome movements. The recovered library
failure matrix uses the accepted macOS arm64 binaries and has four recorded
movements relative to the full Linux controls. That platform/scope boundary is
explicit; filtered success cannot replace a full-gate result.

One raw body VC moves from `Invalid` to `Valid`: `Absy.Foo` in
`git-issues/git-issue-2703.dfy`. The source intentionally declares
`ensures 2 / 0 == 1`. Its independent specification-WF divisor check is
`Invalid` in all three modes under both axiom settings. The specification-WF
checks for `Absy2.f` and `Absy3.f` also remain failing. The complete source
therefore remains rejected. The enabled body result relies on a certificate
whose independent check failed; it can be vacuous and is not evidence that
this specification is valid. Do not count a body-only filtered run as
independent specification-WF acceptance. The raw gate's newly-valid body flag
and rejection remain preserved, alongside this source-level diagnosis.

## Enabled library failures

With additional axioms off, eleven formerly correct declarations fail: one
`Errors` result and ten resource-exhausted results. With additional axioms on,
ten fail: the same `Errors` result and nine resource-exhausted results. No
baseline library `Errors` declaration becomes `Correct`, and no baseline
invalid library batch becomes valid.

Shared resource failures in both settings are:

- `DivMod.LemmaDivByMultipleIsStronglyOrdered`, `LemmaModMultiplesVanish`,
  `LemmaMulModNoopLeft`, `LemmaRemainder`, `LemmaRoundDown`.
- `Collections.Seq.LemmaMaxOfConcat`.
- `JSON.ZeroCopy.Deserializer.Sequences.Elements` specification-WF.
- `Termination.TerminationMetric.MultisetOrdinalDecreasesToSubMultiset`.

With additional axioms off, `Power.LemmaPowStrictlyIncreases` and
`Base64.DecodeValidEncode1Padding` also exhaust resources. With them on,
`Base64.EncodeBVIsBase64` instead exhausts resources. The cost-review union
contains `MulEqualityConverse`, `MembersSpec`, `MulModNoopRight`,
`DivIsDivRecursive`, `Utf8.LemmaDeserializeSerialize`, `MulLeftInequality`,
`DivDecreases` and `DivIsStrictlyOrderedByDenominator`.

`Power.LemmaPowSubtractsAuto` is the sole stable new library `Errors` result.
The complete 72-observation native matrix and 48-observation generated-Boogie
replay retain the real check and assertion fuel. The replay isolates failure
to the second clause's materialized WF path. Omitting that preparation or
putting it on an isolated branch changes search and verifies, but would not
establish the required local support invariant. Both are diagnostic experiments
only. The [preparation diagnosis](obligation-local-preparation-diagnosis.md)
records them without presenting a product workaround or lost-fuel claim.

Completed resource/cost seed matrices cover every other affected library
declaration, preserving the original project, source and caps. Remainder,
sequence maximum of concatenation and multiset-ordinal submultiset targets
exhaust resources at all selected enabled seeds while baseline verifies all
three, in both axiom settings. Round-down and strictly increasing power have
that pattern with additional axioms off. The JSON Elements well-formedness
target and one-padding Base64 decoding (axioms off) are both-partial under S13.
Baseline/default-off outcome vectors match; sampled default-off resource
differences remain recorded. These classifications do not rewrite the raw gate.

## Normal regression and editor integration

The actual upstream issue-100 LitTest registration, including its complete
registered helper and expected output, passes. The normal editor integration
test `ConsistentObligationChecksTest.ProjectOptionChangesReverifyTheSameDeclaration`
passes separately. Each phase has exactly one executed/passed test, no failed,
skipped or missing tests, and durable test-runner results.

The [portable test-harness build](https://github.com/erniecohen/dafny/actions/runs/37736235067)
records all eight actual compilation/version stages as successful. Its build
source is `345fda92f`. The assembled normal test hosts use that harness with the
exact separately accepted native compiler/server product assemblies from
`8d23f3fc8` in every load path, with Z3 5.1.0. The normal legacy driver omits
assembly-info version metadata, so the wrapper version and product-assembly
identity are checked separately. The first native attempt rejected that legacy
metadata before any proof; it is not accepted evidence.

These are ordinary registered/editor test executions with explicit assembly
provenance, not an unmodified whole-CI run or complete-suite acceptance. The
independent soundness review and overall acceptance remain open. Strict
default-off library cost remains rejected, enabled resource movements require
review, and no uniform solver-improvement claim is made. PR #168 stays a draft;
porting to #169 still requires explicit owner approval.

## Statement-expression reads audit

A proposed additional negative used a heap-reading function as the argument
of a proof statement in a postcondition:

~~~dafny
class C { var x: int }
function Read(o: C): int reads o { o.x }
lemma Proof(x: int) {}
method Bad(o: C)
  reads {}
  ensures (Proof(Read(o)); false)
{}
~~~

This is not a valid-specification counterexample. Statement translation inside
the specification-WF procedure itself asserts the reads bound for Read(o);
the corresponding body preparation assumes that independently checked bound.
The specification-WF and body both fail in all twelve baseline/default-off/enabled,
resolver and axiom combinations observed. No false complete source is accepted,
and this probe justifies no compiler correction. The wider diagnostic stopped
at a harness assumption that the direct false control would emit a separate
verifiable specification-WF implementation; none is emitted there. It is not recorded
as a completed matrix or product acceptance.
