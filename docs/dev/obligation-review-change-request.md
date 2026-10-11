# Requested revisions to PRs #168 and #169

The current PRs contain useful infrastructure and diagnostics, but the product implementation should be simplified before requesting approval.

The intended repair is **not quantifier-specific** and should not depend on terms found elsewhere in the method body. It is a local correction to how Dafny translates proof obligations.

## Governing requirement

For an implicit check of a proposition `P`, inserting an immediate source assertion

```dafny
assert P;
<construct whose implicit obligation is P>
```

must not provide essential proving support that the implicit check lacks.

The implementation should therefore follow this conceptual structure:

```text
PrepareCheck(P, local verification context)
Check P exactly once
Publish whatever the original kind of check normally publishes
```

`PrepareCheck` should provide the legitimate assertion-style support for `P`: well-formedness/can-call support, the appropriate fuel/layer treatment, splitting/inlining behavior, trigger-visible terms, heap, substitutions, visibility, SCC restrictions, etc.

It must **not**:

- scan preceding or surrounding body expressions for useful terms;
- collect arbitrary function applications from the verification context;
- generate support based on unrelated assertions or invariants;
- introduce a general new fuel/polarity policy;
- add a second proof of `P` in front of the original proof of `P`;
- weaken explicit assertions.

The check must occur at the actual program point, so current scoped `reveal`/`hide` state, heap, substitutions, and other legitimate local context remain in effect.

The later `issue100-contract-repair` experiments that collect context/body terms should **not** be incorporated into either PR.

---

# PR #168 — `review/4.11.0`

## 1. Replace the additive check-package design

The current implementation frequently does approximately:

```text
new assertion-style check of P
assume/publish summary P
old implicit check of P
```

Replace this with a **single strengthened implicit check**.

The existing implicit obligation should use the same relevant *pre-check* machinery that an explicit `assert P` would use. Do not prove `P` once in an added package merely to make the legacy proof of `P` easy.

Retaining an old formula as a second assertion is not the desired compatibility strategy. Compatibility should instead come from preserving the semantics/publication of the original check while changing how that one check is prepared/lowered.

Keep `LowerProposition` or an equivalent common helper if it remains useful, but it should lower the **actual check**, not manufacture an additional assertion in front of it.

## 2. Method postconditions

Postconditions must still be checked **inside the translated body at each return/fallthrough**, while the body's reveal scope is active. Keep the reveal-scope repair already made in this PR.

However, do not then make Boogie prove the same postcondition again as a second checked procedure `ensures`.

There should be one proof obligation corresponding to each Dafny postcondition on each exit.

A likely implementation is:

```text
body exit:
    assertion-style preparation of instantiated ensures P
    check P once, using the appropriate existing split/publication policy

procedure contract:
    retain P as the caller-visible contract,
    but do not create a second implementation proof obligation for P
```

Using a nonchecking/free Boogie `ensures` for the caller-visible copy is a plausible implementation if that has the required Boogie semantics. Verify that point with a small direct Boogie test before relying on it.

Preserve:

- inherited `$_reverifyPost` guarding;
- clause order;
- the fact that an established earlier postcondition may be used while proving later postconditions;
- custom diagnostics and proof dependencies;
- early returns as well as fallthrough;
- existing reveal/hide scope.

Avoid a separate `assume summary(P)` if ordinary assertion publication can preserve the old continuation semantics. If some summary publication really is required after a split, justify it as the normal post-check publication of the single check, not as part of a duplicated pre-check package.

## 3. Method preconditions at calls

Likewise, do not emit:

```text
new local assertion-style proof of requires P
old checked procedure-requires proof of P
call
```

There should be one caller obligation.

At the call site:

```text
instantiate P using the actual arguments and correct pre-call heap
run assertion-style preparation for P
check P once
perform the call
```

The procedure contract must continue to make `P` available as a callee assumption, but it must not cause Boogie to check `P` a second time at that call.

