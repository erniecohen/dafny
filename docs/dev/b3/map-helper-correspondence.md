# Closed monomorphic map helpers

This P4 slice adds two source-backed families of facts for direct typed Boogie `MapSelect` and `MapStore` expressions. It is a correspondence argument and a proposed executable control gate, not a formal proof of the normalizer. The implementation and controls must compile and run on the exact recorded source before they provide executable evidence.

## Scope and new facts

A demanded map type is eligible only when it has no map type parameters and all index/result types are closed and accepted by the normalizer. Type aliases and resolved proxies are observed without mutation. Each distinct eligible map signature retains an opaque sort and owns exactly one select/store pair. Both functions are associated with every helper axiom so the B3 worker can load the equations when either function is demanded. There are no native SMT arrays.

For a map `m`, index tuple `i`, value `v`, and tuple `j` of the same closed signature, the new equations are:

```text
R0:    select(store(m, i, v), i) = v
R1[k]: i[k] = j[k] OR select(store(m, i, v), j) = select(m, j)
```

All variables are universally quantified. There is one R1 clause per coordinate, so a difference in any one coordinate suffices for an unchanged read. R1 is not a clause requiring every coordinate to differ. Nullary maps have only R0. The helpers use empty trigger lists, matching this pinned native equation family; no trigger or performance equivalence is claimed.

Map equality remains ordinary equality on the opaque carrier. The helpers introduce no extensionality, reverse cast, store idempotence/commutativity, source axiom, lambda equation, or nonidentity prelude definition. Polymorphic map operations keep their former uninterpreted signatures and record that no read-over-write equations were emitted. Map assignment syntax remains rejected; a store expression on the right-hand side of a simple assignment is eligible.

## Pinned source evidence

The reference package is Boogie `3.5.5-review.37e4435d`, source commit `73a0e214a87df85fc058270268c1d0706fd05bc9`, with provenance recorded in [semantic-correspondence.md](semantic-correspondence.md).

The Arguments map builder creates select/store functions and adds `GenMapAxiom0` and `GenMapAxiom1` in lines 253-262. `GenMapAxiom0` states the same-index read equality in lines 298-351. `GenMapAxiom1` creates one universally quantified disjunction per index/type component in lines 353-464. A closed monomorphic map fixes all native abstraction type arguments and has no bound map type arguments, leaving precisely R0 and the value-coordinate R1 clauses above. The source explicitly omits extensionality. [Map builder and equations](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L253).

Primitive typed values reaching native universal-carrier parameters are put in canonical encode/decode form by `AssembleOpExpression`, lines 604-650. Cast creation supplies the forward left inverse in `TypeErasure.cs`, lines 175-189. The reviewed Arguments encoding's `GenReverseCastAxiom` returns true, lines 48-79; it does not assert that every universal-carrier element is an integer or Boolean. The typed helpers do not restore that invalid reverse direction. [Argument canonicalization](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L604), [forward cast law](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasure.cs#L175), [reverse cast omission](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L48).

These native internal casts are distinct from Dafny prelude `$Box`/`$Unbox` functions. Demanded nonidentity prelude instances remain uninterpreted and their defining/source axioms remain omitted. A direct `[int]Box` map therefore gets read-over-write equations on opaque Box values, without a `$Box`/`$Unbox` round trip or an unguarded reverse Box equation.

## Source-model interpretation argument

Consider a model of the pinned native Arguments encoding of an accepted source unit. Interpret each normalized primitive sort by the corresponding primitive domain and each opaque closed sort by the corresponding source carrier, using the native universal carrier where erasure requires it. Interpret a closed map select/store pair by its native pair at the fixed closed type arguments. Native primitive arguments are encoded canonically and primitive select results are decoded; opaque arguments/results retain their native carrier interpretation.

For R0, the native same-index equation gives the encoded stored value. Decoding a primitive result gives the original value by the forward left inverse. An opaque result needs no primitive decoding. For R1, unequal typed primitive coordinates have unequal encodings: equality of the encodings would imply equality after applying the forward decoder. Unequal opaque coordinates already retain that inequality. The native clause for that coordinate then equates the two reads, and any result decoding preserves equality. No reverse cast premise is used.

Consequently, this interpretation satisfies every added helper equation and agrees with the source expressions. It extends the projection of a source model with the owned helper declarations. This is the direction needed for normalized validity to imply reference validity. It is not a claim that every model of the earlier free-UF abstraction satisfies the helpers, nor that every normalized countermodel extends to the full prelude. Calls, state and loops still rely on their separately recorded correspondence restrictions.

There is also an independent consistency model for the helper theory: for one signature, take a map to be a total function on index tuples paired with a tag from a two-element set. Select applies the function; store updates that function at the specified tuple and preserves the tag. R0 and every R1 hold. Different tags allow unequal maps whose reads agree at every tuple. This construction demonstrates why the equations alone need not force extensionality; it is an English model argument, not an executed proof or a formalization.

