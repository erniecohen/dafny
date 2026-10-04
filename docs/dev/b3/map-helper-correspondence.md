# Direct monomorphic map read-over-write identities

This P4 slice rewrites direct typed reads of owned store expressions. The repair is source-only until compiled and executed on the exact package. Its correspondence argument is not a formal proof of the normalizer.

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

## Operational diagnosis

The earlier global quantified-helper package ran in [public gate 37201990430](https://github.com/erniecohen/dafny/actions/runs/37201990430). Six of fifteen cases matched. Eight expected-negative cases and the second positive tuple-coordinate check returned `Inconclusive`, reason `(incomplete quantifiers)`. The helper-free polymorphic omission control returned `Failed` as expected.

The pinned worker disables `smt.mbqi` and `auto_config`. Boolean index/value domains do not remove quantification over the opaque map carrier. Universal R0 over integer values also requires infinitely many distinguishable maps. This repair changes no solver option and accepts no unknown as a failure verdict. Original quantified read-equality assumptions in the weak-equality fixtures remain a separate operational boundary in this first repair commit.

## Strict controls

[MapTheoryInputs/cases.json](../../../Source/DafnyB3Normalizer.Test/MapTheoryInputs/cases.json) retains every original positive, false, wrong-value, nonextensionality, polymorphism and boxing verdict target. All static axiom counts are zero. Each demanded closed signature retains a deterministic `Monomorphic map helper origin:` record identifying the direct ITE encoding and pinned source equations; this does not claim global axioms were loaded.

Structural controls cover tuple differences, nested stores, lexical bound indices, nonmutation, deterministic origins, excessive depth and assignment opacity. They do not invoke a prover. The repair remains uncompiled and unexecuted until its exact gate runs.

The strict [map runner](../../../Source/DafnyB3MapTheory.TestRunner/Program.cs) remains a normal `make test-b3` stage after building the verified worker package:

```sh
dotnet run --project Source/DafnyB3MapTheory.TestRunner --no-restore -- \
  --worker "$WORKER_DLL" --solver "$Z3" --solver-sha256 "$Z3_SHA256" \
  --timeout-ms 30000 --rlimit 1000000
```

Run via the project's queued validation workflow. The runner checks exact package/solver identities and exports owned requests with `--emit-directory`. Success requires actual traversal completion, no error, exact aggregate outcome, fixture-specific check coverage, and every expected attempt outcome. These fixtures have linear reachable checks; the coverage requirement is not generalized to unreachable checks. Unknown, timeout, missing tools, malformed responses and every mismatch fail. No case is waived or silently skipped.