A free/nonchecking Boogie `requires`, or another equivalent encoding, is acceptable if it gives exactly these semantics.

Preserve receiver substitution, generic type arguments, boxing, old arguments, frame checks, termination checks, and call ordering.

Do not publish `P` to arbitrary code after the call merely because the precondition was proved; only retain facts required by the normal call semantics.

## 4. Function-call preconditions

Ordinary function precondition checking is already closer to the desired architecture.

Do not add a parallel proof package around it. Make the existing precondition assertion itself use the canonical assertion-style lowering/preparation where it currently differs.

The same applies to higher-order `.requires`.

## 5. Visible subset/newtype constraints

For a visible type

```dafny
type S = x: B | C(x)
```

do not permanently check both:

```text
assert assertion-style C(v)
assert $Is(v, S)
```

as independent obligations merely for compatibility.

Instead, the single type-membership obligation should be discharged by checking its visible defining constraint with assertion-equivalent support:

```text
check required base membership
check CanCall(C(v)) ==> C(v), using assertion-style lowering under that guard
derive/publish $Is(v,S) using the existing introduction rule
```

The symbolic `$Is` fact may still be emitted/assumed afterward where the verifier needs that representation, but it should be a **derived result of the check**, not a second logically equivalent proof obligation.

Hidden/opaque constraints must remain hidden and should continue using their abstract membership representation.

Apply the same principle to:

- function results;
- method results;
- assignments;
- constructor arguments;
- casts/conversions;
- constant/default values;
- collection/lambda results where applicable.

## 6. Cast/conversion constraints

The current "retain original guarded assertion and add assertion-style pieces" approach should be replaced.

There should be one guarded proof:

```text
CanCall(C) ==> C
```

with `C` lowered using the assertion-style checker.

After it succeeds, publish whatever symbolic/type fact the existing conversion machinery requires.

Do not keep both the old guarded assertion and a second assertion-style proof of the same condition.

## 7. Allocation checks

Do not require both the old typed allocation predicate and the explicit-assertion boxed form as two conjunctive proof obligations solely to seed triggers.

Choose the assertion-equivalent representation needed for the single check, using the existing typed box/unbox/allocation bridges. After the check succeeds, derive whichever representation downstream translation needs.

Preserve the exact heap: current, old, or labeled-old.

Negative old-allocation examples must continue to fail.

## 8. Other implicit obligations

Use the existing producer inventory to audit other user-expressible checks, including bounds, nullness, map-domain membership, destructors, division/modulo definedness, loop invariants, iterator/yield contracts, and similar checks.

Do not force all internal verifier bookkeeping through the same abstraction. The criterion is:

> if there is a source proposition `P` that a user could immediately assert to help the following implicit check of that same `P`, the implicit check should receive equivalent local pre-check support.

Document specialized internal checks that are deliberately outside this criterion.

## 9. Do not resurrect the earlier global fuel rewrite

The withdrawn occurrence/polarity/value fuel rewrite should remain withdrawn.

Use the fuel/layer behavior of the existing explicit assertion machinery at the particular implicit check being translated.

Do not redesign expression translation globally as part of this issue.

## 10. No body/context term collection

Explicitly delete or reject any proposed implementation that searches earlier commands, loop invariants, arbitrary context expressions, or body terms for applications from which to generate supporting instances.

Support for `P` must be a function of:

```text
P
+ legitimate local verifier state
  (heap, substitutions, reveal visibility, SCC/fuel context, guards, types, etc.)
```

not of arbitrary syntactic material appearing elsewhere in the proof.

## 11. Fix the structural fingerprint test

`ObligationFingerprint` currently canonicalizes bound variables by name and `bound.Count`. This is incorrect in the presence of lexical shadowing and can identify distinct formulas.

Change it to use proper lexical binder identities/stacks.

Add at least these controls:

```text
forall x :: forall x :: forall y :: x == y
forall x :: forall x :: forall y :: y == y
```

These must have different fingerprints.

