# Generic constant-field lambdas and allocation (#132)

When a revealed constant field is used, its right-hand side is a declaration expression. If the enclosing declaration is generic, checking that expression's callability must substitute the receiver and the actual type arguments, including arguments inherited from parents. The resolved `MemberSelectExpr.TypeArgumentSubstitutionsWithParents()` map already provides those arguments and is used when translating the constant value.

The shipped line already makes this substitution, as introduced by [commit 5ecb3b4a](https://github.com/erniecohen/dafny/commit/5ecb3b4a73d76798c88e37ac7ba900fe1c10c514). The development integration of the capture-based allocation rules from #132 omitted that existing companion correction. This note records the port repair and keeps the existing crashing input registered as a dependency regression.

## Concrete failure

`dafny0/BoundedPolymorphismCompilation.dfy` contains `Dt<X extends XTrait>` with a constant `K` initialized by a lambda taking `p: X`. `BoundsAndCasts.Test` creates `Dt<Record>` and selects `d.K`. Without the actual type map, callability expands the lambda with declaration-level `X`. The allocation introduction then emits that declaration parameter in its function type and lambda-domain membership outside the generic declaration's scope. Boogie reports six undeclared `BoundsAndCasts.Dt$X` occurrences in the two generated introductions and verification aborts.

The reported method at source line 165 is `BoundsAndCasts.Test`; the failure comes from `d.K` at line 172. The earlier inferred string argument to `MyMethod(i)` is not the failing generic application.

[The public focused comparison](https://github.com/erniecohen/dafny/actions/runs/37244648606) used the same complete input in three arms: original development source `97601b75712f458de37a13bf9953a0b7825122eb` verified 27 declarations with 0 errors, while the repaired composite and final source `3996f31e1bc76b86b6ff81b1d3b421f1e39ee530` both aborted with those six identifiers. The compiler reports 4.11.1 and the comparison uses Z3 5.1.0. The repaired arm was compiled after applying the recorded source patch, so its version string still names the original checkout. This is a diagnostic comparison, not a completed acceptance gate for the port repair.

## Derivation and scope

For a resolved member selection at actual receiver type `Dt<Record>`, declaration type parameter `X` denotes `Record`. Substituting that map is the ordinary capture-avoiding instantiation of the declaration expression. The existing substituter changes the lambda's bound-variable types, occurrences, local variable types, result type, and nested expressions together. Callability and value translation therefore refer to the same instantiated lambda.

The repair changes only the type-map argument supplied when expanding a revealed constant field for callability. It retains the existing revelation guard, receiver substitution, recursive callability checks, and all allocation introductions and capture premises. It adds no axiom, heap fact, trusted cast, or allocation exemption. In particular, a reference-containing actual still requires its existing capture-allocation conditions; the repair must not be implemented by deleting the lambda introduction or by assuming its result allocated.

No source change is required on the shipped or owner-based #132 follow-up snapshots that already contain this exact substitution. Their complete expression-translator files remain unchanged.

## Regression and validation boundary

Register the existing `dafny0/BoundedPolymorphismCompilation.dfy` and keep its source and runtime `.expect` byte-for-byte unchanged. Its existing compiler RUN invokes refresh mode with datatype traits on all supported compilers. The original suite verdict is 27 verified / 0 errors and remains the target; an abort is not an accepted verdict update.

After applying the development port repair, record the unchanged program's verification result and run its existing compiler harness. Retain the #132 fresh-capture negatives, pure/preallocated controls, complete partial/total universal contracts, and anti-vacuity controls. The source derivation does not replace those measurements or the strict full gate. This note makes no post-repair measurement claim.
