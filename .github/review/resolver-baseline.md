# Initial resolver baseline

This separate baseline was recorded by [diagnostic probe 37059726694](https://github.com/erniecohen/dafny/actions/runs/37059726694),
on `e1cf0de002fe55024bbe318c32031f0f28ded6e2`, based on shipped commit
`23325d9942cceca0e2e380dae2ece827d91dd669`. No Dafny product file changed.

The 48 explicit cases cover 46 source files, each in both modes: **45 resolution
acceptances and 51 ordinary rejections**, with no crashes, timeouts, or unexpected
exits. Acceptance means resolution only, not successful verification.

Full diagnostics remain in `expected-resolver-results.json`. The reasons below
cover every case. Mode-specific wording/order is intentional; the snapshot does
not assert that all existing messages are ideal. The verifier and standard-library
expected-verdict files were not changed.

| Case | Legacy | Refreshed | Reason |
|---|---|---|---|
| `DecreasesTo2` | rejected | rejected | Malformed decreases-to syntax is rejected (10 parse errors). |
| `DecreasesTo4` | rejected | rejected | Bindings and ghost components forbidden in decreases-to expressions are rejected (6 parse errors). |
| `GhostPrint` | accepted | accepted | The source ghost-print program resolves; print/reparse behavior remains in Lit. |
| `ImplicitTypeParamPrint` | accepted | accepted | The implicit type-parameter source resolves; serialization remains in Lit. |
| `ScientificNotationErrors` | rejected | rejected | Malformed numeric notation is rejected (23 parse errors). |
| `Simple` | accepted | accepted | The general syntax sample resolves with its existing refinement and old-expression warnings. |
| `incompatibleAttributes` | rejected | rejected | Combining rlimit and resource_limit attributes is rejected. |
| `parse-errors` | rejected | rejected | Invalid let/comprehension/type syntax is rejected (9 parse errors). |
| `parse-errors2` | rejected | rejected | Invalid relational operators and ambiguous implication syntax are rejected (4 parse errors). |
| `git-issue-1618` | accepted | accepted | The opaque function and reveal statement resolve. |
| `git-issue-1637` | rejected | rejected | The function argument result type is incompatible; both resolvers reject it with their existing variance wording. |
| `git-issue-1665` | accepted | accepted | The match statements and assign-or-return expressions resolve; printer/verification comparison remains in Lit. |
| `git-issue-1966` | rejected | rejected | Two modules with the same compile name are rejected. |
| `git-issue-2200` | rejected | rejected | Calling a one-argument function with two arguments is rejected; wording differs between resolvers. |
| `git-issue-2507` | rejected | rejected | An assert without its required terminator is rejected. |
| `git-issue-2550` | rejected | rejected | An unqualified outer-module type name is rejected in the nested scope. |
| `git-issue-2830` | rejected | rejected | Obsolete function-method and predicate-method declarations are rejected under function syntax 4. |
| `git-issue-3304` | rejected | rejected | An underspecified nested-sequence type is rejected; refreshed diagnostics include the inferred type. |
| `git-issue-3382` | accepted | accepted | The match expression returning a numeric newtype resolves. |
| `git-issue-3390` | rejected | rejected | Mixing incompatible bitvector widths is rejected; diagnostic wording differs between resolvers. |
| `git-issue-3461` | rejected | rejected | Illegal opaque modifiers and malformed declarations are rejected (7 parse errors). |
| `git-issue-3497` | accepted | accepted | The no-binder forall resolves with its existing deprecation warning. |
| `git-issue-3757` | rejected | rejected | A const declaration without an identifier is rejected. |
| `git-issue-3757a` | rejected | rejected | A malformed const declaration is rejected (2 parse errors). |
| `git-issue-442` | accepted | accepted | A method with ensures false resolves: resolution acceptance does not imply verification success. |
| `ForbidNondeterminismResolve` | rejected | rejected | The explicit determinism policy rejects the intended nondeterministic constructs (15 resolution errors), retaining its deprecation warning. |
| `git-issue-2959` | rejected | rejected | The explicit determinism policy rejects the nondeterministic branch and assignment. |
| `Bug142` | accepted | accepted | The program resolves, retaining all six requested shadowing warnings. |
| `git-issue-3496` | accepted | accepted | The assumption source resolves, retaining the requested missing-axiom warning. |
| `PrecedenceLinter` | accepted | accepted | The program resolves, retaining the existing indentation diagnostics. |
| `PrecedenceLinter.ignore-indentation` | accepted | accepted | The same source resolves with indentation warnings explicitly disabled by its later-RUN option. |
| `ReadsOnMethods_Printing.reads-false` | accepted | accepted | The source resolves with reads-clauses-on-methods disabled; printed output remains in Lit. |
| `ReadsOnMethods_Printing.reads-true` | accepted | accepted | The source also resolves with the later-RUN reads-clauses-on-methods option enabled. |
| `github-issue-25` | accepted | accepted | The fixed label/refinement regression resolves in both modes. |
| `github-issue-27` | accepted | accepted | Abstract imports with extreme declarations resolve without spurious prefix duplicates. |
| `github-issue-37` | rejected | rejected | Incompatible datatype-constructor function arguments remain rejected; each resolver's current type-name/variance diagnostic is recorded. |
| `github-issue-40` | accepted | accepted | The assign-such-that bounded-pool cloning regression resolves without a crash. |
| `github-issue-41` | accepted | accepted | The unresolved-class type-cloning regression resolves without a crash. |
| `github-issue-42` | accepted | accepted | Valid decreases-to uses resolve without a crash. |
| `github-issue-43` | rejected | rejected | All 16 intended bad destinations/RHS names are diagnosed; mode-specific diagnostic order is retained. |
| `github-issue-44` | rejected | rejected | All seven explicit type cycles are rejected normally. |
| `github-issue-45` | accepted | accepted | Inferred constants that require datatype constructor signatures resolve without a crash. |
| `github-issue-46` | rejected | accepted | Refreshed mode resolves the bitvector-newtype/static-receiver regression with general-newtypes enabled; legacy mode exercises its ordinary unsupported-newtype diagnostic without an incompatible CLI flag. |
| `github-issue-42-errors` | rejected | rejected | The two intended non-ghost uses of ghost/decreases-to expressions are rejected. |
| `github-issue-27-duplicates` | rejected | rejected | The two genuine duplicate declarations remain rejected. |
| `github-issue-43-valid` | accepted | accepted | The valid destinations and RHS expressions resolve. |
| `github-issue-44-acyclic` | accepted | accepted | Acyclic redirecting types resolve. |
| `github-issue-46-range` | rejected | rejected | The two out-of-range bitvector literals are rejected without requiring general-newtypes. |

## Probe review and limitations

The first probe recorded an invalid flag combination in the two legacy issue 46
rows. Those records were **not adopted**. The corrected manifest uses
`--general-newtypes:true` only for the refreshed static-receiver case, and no
such flag for the bitvector-literal control. The second probe changed exactly
those two diagnostics; its other 94 records matched the first probe byte for byte.

The manifest deliberately runs ordinary raw `resolve` commands; it does not
inherit the `%resolve` macro's refreshed-only feature defaults or reproduce
print/reparse pipelines. All rejections were inspected: apart from the explicitly
documented legacy bitvector-newtype restriction, they exercise intended source
diagnostics rather than incompatible feature-option combinations. Complete Lit
RUN sequences and the issue 38/39 solver negatives remain in the regression job.

The separate inferred-newtype-cycle probe (`git-issue-2134.dfy`, issue 44 part b)
reproduced a 15-second legacy timeout and ordinary refreshed rejection. It is
not in this strict baseline. The timeout is reported in the probe summary and
artifact; it is not a passing resolver result.
