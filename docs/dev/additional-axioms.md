# Additional verifier axioms

`--additional-axioms` is the shared, default-off switch for approved added-fact
families in this fork. The legacy spelling is `/additionalAxioms:1`.
In `dfyconfig.toml`:

```toml
[options]
additional-axioms = true
```

CLI `--additional-axioms=false` overrides a project setting. The normal project
option machinery supplies the setting to editor verification. The option is a
process/project verification setting (`OptionScope.Cli`), not a change to the
language or a `.doo` semantic compatibility requirement. Record its value in
proof-cost comparisons. It is distinct from `--allow-axioms`.

## Current family: bounded integer/bitvector round trips

For each registered positive width `w`, exactly one new axiom is emitted:

```boogie
axiom (forall x: int :: { nat_from_bvW(nat_to_bvW(x)) }
  0 <= x && x < M ==> nat_from_bvW(nat_to_bvW(x)) == x);
```

Here `W` stands for the concrete width and `M` for the exact integer numeral
`2^w`. For `bv32`, `M = 4294967296`; for `bv64`,
`M = 18446744073709551616`. The binder and intermediate have native integer
and bitvector sorts. The bound is computed with arbitrary-precision integers.
Registered widths include those introduced by shift-amount optimization.
There is no new axiom for `bv0`, whose inline conversions return zero.

### Derivation and combination with existing families

