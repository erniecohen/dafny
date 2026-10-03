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
large and excluded arguments and independent module inventories.
The issue validation workflow records measurements and actual Boogie/SMT inputs.
Its baseline is the green shipped build at `1d45201a027d46be24988fe0946c0bde80a702c9`.
The retained public artifacts contain per-VC results and commands.

### Solver boundary and stress limits

With the closed literal identities enabled, the complete unchanged original
passes end to end on pinned Z3 4.12.1 and 5.1.0 in both resolver modes at
200,000 RU. Before the literal follow-up (#74), the #73 encoding still exhausted
the cap on 4.12.1 for `ShiftRightByZero`. The actual follow-up translation emits
`LitInt(0) == 0` through the existing function-definition dependency, without
changing pruning or the approved composite trigger. The earlier limitation is
therefore resolved for this reproducer; arbitrary indirect goals have no general
proof-cost guarantee. The existing library/regression gates retain their
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

The translator inspects each function-call actual after boxing adaptation.
Only an argument exactly `LitInt(n)`, with `n` a native integer numeral, is
collected. It records the arbitrary-precision integer value in a numerically
sorted set owned by that module's translator. After translating the module, it
emits each closed identity once. The original actual and all existing legality
and well-formedness checks remain intact.

`AddOtherDefinition` associates these instances with the existing `LitInt`
function, just as it associates other definitions with their functions. Ordinary
pruning consequently retains the instances when `LitInt` is used. Collection
and emission both test the shared option before allocating new nodes; the
collection is allocated only on the first eligible argument. With the option
off, neither the Boogie translation nor controlled solver input/resources change
relative to merged #73 (`edff862a3da391fe2f0142343a5846edf078b2ed`).

These are substitutions in a trusted definition, not a new quantified family.
The translation excludes symbolic arguments, real literals and boxed literals.
It preserves markers, the bounded round-trip formula, its composite trigger,
the native bridge and all solver settings. Numerals occurring only outside
function-call actuals are not collected. The exact end-to-end cost need not equal
the earlier edited-query replay's 6,657 RU.

The issue-74 validation workflow compares the merged baseline and candidate with
the option off and on, records per-batch costs, saves actual SMT, and verifies
literal-heavy non-bitvector code. Its expected failing negative/vacuity probes
are recorded separately from the required regression gate.

### Literal follow-up acceptance boundary

The module-wide literal implementation is not yet accepted for merge. Its
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

Ordinary pruning retains a closed `LitInt(n) == n` whenever `LitInt` is relevant:
the axiom's expression itself produces that dependency. Attaching it to another
function's definitions would not remove the incoming `LitInt` dependency. The
current module-wide identities can therefore reach proofs with no eligible
literal call of their own. A proposed repair is to expose closed identities
through existing call-local assumptions, preserving original arguments/checks
and both resolver modes. That changes the approved module-emission scope and
requires owner review before implementation. It is not implemented here. No
quantified family, trigger, pruning algorithm, solver setting or resource limit
has been changed to make these comparisons pass.

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
