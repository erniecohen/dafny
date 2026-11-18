# Round-2 public evidence for issue 193

All RU below is from [the public run](https://github.com/erniecohen/dafny/actions/runs/43103815404), using unchanged compiler source
`428e348b0b125cb67505f03a0026b3f497f98e48`, compiler version
`4.11.0+fcb2042d.review.8e189a3e`, and immutable probe input commit
[`d81f1379d5ae346cdcaad051519feb50c482dffb`](https://github.com/erniecohen/dafny/tree/d81f1379d5ae346cdcaad051519feb50c482dffb).
The [compact receipt](public-receipt.json) retains compiler/solver hashes,
47 generated-source hashes, configuration counts, ground-query hashes and
profile results. Full generated inputs, scope/VC JSON, commands and solver
captures remain in the run's artifacts. The committed [workflow](workflow.yml)
and [generator](generate.py) reproduce the corpus; commands are in design §9.5.
No product implementation, full production-emitter measurement, or newtype
language-option matrix is claimed.

## Outcomes and proof inventory

| Solver / resolver | Positive scopes | Expected false controls | Diagnostic scopes | Unexpected / setup / unmapped |
|---|---:|---:|---:|---|
| 5.1.0 / legacy | 764 | 68 | 64 | 0 / 0 / 0 |
| 5.1.0 / refreshed | 764 | 68 | 64 | 0 / 0 / 0 |
| 4.12.1 / legacy | 764 | 68 | 64 | 0 / 0 / 0 |
| 4.12.1 / refreshed | 764 | 68 | 64 | 0 / 0 / 0 |

Each of 3,056 positive scopes is Correct. Each of 272 designated false-control
scopes ends in Errors with Invalid only at its intended false assertion; all
have complete scope RU below 200,000. The largest is the all-ten/#97 control,
175,461 RU. There are no unexpected scope, setup/log or unmapped-failure results.
The 256 diagnostic scopes include successful prerequisites and deliberately
retained resource failures; they are not acceptance proofs.

The runner uses one core, `normalizeDeclarationOrder:0`, `restartProver`, no
verification time limit, a 200,000-RU default, and an external 600-second safety
limit per file. Larger helper budgets are explicit source attributes. Included
bodies are independently verified as inputs. `pure-*` files use additional
axioms OFF; other proof files use it ON. Lambda controls additionally exercise
both settings of the heap-free option. All model runs also report PASS.

The two resolver runs have identical outcomes and identical costs in 895 of 896
scopes for each solver. The exception is the pure `smallAnds` correctness proof:
5.1.0 legacy/refreshed costs 1,759,028/1,193,817, and 4.12.1 costs
1,655,022/1,362,438. Both pass the explicit 2,000,000-RU helper cap. All client
costs and control costs are identical between the two resolver runs. This is
an observed difference between runs, not an assertion about its internal cause.

## Vacuity diagnosis and actual SAT witnesses

The profile uses the default resolver, captures the actual first solver query,
and adds observational profiling/reason/statistics requests to its intact prefix.
The extra qid/skolemid labels preserve verdicts and summary statistics in every
capture. These first-query RU values are distinct from a complete Dafny scope's
RU, which can include later prover/model activity.

| Captured first query | Solver | Instantiations | Max generation | RU | Raw result |
|---|---|---:|---:|---:|---|
| transparent | 5.1.0 | 239 | 27 | 205,272 | unknown: canceled |
| opaque | 5.1.0 | 20 | 2 | 7,359 | unknown: (incomplete quantifiers) |
| actual-A64 | 5.1.0 | 22 | 2 | 11,445 | unknown: (incomplete quantifiers) |
| transparent | 4.12.1 | 229 | 27 | 205,272 | unknown: canceled |
| opaque | 4.12.1 | 20 | 2 | 7,322 | unknown: (incomplete quantifiers) |
| actual-A64 | 4.12.1 | 22 | 2 | 11,423 | unknown: (incomplete quantifiers) |

All six captures explicitly disable MBQI. The transparent and opaque width-eight
queries contain no bitvector sort. The trace shows that quantified recursive/fuel/literal/arithmetic interaction
reproduces the failure without any width-64 bit-blasting and with MBQI disabled. It does not prove an infinite matching loop. Opacity turns
resource exhaustion into ordinary Dafny rejection, but the raw opaque result
remains unknown/incomplete quantifiers. **Errors/Invalid is not SAT.**

The separate finite model reads every actual bridge assumption and retains all
24 raw/composed instances in the combined case, including guards and both shift
counts. It keeps real native SMT operations and checked reference values. It
returns actual SAT for each slice and UNSAT for both A+1 mutants:

| Finite slice | Result | RU 5.1.0 / 4.12.1 |
|---|---|---:|
| control_A64 | SAT | 249 / 237 |
| control_C64 | SAT | 80 / 68 |
| control_L64 | SAT | 692 / 680 |
| control_R64 | SAT | 754 / 868 |
| control_add64 | SAT | 98 / 86 |
| control_sub64 | SAT | 106 / 94 |
| control_mul64 | SAT | 98 / 86 |
| control_div64 | SAT | 106 / 94 |
| control_mod64 | SAT | 106 / 94 |
| control_le64 | SAT | 90 / 78 |
| control_combined97_64 | SAT | 1,628 / 1,741 |
| mutant_A64 | UNSAT | 244 / 244 |
| mutant_combined97_64 | UNSAT | 1,419 / 1,462 |

These 11 SAT and two UNSAT results repeat in both resolver jobs for each solver;
the ground formulas themselves are resolver independent. The largest ground
query is 1,741 RU. The lambda part here is the intended source observation at
t=0, not the full quantified #97 implementation. The complete Dafny lambda
fixture separately exercises that implementation. Pure certificates and the
mathematical model extend the ground values to the hidden recursive references.
No SAT claim for the entire Dafny prelude or arbitrary opaque UF models follows.

## Client costs from the minimal basis

Every cell below is **5.1.0 / 4.12.1**, summed over a client's well-formedness
and correctness scopes. Both resolvers give these numbers. OR/XOR/ANDNOT use
integer inputs; the bit/rotation integer-input wrappers visibly use
`isolate_assertions`. No derived-operation contract is assumed.

| Integer-input client | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| OR | 19,705 / 19,693 | 20,713 / 20,701 | 22,729 / 22,717 | 26,761 / 26,749 |
| XOR | 21,671 / 21,659 | 22,677 / 22,665 | 24,695 / 24,683 | 28,733 / 28,721 |
| ANDNOT | 19,367 / 19,355 | 20,373 / 20,361 | 22,391 / 22,379 | 26,429 / 26,417 |
| Bit value | 39,593 / 39,472 | 43,618 / 43,305 | 50,587 / 49,890 | 66,537 / 65,216 |
| Rotate left | 27,770 / 27,786 | 29,958 / 29,990 | 34,342 / 34,406 | 43,110 / 43,238 |
| Rotate right | 27,770 / 27,786 | 29,958 / 29,990 | 34,342 / 34,406 | 43,110 / 43,238 |

The ordinary native-word callers are cheaper:

| Native-word client | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| Bit value | 15,156 / 14,996 | 18,303 / 17,952 | 24,386 / 23,651 | 37,720 / 36,657 |
| Rotate left | 13,509 / 13,513 | 14,037 / 14,057 | 15,093 / 15,145 | 17,205 / 17,321 |
| Rotate right | 13,509 / 13,513 | 14,037 / 14,057 | 15,093 / 15,145 | 17,205 / 17,321 |

The maximum measured integer-input caller cost is 66,537 RU. The twelve unsplit
integer-input bit/rotation callers exhaust in both solvers at every width:
correctness RU is 204,217 for bit and 205,413 for each rotation. Their preceding
well-formedness checks succeed. These are retained ablations, not passing proofs;
isolation is an explicit annotation dependency of the successful wrapper.

## Once-only construction costs

These declaration totals also include well-formedness. They exclude dependencies
shown separately below. For XOR, OR's construction is a dependency; clients
call the checked contract and do not replay either body.

| Adapter declaration, excluding its separately proved dependencies | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| OR | 385,133 / 374,083 | 431,383 / 420,941 | 507,635 / 514,217 | 779,276 / 775,993 |
| XOR | 506,590 / 505,021 | 533,837 / 533,998 | 596,382 / 592,747 | 716,094 / 716,153 |
| ANDNOT | 247,218 / 247,563 | 276,614 / 277,055 | 353,363 / 353,687 | 575,306 / 575,738 |
| Bit value | 30,332 / 28,459 | 40,775 / 36,196 | 63,108 / 52,324 | 102,614 / 91,986 |
| Rotate left | 695,578 / 837,639 | 556,549 / 1,074,850 | 794,735 / 1,319,841 | 1,323,785 / 2,759,806 |
| Rotate right | 440,603 / 494,336 | 547,323 / 1,039,697 | 1,457,627 / 1,594,243 | 1,307,541 / 1,951,203 |

The native variable-count rotation helpers use a complete finite case split:

| Native variable-count helper | 8 | 16 | 32 | 64 |
|---|---:|---:|---:|---:|
| Left | 612,150 / 605,468 | 1,481,150 / 1,470,959 | 4,508,109 / 4,505,445 | 16,503,535 / 16,733,258 |
| Right | 612,150 / 605,468 | 1,481,150 / 1,470,959 | 4,508,109 / 4,505,445 | 16,503,535 / 16,733,258 |

At width 64 these named native helpers have largest VCs 448,692/445,643 RU
(5.1.0/4.12.1), despite totals above 16 million. Each additionally uses 65
literal-count native lemmas; their real proofs and count/cast/power helpers are
also checked. Native Boolean helpers prove De Morgan and XOR identities with
additional axioms OFF. No native identity or disjointness contract is assumed.
The largest 64-bit derived-rotation VC in these runs is 1,356,957 RU, within
its visible 2,000,000 limit. A 200,000 client ceiling is not a promise that all
support construction fits the same aggregate budget.

| Independently verified support file (AA OFF) | 5.1.0 legacy / refreshed | 4.12.1 legacy / refreshed |
|---|---:|---:|
| pure-boolean-reference.dfy | 2,539,615 / 1,974,404 | 2,488,466 / 2,195,882 |
| pure-boolean-native.dfy | 145,161 / 145,161 | 145,161 / 145,161 |
| pure-math.dfy | 1,522,099 / 1,522,099 | 1,478,672 / 1,478,672 |
| pure-certificates64.dfy | 392,188 / 392,188 | 391,228 / 391,228 |

Support is shared. The expensive pure `smallAnds` theorem certifies concrete
control values, so it is a one-time control-construction cost, not ordinary
caller cost. Do not sum each dependency once per client or pretend the support
proofs are free. The source-level derivations are conditional on only A/C or
A/R or L/R/+ as appropriate, plus the already present native/round-trip theory.
They justify the basis, not the future emitter's implementation.

## Reproduction and limitations

Run `python3 docs/dev/design/193-evidence/models/run_all.py` for all Python
checks; it uses only the standard library. Its results exhaust widths 1–8 and
sample 100,000 independent pairs at each of 16/32/64, with endpoint counts and
boundary cases. See the main design for guard counterexamples and nonminimality.
Run the common generator and runner as shown in design §9.5; rerun both solvers
and both resolver modes, keeping the independent pure support files selected.
Inspect actual outcome fields; a green diagnostic workflow alone is insufficient.

The earlier round-2 setup attempt failed before producing proofs; the corrected
control/model run and this full run are the evidence. Historical numbers at
62bae981 remain separately labelled. No full-suite/Std feature delta or both-off
product identity has been measured for an unimplemented feature. Those gates,
actual-emitter controls, and the new fixed-definition mechanism remain explicit
future milestones.