## Demand records and bounds

`Approximations` now includes one `Monomorphic map helper origin:` record per eligible demanded signature. It records the exact Boogie source commit/line range, opaque map sort, index/result sorts, select/store names, and emitted R0/R1 roles. Polymorphic demands have a separate explicit omission record. Helper equations and function names are deterministic for the same typed source and traversal.

A pre-allocation bound rejects a helper family whose estimated construction exceeds the protocol node ceiling. A subsequent traversal counts every helper axiom's expression graph along with the unit and declaration manifest. Unsupported normalization produces no partial program or static obligation manifest.

## Controls and executable verdict gate

[MapTheoryInputs/cases.json](../../../Source/DafnyB3Normalizer.Test/MapTheoryInputs/cases.json) records fixture-specific structural counts and verdict targets. At introduction, all verdict targets below are **unexecuted**. The xUnit controls parse, resolve, typecheck, normalize and validate owned IR; they do not invoke a prover. Additional controls exercise deterministic origins, source nonmutation, excessive map arity, and actual Dafny `imap<int,int>` read translation. That Dafny control establishes normalization coverage only: collection operations and heap update functions have not gained defining equations.

| Typed BPL fixture | B3 target | Native target | Purpose |
| --- | --- | --- | --- |
| `read-same` | Verified | Verified | R0 |
| `read-other` | Verified | Verified | R1 under unequal indices |
| `read-wrong` | Failed | Failed | Incorrect stored value |
| `false-with-theory` | Failed | Failed | Literal false with helpers demanded |
| `weak-equality` | Failed | Failed | Equal reads and unequal maps must remain consistent |
| `tuple-other` | Verified | Verified | Each tuple coordinate independently triggers R1 |
| `aliases` | Verified | Verified | Equivalent closed signatures share helpers |
| `nested` | Verified | Verified | Nested closed map results demand separate signatures |
| `nullary` | Verified | Verified | R0 with no R1 |
| `polymorphic-opaque` | Failed | Verified | Omitted polymorphic map theory |
| `box-left-inverse-omitted` | Failed | Verified | Source Box law remains omitted |
| `box-reverse-invalid` | Failed | Failed | No unguarded reverse Box law |
| `bool-read-wrong` | Failed | Failed | Finite Bool-index/value wrong value |
| `bool-false-with-theory` | Failed | Failed | Finite Bool-index/value literal false |
| `bool-weak-equality` | Failed | Failed | Finite Bool-index/value nonextensionality |

The finite Boolean-index/Boolean-value negative controls complement the integer cases. Quantified read-over-write axioms with infinite integer domains can make SAT countermodel construction difficult for a UF-map solver. Unknown is a gate failure and is not relabeled as a Failed verdict or mathematical evidence. The integer targets remain in the strict gate; any need for a different operational encoding requires a separate review.

The separate [DafnyB3MapTheory.TestRunner](../../../Source/DafnyB3MapTheory.TestRunner/Program.cs) submits these exact typed BPL normalizations through `WorkerProcessClient`. After building a verified worker package and obtaining the pinned Z3 5.1.0 executable, run it through the project's validation workflow:

```sh
dotnet run --project Source/DafnyB3MapTheory.TestRunner --no-restore -- \
  --worker "$WORKER_DLL" --solver "$Z3" --solver-sha256 "$Z3_SHA256" \
  --timeout-ms 30000 --rlimit 1000000
```

`make test-b3` includes this strict verdict runner after building/testing the host package and before the Dafny integration corpus. It computes the solver-file digest portably from the exact supplied `Z3_PATH`; the worker still requires and checks version 5.1.0 and checks that digest before launching it. This is a required gate stage, not a workerless xUnit skip.

Use `--dotnet` to select the worker launcher, `--fixtures` to select the exact fixture directory, and `--emit-directory` to export normalized request packets. The runner reports fixture SHA256, normalizer assembly identity/SHA256, normalizer version, B3 commit, bootstrap compiler, worker source/package fingerprint, solver version/digest, program hash, helper origins and typed completion attempts.

Success requires `TraversalCompleted`, no completion error, the exact expected outcome, and every per-check attempt outcome specified in the fixture manifest. In particular, the anti-vacuity fixtures require the first read-over-write check to be Verified and the subsequent invalid/false check to be Failed. Every fixture here has linear reachable checks, so the runner also requires the actual attempt IDs to cover its static check manifest, with exactly one attempt per check. That fixture-specific requirement is not a general protocol rule: unreachable checks may legitimately have zero attempts. Missing tools, parse/type errors, unsupported normalization, unknown, timeout, resource exhaustion, malformed protocol responses or a mismatch return nonzero. Nothing is silently skipped or counted green. This runner executes B3 verdicts only; native targets require the separate pinned native gate. Gate admission, source review and structural acceptance alone do not establish the target verdicts.
