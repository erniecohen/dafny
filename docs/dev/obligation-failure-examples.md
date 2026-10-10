# Obligation failures and reveal-scope repair

The single-check replacement's original-exit and initializer failures, causal
Boogie experiments and subsequent local preparation repair are documented in
[the current diagnosis](obligation-local-preparation-diagnosis.md). The material
below remains historical.

These findings describe the superseded additive implementation. They are retained
for regression triage and do not validate the single-check replacement. Current
source and evidence are tracked in [the validation record](obligation-validation.md)
and [revision checklist](obligation-revision-checklist.md).

These examples were diagnosed in the preceding exit-publication product,
identified in [obligation-suite-comparison.md](obligation-suite-comparison.md).
The current correction removes both opt-in `ReturnPosition=false` overrides,
preserving the legacy method and forall proof-body reveal scope. The unchanged
Filter, Split and Base64 declarations now verify natively with the option on,
with both additional-axiom settings and their original resource ceilings.
Complete unchanged standard-library runs confirm all three repairs. The
baseline and default-off candidate retain identical outcomes and named batch
resource counts in the focused comparison. No extra reveal was added to the
library source.

The examples below retain the original failures and causal diagnostics for
review. The quantified power exit-package failure remains reproduced. Complete
comparisons of the current correction are recorded separately in
[obligation-validation.md](obligation-validation.md).

The source contracts, library inputs, resource ceilings and expected verdicts
were retained. These are failures to establish valid library obligations, not
demonstrations that their contracts are false. The complete comparison also
records every new resource exhaustion and the formerly resource-limited
declarations that succeed; those are separate from the four proof-error cases.

## Filter concatenation

`Source/DafnyStandardLibraries/src/Std/Collections/Seq.dfy:846-871`,
`LemmaFilterDistributesOverConcat`:

```dafny
ensures Filter(f, xs + ys) == Filter(f, xs) + Filter(f, ys)
```

The nonempty branch uses a calculation with `reveal Filter` in hints. Native
diagnostics identify the calculation steps at lines 861 and 867. Both were
already checked in the baseline.

The enabled Boogie introduces pushes and pops around the hints. At the first
step, two pops occur after `reveal Filter` and before the existing calculation
check. The baseline has no scope commands in this implementation. This changes
the definition visibility at the check even though the checked formula and its
literal fuel arguments are preserved.

A native diagnostic adds only `reveal Filter;` immediately after the method's
`hide *`. This keeps the definition visible in the outer proof scope. Both
original failed steps and the declaration then verify, with all checks and
the original resource ceiling retained.

## Splitting a sequence at a delimiter

`Source/DafnyStandardLibraries/src/Std/Collections/Seq.dfy:703-720`,
`WillSplitOnDelim`:

```dafny
ensures Split(s, delim) == [prefix] + Split(s[|prefix| + 1..], delim)
```

The proof hides definitions and reveals `Split` in the first calculation hint.
Native diagnostics identify the calculation step at line 714. Enabled
translation adds scope pops between that reveal hint and the check; the
baseline has no scope commands in this implementation.

Adding only `reveal Split;` immediately after the method's `hide *` restores
native success. No contract, assertion or resource ceiling is changed.

## Recursive Base64 encoding followed by decoding

`Source/DafnyStandardLibraries/src/Std/Base64.dfy:370-399`,
`EncodeDecodeRecursively`:

```dafny
requires |b| % 3 == 0
ensures (EncodeRecursivelyBounds(b); DecodeRecursively(EncodeRecursively(b)) == b)
```

The proof uses `hide *`, calculation hints revealing `DecodeRecursively` and
`EncodeRecursively`, and a recursive call on the remaining bytes. Native
diagnostics identify calculation steps at lines 388 and 392 and the function
precondition at line 392. Enabled translation adds scope pops after the reveal
hints and before existing checks. The baseline has no scope commands in this
implementation.

Adding only `reveal DecodeRecursively, EncodeRecursively;` immediately after
the method's `hide *` restores native success, including all three original
failed checks. No contract, assertion or resource ceiling is changed.

