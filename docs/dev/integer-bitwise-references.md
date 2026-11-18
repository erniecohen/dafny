# Fixed integer bitwise references (issue 193, M2)

`--integer-bitwise` is disabled by default. It installs the ghost class
`IntegerBitwiseModel` and its explicit one-step unfold lemmas. It does not yet
provide integer-newtype operators or native bitvector bridges. The independent
`--additional-axioms` option does not expose this public interface.

Include `Source/DafnyStandardLibraries/optional/IntegerBitwiseCompiler.dfy` for
`Std.Arithmetic.IntegerBitwiseCompiler`, the checked consumer interface. This
source-only companion is outside the packaged standard library. Its range and
algebra contracts are proved, rather than installed as compiler assumptions.
The original M1 model and separate digit adapter remain ordinary Dafny sources.

## Definition and identity boundary

`AST/Types/IntegerBitwiseReferences.cs` is the canonical definition description.
It constructs fully resolved functions and lemmas in `_System`; it parses no
privileged source and discovers no intrinsics by a user name or attribute.
The raw functions return `int` and have no range contracts or bodies. Normal
bodyless-function lowering gives their fixed backend symbols, such as
`_System.IntegerBitwiseModel.andNat`. These are uninterpreted functions, not
recursive prelude bodies. Normal lemma-call lowering checks arguments and
preconditions and assumes the one defining equality in the installed unfold
lemma's contract. It neither recurses into fresh applications nor reveals user
functions. No globally quantified recurrence is installed.

The source spelling is an ordinary resolution binding to the compiler-owned
objects. A user may shadow that spelling, but then receives ordinary source
semantics; its declaration cannot replace the installed object. The compiler
signatures are reconstructed from the installed compiler on `.doo` reload.
`integer-bitwise` is a Global option with `OptionLibraryImpliesLocalError`:
an enabled library requires an enabled client. The existing exact product-version
check remains the reference ABI. Source and library contents do not authenticate
new compiler declarations.

The following mapping is exact. Width `n` is `nat`; all other inputs and every
raw result are `int`. Arguments retain the M1 order.

| M1 function | Raw function | One-step lemma | Foundation proof |
|---|---|---|---|
| `P(n)` | `IntegerBitwiseModel.P(n)` | `unfoldP` | `Power` |
| `norm(n,a)` | `IntegerBitwiseModel.norm(n,a)` | `unfoldNorm` | `Normalize` |
| `andNat(n,a,b)` | `IntegerBitwiseModel.andNat(n,a,b)` | `unfoldAnd` | `And` |
| `notNat(n,a)` | `IntegerBitwiseModel.notNat(n,a)` | `unfoldNot` | `Not` |
| `orNat(n,a,b)` | `IntegerBitwiseModel.orNat(n,a,b)` | `unfoldOr` | `Or` |
| `xorNat(n,a,b)` | `IntegerBitwiseModel.xorNat(n,a,b)` | `unfoldXor` | `Xor` |
| `andNotNat(n,a,b)` | `IntegerBitwiseModel.andNotNat(n,a,b)` | `unfoldAndNot` | `AndNot` |
| `shlNat(n,a,k)` | `IntegerBitwiseModel.shlNat(n,a,k)` | `unfoldShl` | `LeftShift` |
| `shrNat(n,a,k)` | `IntegerBitwiseModel.shrNat(n,a,k)` | `unfoldShr` | `RightShift` |
| `rolNat(n,a,k)` | `IntegerBitwiseModel.rolNat(n,a,k)` | `unfoldRol` | `LeftRotate` |
| `rorNat(n,a,k)` | `IntegerBitwiseModel.rorNat(n,a,k)` | `unfoldRor` | `RightRotate` |
| `popNat(n,a)` | `IntegerBitwiseModel.popNat(n,a)` | `unfoldPop` | `Population` |

`bitsNat`, `value`, `bit`, `count`, `flip` and `zipAnd` remain ordinary checked
sequence definitions. There is no trusted sequence reference interface.

## Model and bootstrap argument

Interpret each new UF by the corresponding M1 total integer function. Its
one-step equation is precisely that function's body, with ordinary lets inlined.
`P`, `andNat` and `popNat` recur only at `n-1` in the positive-width branch.
The other functions form the same acyclic dependency graph as M1. Negative
integer arguments use Euclidean division/remainder; shifts outside `0 <= k < n`
return zero, rotations at width zero return zero, and positive-width rotations
reduce the count modulo `n`. These branches guard every partial source expression.
This interpretation satisfies all twelve equations together, and extends the
integer part of a model of the pre-existing theory.

The unfold contracts retain source-definedness premises: normalization requires
`P(n)>0`; active left shifts require `P(n)>0`; active right shifts require
`P(k)>0`; positive-width rotations require the powers used as denominators to
be positive. Width zero and inactive shifts require no such premises. The
foundation proves power agreement by induction first, then uses M1 positivity to
discharge those premises. AND and population agreement use induction at raw
signatures. Other agreements follow their exact defining dependencies. None of
these proof targets uses the later consumer range contracts. The independent
`IntegerBitwiseCompilerFoundation.dfy` input includes only M1 and those twelve
proofs; it is checked separately with every included body before the consumer
file is checked. The wrappers then prove range from agreement with M1, and transport M1's checked laws by equality.

This is a trusted **definitional rule**, not a soundness repair and not an
additional-axioms bridge. It therefore ships behind its own opt-in language option,
as selected in [the design](design/193-bitwise-integers.md#64-canonical-reference-functions-and-packaging).
Both options off constructs no new builtin and preserves the existing path.

## Checks and scope

`issue193-fixed-references.py` checks the actual emitted Boogie equalities against
M1's bodies, failing on unsupported syntax. It independently checks the companion
foundation together with included M1 bodies, then the consumer bodies, and runs
false and wrong-result controls with the defining facts active. `--full` additionally transports the existing M1 law calls and
symbolic width clients. Positive/bootstrap scopes have a 2,000,000-RU ceiling;
negative controls have a 200,000-RU ceiling. Warnings, resource exhaustion,
unexpected diagnostics or missing logged scopes fail the check.

`unfold-slices.py` constructs finite integer-only SMT slices from those actual
emitted equations and a recursively evaluated M1 interpretation. It checks SAT
and sensitivity to changing each equation's right-hand side by one. These finite
slices do not assert SAT of the unsliced erased prelude or of a negated VC.

All results remain backend-qualified as required by
[issue 284](https://github.com/erniecohen/dafny/issues/284): the guarded-character
repair and canonical-map-payload repair are present in predecessor
`81062e7df6eecd9d73a0671abd83206d13b67893`, including repair commit
`0c8ce6f0b820f945b87de0b049d4b42011727425`, with
Boogie `3.5.5-review.dcd726f6`. The wider erased collection/boxing/handle theory
has no whole-theory closure proof here. This extension argument and the controls
do not establish unconditional consistency of that underlying theory.