Also retain a positive alpha-equivalence test where only binder names differ.

## 12. Required regression tests

Add/retain paired tests for at least:

```text
assert postcondition; return
versus
return

assert subsetConstraint(x); return x
versus
return x

assert Pre(actuals); M(actuals)
versus
M(actuals)

assert Pre(actuals); f(actuals)
versus
f(actuals)

assert bounds; a[i]
versus
a[i]

assert allocated(oldValue); twoStateCall(...)
versus
twoStateCall(...)
```

Include quantified and non-quantified examples. The implementation and analysis must not describe this as a quantifier fix.

Add a **no-body-leakage structural test**: insert unrelated pure assertions/quantified terms before an unchanged check, without changing state or visibility. They must not change the support generated specifically for that implicit check.

Add a **scope test** showing that a scoped `reveal` active at the return/call/check point is also active for the strengthened check.

Add a structural check ensuring that enabled translation does not contain both an added assertion-style proof of a contract and the old checked proof of the same contract.

## 13. Regression triage

Do not attempt product changes specifically for a case merely because one solver seed moved from success to failure if:

- the baseline itself succeeds only at some tested seeds; and
- the new implementation also succeeds at some tested seeds.

Record those as solver/resource variance rather than evidence of a translation defect.

For remaining stable regressions, first determine whether the single-check implementation removes them. In particular, rerun the examples previously attributed to exit packages, exit-summary assumptions, and duplicate call-site packages before doing any further solver-specific repair.

Do not increase resource limits or add source proof hints to make the comparison pass.

## 14. Acceptance for #168

Before requesting review:

- issue #100 original source verifies without the redundant final assertion at its existing resource ceiling;
- the subset-type reproducer verifies without its redundant assertion;
- representative method/function precondition cases verify without redundant assertions;
- all negative controls remain negative;
- scoped-reveal examples retain their previous proofs;
- default-off translation remains compatible with the `review/4.11.0` baseline;
- enabled mode has no unexplained stable correctness regression;
- remaining resource movements are classified with multiple seeds;
- no body/context scanning is present;
- no new background axiom or global fuel policy is introduced.

The PR description should be rewritten around **single-check assertion-equivalent lowering**, not around added "exit packages" or "call-site packages".

---

# PR #169 — `dev`

Do **not** independently evolve a different solution on the development branch.

First settle the simplified semantic design above on #168. Then make #169 the development-line port of the same design.

## Required changes

Apply the same single-check architecture:

- one local proof of each method postcondition, at the actual exit and reveal scope;
- one proof of each method-call precondition;
- canonical checking of visible type constraints;
- canonical allocation representation;
- no duplicate assertion package plus legacy proof;
- no body/context term collection;
- no global fuel/polarity rewrite.

Port the `ObligationFingerprint` binder fix as well.

Where `dev` has diverged from `review/4.11.0`, adapt to the current development verifier architecture rather than mechanically cherry-picking source hunks. The semantic invariant must remain identical.

## Development-specific acceptance

#169 needs its **own** native verification evidence. #168's supported-line solver results are not evidence for the development branch.

Before requesting approval on #169:

- run the paired assertion-invariance tests natively on `dev`;
- run #100 and the non-quantified subset/call cases;
- run reveal-scope controls;
- run the verifier suite and standard library under the fork's required configurations;
- classify seed-sensitive resource changes using the same policy as #168;
- confirm default-off behavior against the actual `dev` baseline;
- confirm the producer inventory against the development source;
- document any branch-specific difference in lowering or soundness arguments.

Do not port the later context-collection / definition-support experiments merely because they exist on a scratch branch.

# Completion criterion

The final implementation should be explainable in one sentence:

> Dafny now translates an implicit proof obligation using the same relevant local pre-check support as an immediate explicit assertion of that obligation, while checking the obligation only once and without consulting unrelated body terms.

If the resulting implementation cannot accurately be described that way, simplify it further before requesting approval.