The preceding enabled product also reported corresponding failures in the
inverse `DecodeEncodeRecursively` at lines 416 and 420. Its baseline was already
OutOfResource. The current reveal-scope correction verifies that unchanged
declaration in both complete additional-axiom settings.

## Quantified power subtraction

`Source/DafnyStandardLibraries/src/Std/Arithmetic/Power.dfy:222-234`,
`LemmaPowSubtractsAuto`:

```dafny
ensures forall b: nat, e1: nat :: b > 0 ==> Pow(b, e1) > 0
ensures forall b: nat, e1: nat, e2: nat {:trigger Pow(b, e2 - e1)}
          :: b > 0 && e1 <= e2 ==>
               Pow(b, e2 - e1) == Pow(b, e2) / Pow(b, e1) > 0
```

The body calls `LemmaPowPositiveAuto`, then uses a forall statement to call
`LemmaPowSubtracts` under its preconditions. Native diagnostics identify the
second quantified postcondition at line 224 on the method return. This case
contains no hide/reveal commands. The original checked procedure ensures and
their fuel-indexed triggers are retained; enabled mode adds local exit checks
and summary assumptions.

Controlled Boogie bisection reproduces failure in the retained original second
postcondition. Removing only the added exit package restores success. Removing
only its two summary assumptions does not restore success, and substituting
the baseline body while retaining the added exit package still fails. Removing
the new scope commands has no effect. Keeping only the first clause's exit
package succeeds, while keeping only the second clause's package fails. Moving
both can-call assumptions before the checks, or using checked-fuel terms in both
summaries, does not repair the failure. These observations isolate the second
clause's exit-package interaction; they do not yet identify the precise solver
instantiation that fails. They are not evidence of less fuel or a removed
original formula.

## Representative resource failures

The full table in [obligation-suite-comparison.md](obligation-suite-comparison.md)
names all new resource failures. Every such run exhausts the original ceiling;
none is accepted by increasing that ceiling. The following smaller examples were resource failures in the preceding
complete product and remain useful starting points for diagnosis. Their current
verdicts are recorded separately in the complete comparison; `dec` now verifies
in both settings:

* `dafny0/NoTypeArgs.dfy`, `Lemma<A>`, proves
  `reverse(concat(xs, ys)) == concat(reverse(ys), reverse(xs))`. Its match
  branches assert quantified induction facts for concatenation identity and
  associativity. It succeeds in the baseline and exhausts its resource ceiling
  with the feature enabled, under both additional-axiom settings.
* `dafny4/NumberRepresentations.dfy`, `dec`, returns a skew representation of
  the input value minus one. It has three short branches: produce `[-1]`,
  decrement the first digit, or recurse on the remaining digits and prepend
  the borrowed digit. Its unchanged postcondition combines representation
  validity with `eval(b, base) == eval(a, base) - 1`. It exhausts its resource
  ceiling with the feature enabled under both settings.
* `dafny4/Primes.dfy`, `Composite`, returns factors with a prime first factor.
  The proof calculates a proper divisor witness, chooses it, and recurses
  when that divisor is composite. It exhausts its resource ceiling with the
  feature enabled under both settings.

The preceding complete comparisons did not establish controlled causes for
these resource failures. The focused native investigation below now localizes
the added-package boundaries for the six remaining suite examples; precise
instantiation chains and product corrections remain open.

## Scope diagnosis and remaining limits

The source origin of the extra scope commands is the enabled method-body
`ReturnPosition=false` override in `BoogieGenerator.Methods.cs`. It propagates
through terminal branches and calculation hints. `TrStmtList` emits scope
commands when that context is false. Boogie's scope commands restore hide/reveal
state on pop, and its pruning analysis uses that state to select function axioms.
The same checked expressions and fuel indices therefore do not imply the same
available definition axioms.

