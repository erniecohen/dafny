# Bounded-integer bitwise reference model

`IntegerBitwise.dfy` implements the mathematical milestone of
https://github.com/erniecohen/dafny/issues/193 and the design in
https://github.com/erniecohen/dafny/pull/247. Include `IntegerBitwise.dfy`
explicitly to use `Std.Arithmetic.IntegerBitwise`.

The optional directory is outside the existing `src/Std` project. The original
M1 sources need no feature option and change no packaged `.doo`.

For the M2 compiler-owned interface, include `IntegerBitwiseCompiler.dfy` and
enable `--integer-bitwise`. It proves agreement between all twelve raw references
and M1 in the separate `IntegerBitwiseCompilerFoundation.dfy` verification
input, then exposes checked range contracts and transports the existing laws.
Its module is `Std.Arithmetic.IntegerBitwiseCompiler`. See
[the interface, mapping and model argument](../../../docs/dev/integer-bitwise-references.md).
The separate digit adapter continues to use the original ordinary model.

`P(n)` is the exact power of two. All integer reference functions are total,
including negative arguments and width zero. `norm(n,a)` is Euclidean remainder.
`bitsNat(n,a)` preserves exactly n little-endian Boolean digits, including leading
zeros; `value` is its inverse on fixed-length sequences. `AndView`, `NotView`
and `BooleanBits` prove the Boolean meanings of AND, complement, OR, XOR and
ANDNOT. `ShiftViews` and `RotationViews` give sequence decompositions;
`ShiftBits` and `RotationBits` give scalar indexed laws. `PopulationView`
connects population count with the number of true sequence entries.

Shifts outside `0 <= k < n` return zero; a shift by n is therefore zero.
Rotations use `k % n` at positive width, for every integer k; zero-width
rotations return zero. These total reference definitions do not legalize source
operators or counts. `RotateEndpoints` proves identity after normalization at
0/n. `RotationAlgebra` proves composition and inverse laws without native BV
operations. Every operation retains its exact definition from the design;
OR/XOR/ANDNOT are derived from AND/complement, not independently postulated.

The ten scalar client obligations are provided by `AndMaskLow`, `AndBound`,
`OrBound`, `ShiftRoundDown`, `XorSelf`, `BitTest`, `MaskMergeBit`, `PopPower`,
`ShiftFits` (and the weaker range result in `ShiftViews`), and `RotateEndpoints`.
Their explicit preconditions distinguish normalization, count/index bounds,
and no-overflow arithmetic. No arbitrary newtype predicate is presumed closed.

The design's carrier-width counterexample is `CarrierRotationCounterexample`:
`rolNat(12,16,8) == 1`, whereas `rolNat(8,16,8) == 16`. Both results fit an
8-bit carrier, so representability alone cannot justify carrier rotation.
The regression driver documents this as a test of reference semantics; it does
not claim an existing integer-newtype compiler bug or implemented backend fix.

`IntegerBitwiseDigits.dfy` additionally uses the existing standard libraries.
Its base-two refinement of `LittleEndianNat` proves Boolean/digit inverses,
value agreement, and agreement with `FromNatWithLen`. It normalizes arbitrary
integers before using that natural-number interface. It does not install an
`Auto` lemma globally or introduce a second trusted arithmetic theory.

The core model can be checked with:

```sh
dafny verify Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy \
  --additional-axioms:false --standard-libraries:false --solver-path /path/to/z3
```

For the digit adapter, verify `IntegerBitwiseDigits.dfy` with
`--standard-libraries:true`. Its embedded dependency requires the same Boogie
options with which that library was built (the empty list). Verify the core
separately: an include alone does not verify its bodies.

The `Integer bitwise reference` review-CI jobs check every new body on Z3 5.1.0
and the test solver, under both resolvers, with additional axioms OFF. They retain
generic construction costs, per-lemma symbolic-client and concrete-call costs
at widths 8/16/32/64, source hashes and full logs. Every new lemma has an
inventoried, inhabited call followed by a reachable `assert false`. These
controls must produce exactly the expected assertion errors within 200,000 RU
per scope; exhaustion, warnings, missing rows and other errors fail the gate.
This is operational non-vacuity evidence, not a claim of SAT for the entire
unsliced solver prelude. The independent generic proofs remain the mathematical
justification. Ordinary proof bodies have a separate 2-million-RU per-scope cap.

M1's historical both-off compatibility claim and unavailable flag modes remain
scoped to its introducing revision, as recorded in the design. M2 has separate
compiler identity, emission, bootstrap, control and comparison gates; the original
historical audit does not establish compatibility for a later product change.

`IntegerBitwiseNative.dfy` is the explicitly included checked native Boolean
companion. With `--integer-bitwise`, its width-specific modules prove conversion
round trips and AND/complement/OR/XOR/ANDNOT equations at 8, 16, 32 and 64 bits.
It works independently of `--additional-axioms`; all helper and bridge bodies
are verification inputs. See the model arguments and API in the implementation
notes linked above. Shift/arithmetic/rotation bridges and runtime lowering remain
later milestones.