Interpret a width-`w` bitvector by its unsigned binary value
`U_w(b) = sum(bit_i(b) * 2^i, i = 0 .. w-1)`. The trusted native integer
conversion `I_w(x)` has unsigned value `x mod 2^w`, the nonnegative remainder.
The existing wrapper bridge says `N_w(b) = U_w(b)`. Thus for
`0 <= x < 2^w`, `N_w(I_w(x)) = x mod 2^w = x`.
These are the [SMT-LIB unsigned conversion semantics](https://smt-lib.org/theories-FixedSizeBitVectors.shtml).
The new family is entailed by the existing bridge and native semantics.
If `T` is that trusted theory, `S` any other enabled family, and `A33` this
family, `Models(T union S union A33) = Models(T union S)`. This shows the
addition imposes no new model restriction in combination with `S`; it does not
prove consistency of the entire prelude or of `S`. The shared switch also exposes the closed literal identities derived below.
Those identities are entailed by the trusted literal definition, so the same
model argument applies with both additions enabled. Inventory this again when
adding another family.

The strict upper bound and lower bound are essential: the total native round
trip is zero at `2^w`, and is `2^w - 1` at `-1`. Native conversions being total
does not make either Dafny cast legal. All existing source fit, integrality,
narrowing, boxing and allocation checks remain intact.

### Trigger and compatibility

The single explicit pattern is `N_w(I_w(x))`, with the existing uninterpreted
wrapper outermost. It matches round-trip terms, including terms exposed by
equalities. It does not trigger on every integer conversion and does not
introduce a structurally deeper conversion in its body. The guard restricts the
conclusion, not matching; false/unknown guards are covered by SMT controls.
The new quantifier has stable profiling id
`additional_axioms_bv<W>_int_round_trip`, retained in SMT when profiling with
Boogie `/emitDebugInformation:1`. Existing triggers, declaration order,
pruning, optimizations and solver settings are unchanged. The option is checked
before constructing any new AST nodes. No modulo law, inverse family,
injectivity fact or arithmetic-operation axiom is added.

## Validation

The unchanged original reproducer is `git-issues/Inputs/github-issue-33-original.dfy`.
The integration driver `git-issues/github-issue-33.dfy` checks both resolvers,
widths, conversion shapes, negative fit checks and a live false-proof control.
The review workflow runs that driver with pinned Z3 5.1.0 (`%review-z3`).
A second permanent driver, `git-issues/github-issue-33-z3-4.12.1.dfy`, uses an
explicitly pinned 4.12.1 substitution, independent of future harness-default
changes. It gates the complete unchanged original, including `ShiftRightByZero`,
positive conversions, bitvector newtypes, and genuine negative controls in both
supported resolver modes at the same 200,000-RU cap. The literal follow-up also
has `github-issue-74.dfy` and structural translation tests for repeated, negative,
large and excluded arguments, independent modules, branches, and same-module
isolation from literals occurring only in another function body.
The issue validation workflow records measurements and actual Boogie/SMT inputs.
Its baseline is the green shipped build at `1d45201a027d46be24988fe0946c0bde80a702c9`.
The retained public artifacts contain per-VC results and commands.

### Solver boundary and stress limits

With the closed literal identities enabled, the complete unchanged original
passes end to end on pinned Z3 4.12.1 and 5.1.0 in both resolver modes at
200,000 RU. Before the literal follow-up (#74), the #73 encoding still exhausted
the cap on 4.12.1 for `ShiftRightByZero`. The first, module-wide prototype achieved this result but failed enabled-mode
acceptance comparisons below. The local revision exposes the identity only
through verification-context assumptions; its acceptance results must be
reviewed before removing the shipped #73 limitation. Arbitrary indirect goals
have no general proof-cost guarantee. The existing library/regression gates retain their
established configurations.

The generated scaling corpus verifies nesting through depth 64 and widths
through 1024. A single assertion batch combining 1000 distinct round trips
exhausts 20,000,000 units; this family does not guarantee linear total solver
cost. The negative guard mutations check explicit instances of the extracted
formula, while SAT witnesses give the wrapper its standard unsigned native
interpretation. Live Dafny false-proof controls separately audit actual matching.

### Literal identity diagnostic before #74

The SMT comparison replays the unchanged `ShiftRightByZero` implementation VC
in a fresh process for each variant. It preserves all solver options and the
20,000,000-RU cap; quantifier profiling and full statistics are observations.
The variants are the captured query, one ground instance of the existing
`LitInt` definition, a diagnostic conversion-only pattern, and one explicit
bounded round-trip instance for the actual input. The latter two variants are
measurement controls, not proposed production changes.

The [complete replay artifacts](https://github.com/erniecohen/dafny/actions/runs/37087827539)
contain input checksums, solver versions/checksums, raw stdout/stderr, complete
statistics, and per-query quantifier inventories/profiles. Both solvers replay
identical query bytes for each variant. Reported resource counts include setup
units beyond the check's cap; 4.12.1 returns `unknown` with reason `canceled` at
that cap, without a process cancellation or wall-time safety timeout.

| Query variant | 4.12.1 result / RU | 5.1.0 result / RU | Instantiations (4.12.1 / 5.1.0) |
| --- | --- | --- | --- |
| Unchanged | unknown / 20,002,972 | unsat / 21,983 | 137,612 / 74 |
| Ground `LitInt(0) == 0` | unsat / 6,657 | unsat / 6,657 | 3 / 3 |
| Conversion-only pattern (diagnostic) | unknown / 20,002,972 | unsat / 20,415 | 7 / 7 |
| Ground bounded round-trip instance | unknown / 20,002,987 | unsat / 20,430 | 5 / 5 |

Every query emits four profile records. Their instance counts sum exactly to
its `quant-instantiations` statistic. Of the captured query's 17 quantified
formulas, others have no nonzero profile record (they may have been removed by
preprocessing). The unchanged older query records 137,609 round-trip instances,
versus 41 on 5.1.0; the ground literal identity reduces that family to one on
both solvers. The other records are the function definition, existing literal
identity, and native wrapper bridge. The ground identity has three instances
total and one literal-identity checker-satisfied record. The profile fields and
exit-time emission follow [Z3's profiling implementation](https://github.com/Z3Prover/z3/blob/z3-4.12.1/src/smt/smt_quantifier.cpp#L153-L180).
The broader pattern and ground round-trip reduce instantiation counts but do
not repair the older solver's resource exhaustion.

The relevant definition is already in `DafnyPrelude.bpl`:

```boogie
function {:identity} LitInt(x: int): int { x }
```

A closed instance `LitInt(n) == n` adds no semantic assumption: substitute the
integer numeral `n` in this definition. Retaining the marker preserves the
existing literal-sensitive function encoding. The zero instance can expose the
zero shift amount and the numeric lower bound before expensive bitvector search.
The comparison demonstrates a performance effect; instantiation counts alone
do not establish the solver's internal ordering of simplification and search.

### Closed native integer literal identities (#74)

The translator inspects native function-call actuals after boxing adaptation.
Only an argument exactly `LitInt(n)`, with `n` a concrete native integer numeral,
qualifies. The original argument and all checks remain intact.

`CanCallAssumptionForVerification` augments an existing local can-call assumption
with these closed identities. The original `CanCallAssumption` is retained for
shared function-definition, consequence, let and type axioms. A collector visits
only the local source expression, including its nested expressions; it does not
visit the body of a called function. Function-call well-formedness checking also
inserts identities for the final native actuals in that implementation's local
statement builder, excluding receivers and implicit arguments.

Each emitted assumption owns its numerically sorted arbitrary-precision set.
Repeated values in that assumption are deduplicated. There is no inventory or
suppression across modules, declarations, branches or other verification
contexts: an identity emitted in one context may be absent from another. No
top-level literal identity axioms or pruning dependencies are added.

The shared option guards both collection and insertion before constructing new
identity nodes. With the option off, the existing can-call expression is returned
without collecting numerals or emitting extra commands. These are substitutions
in the trusted `LitInt` definition, not a new quantified family. Symbolic, real
and boxed arguments are excluded. Markers, the bounded round-trip formula and
composite trigger, the native bridge, pruning and solver settings are preserved.
The actual cost need not match the edited-query replay's 6,657 RU.

The issue-74 validation workflow compares the merged baseline and candidate with
the option off and on, records per-batch costs, saves actual SMT, and verifies
literal-heavy non-bitvector code. Its expected failing negative/vacuity probes
are recorded separately from the required regression gate.

### Rejected module-wide prototype

The module-wide literal implementation was rejected for merge. Its
[paired suite comparison](https://github.com/erniecohen/dafny/actions/runs/37095952540)
against merged #73 covers 1,081 programs and 7,499 proof batches. Option-off
verdicts and controlled resource counts have no differences. With the option on,
three previously valid batches exhaust the existing 16,000,000-RU cap:

| Batch | Merged #73, option on (RU) | Module-wide identities (RU) | Result |
| --- | --- | --- | --- |
| `SnapshotableTrees`, `Iterator.Push` batch 13 | 5,226,959 | 16,037,767 | out of resource |
| `Lucas-up`, `INDUCTION_EVEN_ODD` | 1,302,475 | 16,014,820 | out of resource |
| `Ackermann`, `Am` | 36,339 | 16,025,284 | out of resource |

Counters include setup work beyond the cap. Seven programs have substantial
cost increases (eight batches exceed both twice the baseline cost and a
100,000-RU increase). Three batches improve: one formerly out-of-resource
function-specification proof and two true `Gauss` assertions whose literal
encoding formerly failed to unfold the recursive function. This is not an
option-on acceptance pass, despite the probe workflow recording expected
failures and exiting zero. The required option-off CI and permanent source
regressions pass separately.

[Isolated actual-query diagnostics](https://github.com/erniecohen/dafny/actions/runs/37097153001)
keep the source, solver settings and cap unchanged. Removing only the collected
closed identities from the candidate SMT restores all three failing proofs.
All 45 selected correctness-query profiles account for their complete
`quant-instantiations` statistics. `Am` returns from 36,165 instances to the
baseline's 232; the failing `Push` query returns from 48,063 to 22,329. The
filtered fresh-process Lucas query returns from 271,403 to 18,654 instances and
1,159,912 RU (its full-program baseline above has a different cost). These
removals are causal diagnostics, not a production repair or end-to-end evidence
for a different implementation.

The same comparison verifies 2,190 source-library declarations per solver
using the library's existing limits, including its larger declaration-specific
limits. All 92 complete module translations are byte-identical in the two
option-off builds and with explicit CLI false. Option-off library verdicts have
no differences. Default-order resource counts vary in 239 declarations on
4.12.1 and 67 on 5.1.0; unchanged-build repeats vary in 238/19 and 46/41,
respectively. This is consistent with the pre-existing declaration-order
variation in [#30](https://github.com/erniecohen/dafny/issues/30), rather than a
controlled resource-equality result for those default-order runs. The controlled
suite and focused SMT comparisons above have exact resource equality.

The option-on library comparison also fails acceptance: two previously correct
declarations regress on 4.12.1 (`DivMod.LemmaHoistOverDenominator` and
`Power2.Lemma2To64`), and four regress on 5.1.0
(`DivMod.LemmaMultiplyDivideLe`, `BulkActions.ToBatchedProducer`,
`JSON.ConcreteSyntax.SpecProperties.ConcatBytes_Linear`, and
`JSON.ZeroCopy.Deserializer.Sequences.Elements`). `Lemma2To64` becomes a proof
error; the others exhaust their existing limits. These are observed paired
library differences, without the causal removal diagnostics performed for the
three suite failures. Improvements elsewhere do not make this comparison pass.

Ordinary pruning retains a closed `LitInt(n) == n` whenever `LitInt` is relevant:
the axiom's expression itself produces that dependency. Attaching it to another
function's definitions would not remove the incoming `LitInt` dependency. The
module-wide identities can therefore reach proofs with no eligible literal call
of their own. The [review decision](https://github.com/erniecohen/dafny/pull/76#issuecomment-5966298566)
authorized the local revision described above, while requiring new focused and
complete comparisons and merge review. It explicitly prohibits adding identities
to shared quantified can-call axioms. No quantified family, trigger, pruning
algorithm, solver setting or resource limit is changed by the revision.

### Local revision validation

[Focused end-to-end validation](https://github.com/erniecohen/dafny/actions/runs/37103804760)
uses the actual local implementation and checksum-pinned Z3 4.12.1 and 5.1.0.
The unchanged original verifies all 17 batches in both resolver modes at the
existing 200,000-RU cap. `ShiftRightByZero` correctness uses 6,654 RU and its
well-formedness query uses 4,413 RU in each combination. Positive conversions
and literal-heavy non-bitvector code pass; the conversion negative controls
retain 11 errors and the native literal/precondition/vacuity controls retain
three errors. Same-module isolation verifies five batches on each solver/mode.

Four permanent structural tests check both resolver modes and flag settings.
They reject closed identities anywhere in shared quantified axioms, check
ordered per-assumption deduplication, and check that other-body literals do not
enter unrelated declarations. The actual-SMT audit inspects every captured
original/isolation query: zero is available in the original; no `777` identity
from the other function body propagates; no top-level closed identity assertion
is present. Debug-free normalized logs omit VC names, so the audit does not
invent an ordinal-to-declaration mapping. Named declarations are checked by
the structural tests separately.

All three known suite failures recover their exact merged-baseline resource
counts in the focused whole-program comparisons:

| Correctness batch | 4.12.1 baseline and local (RU) | 5.1.0 baseline and local (RU) |
| --- | --- | --- |
| `Iterator.Push` batch 13 | 191,298 | 5,226,959 |
| `INDUCTION_EVEN_ODD` | 765,192 | 1,302,475 |
| `Am` | 36,503 | 36,339 |

The six library regression filters have no new incorrect/out-of-resource
outcomes on either solver under their existing project limits. This does not
claim that every filtered baseline declaration is correct: e.g. 5.1.0's existing
`LemmaHoistOverDenominator` exhaustion remains on both builds, while
`Lemma2To64` improves from exhaustion to a proved result with the local facts.

The [complete paired suite comparison](https://github.com/erniecohen/dafny/actions/runs/37104365416)
checks 1,081 programs and 7,499 batches on Z3 5.1.0. Option-off verdicts and
controlled counts are identical. There are no enabled verdict regressions or
improvements. One program retains two substantial enabled cost increases,
which both finish within the unchanged 16,000,000-RU cap:

| `MinimumWindowMax` correctness batch | Merged #73, option on (RU) | Local identities (RU) |
| --- | --- | --- |
| 116 | 2,257,780 | 5,387,699 |
| 133 | 2,911,555 | 9,776,756 |

[Required scratch CI](https://github.com/erniecohen/dafny/actions/runs/37104086114)
passes the regression harness, resolver matrix, suite verdict gate and both
source-library verdict gates. The [completed paired library comparison](https://github.com/erniecohen/dafny/actions/runs/37104365416)
checks 2,190 declarations on each solver at the existing project and declaration
limits. Neither solver has a new enabled verdict failure. On 4.12.1,
`LemmaIndistinguishableQuotients` and `LemmaMultiplyDivideLt` improve to proved
results, leaving all 2,190 declarations correct in this run. On 5.1.0,
`Lemma2To64` improves to a proved result; other pre-existing failures remain.
These are observed end-to-end improvements, not causal edited-query diagnostics.

All 92 library module translations are byte-identical with the option off,
including explicit CLI false, and there are no option-off verdict differences.
The default-order B/O resource counts match on 4.12.1 and differ in 82
5.1.0 declarations. Unchanged-build repeat controls differ in 48/47 declarations
on 4.12.1 and 8/98 on 5.1.0. These default-order counts retain the known #30
variation; the controlled focused and complete-suite comparisons establish
exact option-off input/count equality separately. The remaining enabled cost increases and completed library
results are submitted for merge review; this revision is not yet merged.
No existing proof source, expected-verdict file or resource limit is changed.

### Isolated 1,000-term performance characterization

The [reviewer diagnostic artifacts](https://github.com/erniecohen/dafny/actions/runs/37087827539)
replay each captured stress query in a fresh process, retaining complete
exit-time profiles. The grouped case has two queries: well-formedness is UNSAT
at 637,151 RU on both solvers; correctness exhausts the unchanged cap at
20,034,410 RU on both. The resource counter includes query setup; the cap remains
20,000,000. All profile instance counts agree with full solver statistics.

| Grouped correctness profile | 4.12.1 instances | 5.1.0 instances |
| --- | --- | --- |
| Bounded round-trip family | 4,434 | 4,415 |
| Existing native wrapper bridge | 4,434 | 3,986 |
| Existing literal identity | 1 | 1 |
| Total | 8,869 | 8,402 |

Each of the three records has maximum generation 0 and maximum cost 1. The
input contains 15 quantified formulas; the other formulas have no nonzero
exit-time record. These counts characterize this input and cap, not a general
complexity bound. Fresh-process solver statistics report `:max-memory` values of 696.89
(4.12.1) and 730.88 (5.1.0), separately from the earlier command's GNU-time RSS
measurement.

The isolated comparison keeps the exact 1,000-term source (SHA-256
`aa7b4ea46e5caa7a22ba7b9dfe20dae4de0ddc92dbeb2b6e2e86e0f493a4f1e4`).
It adds `--isolate-assertions` and selects the first and last contract locations,
lines 3 and 2001. These are universal contracts for arbitrary inputs, with the
original 1,000 range preconditions; no source workaround or concrete witness is
introduced. [Paired fresh-process replays](https://github.com/erniecohen/dafny/actions/runs/37090094354)
use identical captured SMT bytes on both solvers and include numbered solver-log
files created when the prover restarts. All eight queries are UNSAT, and every
profile accounts for the full `quant-instantiations` statistic.

| Isolated query | 4.12.1 RU | 5.1.0 RU | Instances, both solvers |
| --- | --- | --- | --- |
| First contract, well-formedness | 352,410 | 352,410 | 1 |
| First contract, correctness | 123,113 | 123,256 | 3 |
| Last contract, well-formedness | 3,566,824 | 3,566,519 | 1,999 |
| Last contract, correctness | 3,287,068 | 3,287,245 | 2,001 |

First correctness has one instance each from the round-trip family, wrapper
bridge, and literal identity. Last well-formedness has 999 family, 999 bridge,
and one literal instance; last correctness has 1,000 family, 1,000 bridge, and
one literal instance. All records have generation 0 and cost 1. The final
isolated correctness VC still contains all 1,000 round trips: earlier contracts
are assumptions and only the selected contract is checked. It is therefore not
a one-variable replacement program.

This is a focused performance characterization of **two of the 1,000 contracts**
(four queries per solver), not a completed isolated proof of the whole stress
file. A full 2,000-query isolated sweep reached its 2,400-second wall-time safety
cap on each solver; its partial output is retained and further replay of that
partial sweep was stopped. It supplies no whole-file green result. The
20,000,000-RU per-query limit was not raised, and the failed grouped case is not
made into an acceptance gate or used to redesign the axiom family.


## Standing CI for the shared option (#50)

The review workflow runs the existing suite plan with the shared option both
OFF and ON under checksum-pinned Z3 5.1.0. It likewise runs the source standard
library in both modes under Z3 4.12.1 and 5.1.0. The existing OFF expectations,
program options, declaration order, resource caps and safety timeouts remain
unchanged. An ON verdict is checked against its own file:

- `.github/review/expected-verdicts-additional-axioms.tsv`;
- `.github/review/expected-std-verdicts-z3-4.12.1-additional-axioms.tsv`;
- `.github/review/expected-std-verdicts-z3-5.1.0-additional-axioms.tsv`.

Missing files, added/removed programs or declarations, and changed verdicts fail
the gate. The existing regression harness still checks the retained #33/#74
positive, negative, vacuity and local-identity isolation controls in both resolver
modes. These controls are needed alongside the suite: passing existing proofs
alone does not establish consistency.

Each suite shard retains commands, diagnostic output and per-batch JSON logs.
The resource report joins OFF and ON batches by program, declaration and VC
number, preserving unmatched entries. It retains every observed outcome/count
and shows the largest increases in the job summary. Source-library resource
counts are declaration totals in the existing runner's TSV files. They retain
the known default-order variation described above and in #30; the paired
observations do not claim exact controlled library resource equality. Costs have
no new pass/fail threshold. In particular, #78's accepted `MinimumWindowMax`
outliers remain nonblocking. No axiom, proof source or verification limit changes.

`additional_axioms_probe=true` is a workflow-dispatch input restricted to
`scratch/` branches. Only the new ON comparisons then record differences and
exit zero, retaining candidate expected files in comparison artifacts. OFF
comparisons remain strict. A successful probe is not an acceptance run: inspect
every difference, document its reason, commit ON expectations separately, and
run ordinary strict CI before proposing the change. Future snapshot changes
follow the same reviewed-verdict policy as the OFF files.