The first scope-removal replay does not reproduce the native Base64 failure:
baseline, enabled and scope-removal copies all verify after reparsing. The
Boogie axiom object has an in-memory `CanHide` property that its emitter does
not serialize; the parser defaults that property to false. Replaying printed
Boogie therefore does not preserve native hide/reveal pruning. The native
baseline/enabled controls reproduce all four complete-run declaration outcomes
and resource entries on both native platforms. The three explicit-reveal
witnesses restore native success while retaining all contracts and checks.
These witnesses, the existing-check locations and the source pruning rules
identify definition visibility as the cause of the three hide/reveal failures.
They are disposable proof diagnostics, not proposed library edits. The product
must preserve the original scope environment rather than require these extra
reveals in previously successful source.

The second quantified power exit package remains a separate regression. Its
precise solver instantiation cause is not yet established. The captured
second-postcondition formulas in the original goal, added check and its ordinary
publication differ only in bound-variable names. Making those names identical
in the captured solver input does not repair the failure, so that proposed
explanation is refuted. The raw solver replay returns an incomplete-arithmetic
unknown result; this generic reason does not identify the missing instantiation.

Native per-check declaration capture confirms the defining `Filter` axiom is
present at both original calculation checks in the baseline, absent at both
failed enabled checks, and present again when the outer reveal is retained:

| Original calculation line | Baseline | Preceding enabled | Outer-reveal witness | Current enabled |
| --- | --- | --- | --- | --- |
| 861 | axiom present; Valid | axiom absent; Invalid | axiom present; Valid | axiom present; Valid |
| 867 | axiom present; Valid | axiom absent; Invalid | axiom present; Valid | axiom present; Valid |

The witness adds one source line, so its corresponding check locations are
862 and 868. The actual pruned declarations, not just unpruned printed Boogie,
are used for this comparison.
All diagnostic copies retain the original checked
procedure ensures; variants removing added checks are for diagnosis only.
Reparsed Boogie observations do not establish native product acceptance. The
current product removes both method and forall proof-body context overrides;
native pruned captures confirm the defining axiom remains available at both
original Filter checks under both additional-axiom settings. The regression
suite also covers terminal forall/nested calculation reveals and ordinary
nonterminal scope boundaries under both resolvers. Expected verdicts are unchanged.

## Library resource variation after the correction

The complete comparison also records `Arrays.BracketedToArray` exhausting its
ceiling with additional axioms off, while verifying with them on. A filtered
native comparison of the preceding and current enabled products verifies the
unchanged declaration in all four observations. The captured solver streams
contain the same formulas; the only textual difference moves the same existing
`TWO_TO_THE_32` constant axiom among the other axioms. This control does not
reproduce the complete-run failure or establish its exact cause.

With additional axioms on, the fresh baseline proves
`Objects.BracketedToObject`, while the default-off candidate exhausts its
ceiling and matches the committed expectation. All emitted operational Boogie
matches between baseline and default-off. The runner already documents resource
variation from its default declaration order and calls for a repeat before
assigning a near-limit verdict change to the product. The unchanged complete repeat returns that baseline declaration to the
committed OutOfResource result, matching default-off. Complete operational
Boogie is identical across both baseline/off runs. This confirms baseline
variation without a source or binary change; controlled default-off library
cost remains unaccepted.

The separate quantified power regression and resource regressions remain open.

## Focused native diagnosis of the six suite regressions

The current product was compared with the baseline on the unchanged complete
files below, under both additional-axiom settings and native solver seeds 0,
1 and 7. The original resource ceilings, assertion batching, declaration-order
policy, fuel and reveal scopes were retained. No filtered source reconstruction
or printed-Boogie replay substitutes for these native controls.

The legacy assertion formulas are retained exactly in five cases. In
`M2.SchorrWaite`, the changed allocation checks retain each original predicate
and conjoin an explicit boxed-allocation predicate. The scope commands are
retained. Native pruned captures retain the same axiom inventories, including
their trigger formulas, for the five single-batch targets. There is no evidence
here of a removed original check, reduced original fuel or lost definition
axiom. `MinimumWindowMax` is already isolated in the source: its failure is the
existing maintained loop invariant at line 65, with its original per-batch
resource ceiling, rather than one of the newly inserted postcondition checks.

