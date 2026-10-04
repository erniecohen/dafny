# Conservative monomorphic map normalization

This P4 slice rewrites direct typed reads of owned store expressions and abstracts one exact observation-equality form. The repairs are source-only until compiled and executed on the exact package. Their correspondence argument is not a formal proof of the normalizer.

## Scope and identity

For closed monomorphic maps, the normalizer uses:

```text
select(store(m,i,v),j) =
  if (AND over k: i[k] == j[k]) then v else select(m,j)
```

The empty conjunction is true. Nested owned store expressions are peeled recursively, and every coordinate participates. The normalized program has zero global ROW axioms. Maps retain opaque sorts and ordinary equality. There are no native arrays, extensionality, reverse casts, store idempotence/commutativity or lambda equations.

Only a validated store expression constructed by this normalizer supplies store provenance. Reads through variables or ordinary source functions remain uninterpreted. Earlier assignments are never substituted or guessed: after `n := m[0 := 1]`, the source-valid check `n[0] == 1` may remain unproved. A state-aware local-instantiation pass needs separate review. Reviewed identity projection may expose an owned store expression; this rewrite inspects no hidden definition.

Complete resolved signatures are checked. Polymorphic map operations retain uninterpreted signatures. Estimates bound generated nodes before allocation, and the final traversal counts copied occurrences and rejects excessive size/depth without partial IR.

## Source evidence and model interpretation

The reference package is Boogie `3.5.5-review.37e4435d`, source `73a0e214a87df85fc058270268c1d0706fd05bc9`. Its Arguments encoding generates:

```text
R0: select(store(m,i,v),i) = v
R1[k]: i[k] = j[k] OR select(store(m,i,v),j) = select(m,j)
```

All coordinate equalities imply the stored value by congruence and R0. Otherwise one coordinate differs, and that R1 clause implies the original read. These exhaustive cases prove the expression identity for every source map, tuple and value, including nullary tuples. [Pinned equations](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L298).

Interpret opaque sorts and ordinary functions by their source carriers and interpretations. Primitive coordinates use canonical encoding, whose forward decoder is a left inverse; encoding therefore preserves equality and inequality. Primitive results decode to the source value. No unguarded reverse cast law is required. [Argument canonicalization](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L604), [forward cast](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasure.cs#L175), [reverse cast omission](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L48).

Every rewritten expression agrees with its source expression under this interpretation. Remaining unconstrained operations enlarge the model class: normalized validity implies reference validity in the intended direction, while normalized countermodels need not extend to the omitted prelude.

Independently, maps may be total functions paired with arbitrary identity tags. Select reads the function and store updates it while retaining a tag. Equal reads do not force equal tags or map identities. This consistency model explains the nonextensionality controls; it is an English argument, not an executed proof.

## Observation equality abstraction

Only this exact shape is replaced by a fresh, consistently named uninterpreted Boolean function of the two map values:

```text
forall i[0], ..., i[n-1] ::
  select(m, i[0], ..., i[n-1]) == select(n, i[0], ..., i[n-1])
```

Pinned Boogie type checking rewrites Boolean `==` to `Iff`; the matcher accepts that typed equivalence form as Boolean value equality. This does not include implication or disequality. Both maps must have the same closed monomorphic resolved signature. The bound variables must form the complete index tuple, each used once and in declaration order on both sides. The map-value expressions may contain current/old variables, substitutions or ordinary pure function applications, but may not depend on any of these index binders. Partial, repeated or permuted tuples, unused extra binders, extra guards, additional index captures, type-polymorphic maps and non-equality bodies retain the original quantifier translation. Quantifiers with attributes or explicit triggers also retain that translation. No source function name supplies eligibility.

For each source model, interpret the new function `ObsEq(m,n)` as exactly the universal read-equality relation over that signature's complete index carrier. It is a pure function of two map values; alpha-renamed binders do not change that relation. Thus every replaced formula has its original truth value in this model extension, whether it occurs positively or negatively. Normalizing the two map expressions through the ordinary environment also preserves their enclosing binders, call-input substitutions and distinct current/old state values. This argument relies on the complete tuple and absence of bound-index captures; it does not justify replacing a guarded or partially quantified formula by the same relation.

The normalized predicate has no axioms or instances. Its unconstrained interpretations enlarge the model class, and do not strengthen the original context. In particular, observation equality implies neither raw map equality nor any individual read equality in the normalized program. The source-valid pointwise consequence `ObsEq(m,n) ==> select(m,0)==select(n,0)` can remain unproved. No reflexivity, symmetry, extensionality, reverse equality or observation premise is added. Under `subsumption 1`, an original top-level quantified assertion retains its nonlearning classification even though its owned condition is now an application.

An independent tagged-function consistency model can assign equal observations to two distinct map tags. Interpreting `ObsEq` by their function components satisfies the weak-equality negative fixture's assumptions and leaves `assert false` false. Executing that fixture on the worker remains a required anti-vacuity control; this model argument is not an observed verdict.

## Operational diagnosis

The earlier global quantified-helper package ran in [public gate 37201990430](https://github.com/erniecohen/dafny/actions/runs/37201990430). Six of fifteen cases matched. Eight expected-negative cases and the second positive tuple-coordinate check returned `Inconclusive`, reason `(incomplete quantifiers)`. The helper-free polymorphic omission control returned `Failed` as expected.

The pinned worker disables `smt.mbqi` and `auto_config`. Boolean index/value domains do not remove quantification over the opaque map carrier. Universal R0 over integer values also requires infinitely many distinguishable maps. These repairs change no solver option and accept no unknown as a failure verdict. The weak-equality fixtures' exact complete-tuple read-equality assumptions now use the separately reviewed `ObsEq` abstraction; other source quantifiers remain an operational boundary.

## Strict controls

[MapTheoryInputs/cases.json](../../../Source/DafnyB3Normalizer.Test/MapTheoryInputs/cases.json) retains every original positive, false, wrong-value, nonextensionality, polymorphism and boxing verdict target. All static axiom counts are zero. Each demanded direct map-operation signature retains a deterministic `Monomorphic map helper origin:` record identifying the ITE encoding and pinned source equations; this does not claim global axioms were loaded. A separate `Map observation equality abstraction:` record describes each demanded observation-predicate signature. The manifest and strict runner check both origin counts and export them with the actual request/verdict evidence.

Structural controls cover tuple differences, nested stores, lexical bound indices, nonmutation, deterministic origins, excessive depth and assignment opacity. Observation controls cover complete/partial/repeated/permuted tuples, extra guards/captures, metadata, both polarities, no pointwise/reverse premises, call-input substitutions, and current/old captures across assignment. They do not invoke a prover. The repairs remain uncompiled and unexecuted until their exact gate runs.

The strict [map runner](../../../Source/DafnyB3MapTheory.TestRunner/Program.cs) remains a normal `make test-b3` stage after building the verified worker package:

```sh
dotnet run --project Source/DafnyB3MapTheory.TestRunner --no-restore -- \
  --worker "$WORKER_DLL" --solver "$Z3" --solver-sha256 "$Z3_SHA256" \
  --timeout-ms 30000 --rlimit 1000000
```

Run via the project's queued validation workflow. The runner checks exact package/solver identities and exports owned requests with `--emit-directory`. Success requires actual traversal completion, no error, exact aggregate outcome, fixture-specific check coverage, and every expected attempt outcome. These fixtures have linear reachable checks; the coverage requirement is not generalized to unreachable checks. Unknown, timeout, missing tools, malformed responses and every mismatch fail. No case is waived or silently skipped. The operational repair is a target for the exact gate, not a claim that SAT/UNSAT outcomes have already been obtained.