A separate [scratch native diagnostic build](https://github.com/erniecohen/dafny/actions/runs/37554159139)
omits only identified additions. With no omission, its complete operational
Boogie and target assertion-batch outcome/resource vectors match the current
product under both axiom settings. This establishes the control before
interpreting each omission. The original source assertions and checked
procedure contracts remain in the following safe diagnostic variants:

| Unchanged source and target | Safe omission that restores the target | Important negative control |
| --- | --- | --- |
| `dafny0/NoTypeArgs.dfy`, `Lemma` | Complete added method-exit package | Omitting only emitted exit summaries does not restore native verification |
| `dafny1/ExtensibleArrayAuto.dfy`, `ExtensibleArray.Set` | Complete added method-exit packages | Omitting only exit summaries or only call-site packages does not restore the target |
| `dafny1/SchorrWaite-stages.dfy`, `M2.SchorrWaite` | Only the new exit-summary assumptions; all old and new checks remain | Removing whole exit packages or only new boxed-allocation conjuncts does not restore the target |
| `dafny4/FlyingRobots.dfy`, `FormArmy` | Complete added call-site precondition packages; original call checks remain | Removing whole exit packages or their summaries does not restore the target |
| `dafny4/Primes.dfy`, `Composite` | Added exit and call-site packages together | Either family alone still exhausts the original ceiling; allocation omissions do not change that result |
| `dafny2/MinWindowMax.dfy`, `MinimumWindowMax` | Complete added call-site precondition packages, with additional axioms off | Exit-package, summary and allocation omissions do not restore that target; it already verifies with additional axioms on |

These results localize particular additions to particular resource regressions.
They do not propose deleting those additions from the product, establish a
uniform improvement, or certify the entire files: existing resource failures
in other declarations remain visible in the complete-file captures. No
expected verdict or source contract was changed.

The matched seed comparison shows baseline sensitivity as well. `NoTypeArgs`,
`SchorrWaite` and `Primes` have baseline resource failures at another tested
seed, and `FlyingRobots` fails at both other tested seeds. The enabled
`ExtensibleArray.Set` target verifies at the other two seeds. The enabled
`NoTypeArgs` and `Primes` targets also verify at one of them. `MinimumWindowMax`
has a resource-sensitive baseline in both axiom settings; the enabled target
verifies at every tested seed with additional axioms on. The original seed-0
regressions are reproduced, but these observations refute treating the failures
as seed-independent semantic impossibility.

### NoTypeArgs solver-input diagnosis

Before the appended exit package, the original `Lemma` body, quantified
assertions, trigger sets and fuel are unchanged. The package introduces
preparation permissions, a duplicate checked postcondition and a lower-fuel
summary assumption. The original checked procedure ensures remains.

Replaying the captured native SMT reproduces the original control resource
entries. Replacing only the one newly appended concrete summary term with
`true` restores the proof while retaining every original and additional
checked formula. Profiling shows a large reduction in quantifier instances,
without changing the maximum instantiation generation. This establishes a
causal contribution of that summary to search in the captured VC.

That conclusion must retain its boundary: suppressing summary emission during
native translation does **not** repair this case, while removing the complete
new exit package does. Replacing the summary's fuel argument in the existing
SMT also does not repair it, although regeneration through printed Boogie can
succeed. VC construction, optimization or ordering therefore remains part of
the unresolved explanation. The exact instantiation chain is not established,
and this is not evidence that an original check was given less fuel.

### Diagnostic trust boundary

Reparsed Boogie controls do not faithfully reproduce every native failure.
The enabled `ExtensibleArray.Set` and axiom-off `MinimumWindowMax` failures
disappear in those controls. Even an apparently matching replay control does
not establish that an ablated replay preserves the native checking environment.
The native omission results above take precedence over those exploratory
replays.

An omission that deletes new exit checks but leaves their summary assumptions
can make those summaries unchecked premises before the final procedure
ensures. Such successes are excluded from the safe-omission table: retaining
the old assertion syntax alone would not preserve the proof boundary. The
instrumentation is scratch-only and must not be merged into the product.

The added-package boundaries are now localized for all six examples. Precise
solver instantiation chains and a correction preserving the intended extra
checking power remain unresolved. Complete enabled acceptance remains open.

AI assisted the investigation.
