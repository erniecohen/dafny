# Integer bitwise references and newtype frontend (issue 193)

`--integer-bitwise` is disabled by default. It installs the ghost class
`IntegerBitwiseModel` and its explicit one-step unfold lemmas. It also enables the six basic bitwise operators on
explicitly width-qualified integer newtypes. The optional native companion below
provides checked conversion and Boolean bridges. The six source operators
compile to C#, Go, Java and Python; runtime support for other targets remains later work. The independent
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
an enabled library requires an enabled client. Its manifest also binds the reference
ABI to the full Dafny product version, including the fork revision. A missing or
different binding requires rebuilding the library: the numeric file version alone
is shared by fork revisions and cannot identify the installed equations. Disabled
libraries retain the existing manifest and compatibility path. Source and library
contents do not authenticate new compiler declarations.

Local declarations and opened imports retain their ordinary bindings, including
unqualified names inside an off-built library loaded by an enabled client. The
compiler identity recognizes both the nullable class and its non-null view.
Availability checks use the declaring module's option mode and the parsed file's
recorded mode. File provenance matters because the default module can combine
enabled client declarations with off-built library declarations. References to
the compiler-owned class, including types and static receivers, are rejected in
off-mode declarations; this check also traverses library bodies and signatures.

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

The independent U/I cross-product checks fixed-reference availability alongside
the existing user-arithmetic option, ordinary functions with misleading bitwise
names, and existing Boolean/bitvector intrinsics. Neither flag enables the other.
The user-operator prototype requires the refreshed resolver; its legacy rejection
is checked explicitly. The frontend controls below exercise integer-newtype capability, count/result
checks and rejection of unsupported uses on actual source operators.

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

## Integer-newtype frontend

This frontend milestone depends on PR 532. The source operators `&`, `|`, `^`,
`!`, `<<` and `>>` on a revealed `newtype {:bitWidth N} T` lower respectively to
`andNat`, `orNat`, `xorNat`, `notNat`, `shlNat` and `shrNat`, with the literal N
and operands converted to mathematical integers. An ordinary conversion back
to T checks its entire predicate, including inherited subset/newtype constraints.
There is no assumed closure or automatic range contract. Clients may call the
independently checked foundation and M1 range lemmas before an operation.
An operator in a specification also needs its membership checks justified in
that specification's well-formedness context.

Both resolvers accept the surface. The source count must satisfy `0 <= k <= N`,
including for bitvector counts; an ordinary checked assertion precedes the
shift reference call. Counts zero and N are legal; a shift by N yields zero
by the existing M2 definition, rather than by host count masking. Binary operands
and the result have the same nominal newtype. Type synonyms retain that identity;
distinct nested newtypes need their own annotation. Opaque exports expose no
capability. Newtypes with member invariants remain excluded from this milestone.
Malformed width attributes are diagnosed only with the feature enabled.

Width validation uses the existing resolved integer-bound discovery, including
implied base-type constraints. Its result must establish a nonnegative interval
below 2^N. It does not select a native carrier or rely on native-type annotations.
The compiler does not infer width from the interval. Attribute arguments are
positive integer literals. The interval comparison uses bit lengths so a large
width annotation does not allocate a correspondingly large power of two.

**Definitional model argument.** Interpret each reference by the M1 function as
above, and each source operator by that function at its annotation's width.
For AND, OR and XOR the unsigned operands are their finite N-digit expansions;
for complement use the N-digit complement. The existing M1 correspondence and
range lemmas prove those interpretations agree. For left/right shift use the
M1 shift interpretation at a checked count in [0,N], including the zero-valued
N endpoint. Each lowered term is exactly its reference application followed
by an identity conversion on integers. That conversion introduces T only after
checking its complete predicate. The count assertion introduces its conclusion
only after proving it. These are source definitions and checks, with no new
prelude formulas, trusted declarations, global recurrences, assumed range facts,
or native-BV bridge. They extend the same qualified model described above.

`IntegerBitwiseSyntaxExpr` retains source notation for serialization and formatting.
After inference, before the call graph and ghost checks, its semantic child is
an ordinary call/conversion (and a checked count assertion for shifts). Later
visitors therefore do not receive an integer under a BV opcode. Constant folding
cannot mistake semantic width for `AsBitVectorType.Width`; the opaque reference
call remains opaque. Clones use the standard resolved-child/type substitution;
source and `.doo` reload rebuild the selection from recorded options and visible
declarations. Off-built files cannot acquire the surface or width capability
from an enabled client, including in the merged default module. No source name
or attribute can replace the compiler-owned target. The reference call remains
the verifier meaning in compiled contexts as well.
C#, Go, Java and Python compilation of these six operators is described below; other targets reject
compiled uses explicitly.

`issue193-newtype-operators.py` verifies actual source clients against M1 at widths
8, 16, 32 and 64, checks live false assertions with every definition active, and
rejects missing range proofs, strict-predicate violations, invalid counts,
invalid width declarations, mixed nominal types and opaque capability. It
preserves existing BV and Boolean intrinsics. Negative controls must
finish within 200,000 RU; exhaustion is never accepted. The existing M2
foundation/emission controls remain separate gates.

## Checked native Boolean bridges

Include `Source/DafnyStandardLibraries/optional/IntegerBitwiseNative.dfy` with
`--integer-bitwise` to use `Std.Arithmetic.IntegerBitwiseNative8`, `16`, `32`
and `64`. The suffix selects the native `bvN` type. These ordinary ghost
lemmas work with `--additional-axioms` off. No collector, new prelude formula,
compiler assumption or globally installed bridge is added. Including the file
is explicit; the packaged standard library and all existing compiler inputs
are unchanged.

Each module exports `RoundTrip(a)` for `0 <= a < 2^N`, `InverseN(x)`, and
`And(x,y)`, `Not(x)`, `Or(x,y)`, `Xor(x,y)` and `AndNot(x,y)`. For example,
`And` proves `(x & y) as int == IntegerBitwiseModel.andNat(N,x as int,y as int)`.
Call the corresponding checked foundation lemma to transport the result to M1.
For integer inputs call `RoundTrip` first, then apply the Boolean bridge to the
checked conversions. Width-zero and other native widths are outside this API.
The existing checked cast still enforces its destination range; nothing here
makes a cast wrap or presumes closure of a stricter integer newtype predicate.

### Model argument for every checked rule

Fix `M = 2^N`, interpret a native word as its unsigned little-endian weighted
sum `U`, and interpret the compiler references by M1 as above. All following
rules are proved from the existing theory and finite M2 definitions. Their
model arguments explain the proof decomposition; they are not new axioms.

| Checked rules | Model and proof |
|---|---|
| `WidenN`, `WidenTo64` | Zero extension adds only zero high digits, preserving `U`. The smaller-word AND adapters use the same fact for both operands and their result. |
| `AddN` | Widen both words by one bit. Their sum is below `2M`, so native addition in that wider sort equals `U(a)+U(b)`. |
| `NoOverflowN` | Its checked premise equates the widened sum with the zero extension of the narrow sum. Substitute the widening and addition equations. The premise excludes carry out of the original width. |
| `Split64`, `DigitSplit64` | Write `x = 2h+l` with `h=x>>1`, `l=x&1`. Both narrow additions in the reconstruction fit. The low digit is zero or one, so Euclidean uniqueness gives `U(h)=U(x)/2` and `U(l)=U(x)%2`. Smaller copies of these support lemmas have the identical argument. |
| `NextN`, `Successor` | Excluding the all-ones word excludes the only overflowing increment. Checked addition gives `U(p+1)=U(p)+1`; substitute the stated integer value in `Successor`. |
| `InverseN` | Converting the unsigned weighted sum of a word back to its native width reconstructs exactly those digits. This inverse is checked against the existing native theory. |
| `RoundTrip` | Induction on nonnegative `a`. Zero is immediate. Reconstruct the preceding word by induction; it is not all ones because `a<M`. Its checked successor has value `a`; the native inverse identifies that successor with the checked cast of `a`. No integer round-trip axiom is used. |
| `Zero64` | A sum of nonnegative binary digits is zero exactly when every digit is zero. |
| `AndStep64` | Native AND commutes with removing the low digit; its low digit is one exactly when both operand low digits are one. Apply the checked digit decomposition to its result. |
| `AndPrefix64`, `AndRecurrence` | At width zero the range premises force both operands to zero. At positive width the checked digit equations divide each operand by two, satisfying the smaller range. Induction and one finite defining equation give the exact integer AND recurrence. The smaller native adapters zero-extend into this proof at their original semantic width. |
| `Power` | Explicit finite evaluations of the terminating M1 power recurrence, followed by the checked M2 power-agreement theorem, give the literal modulus for each supported width. |
| `NativeNot`, `Not` | Each complemented digit contributes with the original digit exactly once to `M-1`. Their widened native sum therefore equals `M-1`; checked addition proves subtraction from that value. For unsigned input, normalization is identity, giving the exact M2 complement definition. |
| `Or` | Native De Morgan identity is checked, then the checked complement and AND bridges instantiate the M2 OR definition. |
| `Xor` | The checked native identity `x^y == (x|y)&!(x&y)`, the preceding bridges, and the finite M2 XOR equation give the result. |
| `AndNot` | Apply checked complement to the second operand and checked AND to the pair, then instantiate the finite M2 ANDNOT equation. |

All recursive proof calls decrease a natural number. Every helper has a body;
there is no new bodyless declaration, `assume`, `{:axiom}` or `{:verify false}`.
No rotation operator is used, so these proofs introduce no reachability of the
rotation rewrite discussed in issue 147. The issue-284 backend qualification
above remains in force: checked source proofs and live controls do not establish
whole-theory consistency of the erased backend.

The permanent native regression independently checks all included proof bodies,
symbolic native and composed integer callers, wrong equations and an inhabited
false assertion after each rule. Invalid casts, overflowing increments/additions,
invalid prefix ranges and recurrence premises must fail. Mutation mode negates
every postcondition separately and requires rejection by its proof body. Recursive
fact mutations use an inhabited base input, keeping a broken induction hypothesis
from obscuring the counterexample; the unmodified proofs remain generic. It
removes each explicit guard separately and requires a concrete invalid caller
to lose its expected rejection; the guard making a recurrence contract
well-formed is checked directly in that rule's verification. Positive
construction has a 2,000,000-RU scope ceiling; live negative and vacuity controls must finish within
200,000 RU. Exhaustion never passes.

This is a checked Boolean/conversion sub-milestone, depending on PR 538 and
through it PR 532. Shift, modular arithmetic, order, rotation and population-count
native bridges remain separate proof work. It implements no trusted emitter and
no runtime lowering.


## Checked native shifts and modular addition

This milestone depends on PR 543. Include
`Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeShifts.dfy` with
`--integer-bitwise`. The modules `Std.Arithmetic.IntegerBitwiseNativeShifts8`,
`16`, `32` and `64` extend the explicitly included proof API. All bodies are
checked with additional axioms off; the compiler and packaged library inputs
are unchanged.

`Plus(x,y)` proves `(x+y) as int == (x as int+y as int) % 2^N`.
`NativeLeft(x,k)` and `NativeRight(x,k)` prove the unsigned integer observations
`(U(x)*P(k)) % 2^N` and `U(x)/P(k)`. Their natural count must satisfy `k <= N`.
`LeftShift` and `RightShift` transport these results to the compiler-owned
`shlNat` and `shrNat` references. Counts zero and N are included: the former
preserves the word and the latter produces zero. Integer inputs use the checked
conversion round trips from the predecessor. Integer-newtype clients still
prove their complete result predicate through the existing checked foundation.

### Model and proof decomposition

Interpret a word as its unsigned weighted digit sum U and put M=2^N. The native
carry equation writes the widened sum as the zero extension of the low word
plus either zero or M. Widened addition and `CarryValue` prove the corresponding
integer sum. Euclidean quotient/remainder uniqueness then gives `Plus`.
`PlusWitness` simply names the native result so later proofs can use its
observation without rediscovering a native equality in a mixed-theory context.

The existing shift rewriter narrows counts to `bit_length(N)` bits. The checked
count modules therefore supply exact conversions and predecessor equations for
widths 4, 5, 6 and 7. These are internal count helpers, not general word-operation
APIs at additional widths. Their proofs use exact widened addition, native
inversion and terminating induction, as in the predecessor's checked round trip.
`CountBound` checks agreement of the small native count bound with its integer
observation.

At positive count the checked native recurrences remove one low digit for a
right shift, and double the preceding shifted word for a left shift. The
witness lemmas name the result word explicitly. Induction decreases the integer
count. The right-shift proof combines digit decomposition with the already
checked division-product theorem. The left-shift proof uses `PlusWitness` and
the checked modular-shift law. Separate integer-only helpers establish the
power recurrence and the zero-count arithmetic. The reference adapters then
use the existing foundation, normalization identity and endpoint arithmetic.
Every helper has a body. No assumption, axiom, verification exemption or new
trusted compiler fact is introduced. No native rotation is used. The issue-284
backend qualification above remains applicable.

The regression independently checks all included bodies, symbolic native and
composed integer-newtype clients, endpoint and overflow examples, wrong equations,
and an inhabited false assertion after each rule. Invalid counts, count
conversions and helper premises are rejected. Serial mutation mode negates each
postcondition and removes each explicit precondition separately. Fact mutations
are checked on concrete inhabited inputs; recursive ones use base inputs to avoid
an inconsistent induction hypothesis. Guard mutations must lose a designated
invalid-caller rejection, or fail contract well-formedness when the guard defines
a later premise (the count/divisor guards in `RightArithmetic` and `LeftArithmetic`). Exhaustion never counts as a control or mutation pass.
The proof/control ceilings remain 2,000,000/200,000 resource units per scope.

Remaining native equations include multiplication, unsigned
division/remainder, ordering, rotations and population count. General word APIs
at other widths and runtime lowering of integer-newtype operators also remain.

## Checked native modular subtraction

Explicitly include `Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeSubtraction.dfy`
with `--integer-bitwise`. The modules `Std.Arithmetic.IntegerBitwiseNativeSubtraction8`,
`16`, `32` and `64` provide `Minus(x,y)`, proving
`(x-y) as int == (x as int-y as int) % 2^N`. The result is the unsigned
observation of modular subtraction; it includes borrow and wraparound.

For M=2^N, the checked native `ComplementSum` identity expresses subtraction as
`x+!y+1`. Checked complement gives U(!y)=M-1-U(y). Two checked modular additions
and the integer-only `Reduce` lemma then give (U(x)-U(y)) mod M. The helper names the intermediate and final integer
observations, with checked
positive-modulus and remainder-equation premises. `MinusWitness` similarly names
the native result under its checked subtraction-equality premise. These interfaces
keep the mixed integer/native obligations local. Every declaration has a proof
body; no axiom, assumption, verification exemption or compiler fact is added.
The companion remains outside the packaged standard-library inputs.

The regression checks included proof bodies and symbolic native and bounded
integer-newtype clients with both resolvers, independently of additional axioms.
Concrete controls cover no borrow, borrow, zero, maximum values and self
subtraction. Wrong-result equations and inhabited false assertions must fail;
invalid conversion bounds, incorrect witness results, incorrect intermediate
observations and a zero modulus must be rejected. Serial mutations
negate every new postcondition and remove each new premise separately.
Resource exhaustion or timeout is never accepted as a successful control. The existing proof/control
ceilings are 2,000,000/200,000 resource units per scope.

## C# runtime lowering for integer newtypes

With `--integer-bitwise`, the six existing source operators compile to C#.
The resolved result conversion retains its ordinary compiler-owned reference
call as its verifier meaning. An internal width marker selects runtime emission
and survives resolved cloning and substitution. Compilability examines exactly
the source operands: a ghost operand remains forbidden, and a direct call to a
ghost reference function remains forbidden. The existing count assertion and
full result-newtype membership checks are unchanged. No axiom or new verifier
fact is introduced.

C# binds operands once, from left to right. A native newtype uses unsigned
machine operations and converts the checked result to its original carrier;
other newtypes use BigInteger operators. AND, OR and XOR preserve the finite
unsigned domain. Complement is masked to the semantic width; left shift is
masked after shifting; right shift is logical because operands are nonnegative.
A count at least the semantic width returns zero. Native intermediates also
return zero for counts at least 64, avoiding C# count masking. For a semantic
width larger than a native carrier, the existing result predicate must still
prove representability: any discarded high bits are zero in a verified result.
No bitvector or sequence conversion is executed. Semantic width is never
inferred from the carrier. These rules refine the weighted-digit reference:
masking is remainder modulo a power of two, complement flips exactly those
digits, and the two shifts move their indices with zero fill.

Go, Java and Python compilation are described below. Other targets report an explicit
unsupported-feature diagnostic. The fixed
reference interface remains ghost-only. Other target implementations, native
rotation bridges and rotation/bit/population-count source surface were separate work.
The rotation and bit-query source extension is described below.

`issue193-runtime-csharp.py` checks complete generated programs (including the
runtime drivers), symbolic reference contracts, both resolvers and axiom modes,
and an independent arithmetic-digit runtime oracle. It exhausts small-word
binary inputs, exercises wide structured/seeded inputs, and checks exact runtime
case identities as a vacuity control. Unsigned and signed native carriers at
widths 4/5/8/12/16/31/32/63/64 and nonnative 80/128-bit cases cover masks, signs and count endpoints. Wrong results, invalid
counts, ghost operands and unsupported targets are rejected. Generated-code
inspection, clone/substitution unit tests, serial mutations and paired
option-off/code/verdict/resource comparisons qualify the compilation boundary.


## Rotation and bit-query source members

The M6a source surface stacks on https://github.com/erniecohen/dafny/pull/621.
With `--integer-bitwise`, a revealed descriptor `newtype {:bitWidth N} T`
supports `x.RotateLeft(k)`, `x.RotateRight(k)` and `x.bit(i)`. Both resolvers
elaborate actual member calls; method values and opaque descriptors do not gain
this capability. These names are reserved on enabled width declarations, so a
user member cannot silently take the compiler-owned meaning. Count arguments
have type int (named `k` for rotations and `i` for bit queries).

**Definitional argument.** A rotation is exactly the existing compiler-owned
`rolNat(N,x as int,k)` or `rorNat(N,x as int,k)`, followed by the ordinary checked
conversion to T. The ordinary assertion checks `0 <= k <= N`. A bit query checks
`0 <= i < N` and returns `shrNat(N,x as int,i) % 2 == 1`; it never converts the
intermediate shift result to T. Thus its proof meaning is the unsigned reference
bit at i. Rotation endpoints zero and N are identities by the existing model.
Nested source calls retain each full result-predicate obligation. No axioms,
closure contracts, native-BV conversions or compiler identities are added.
Existing foundation agreement lemmas connect these expressions to the checked
integer model, independently of `--additional-axioms` and of native bridge PRs
https://github.com/erniecohen/dafny/pull/610,
https://github.com/erniecohen/dafny/pull/615 and
https://github.com/erniecohen/dafny/pull/622.

C# evaluates receiver and count once, in source order. Rotations use semantic N,
with guarded endpoints before any host shift masking. A native carrier is used
only when it can hold the N-bit intermediates; wider semantic widths use
BigInteger intermediates and cast only the final result. In particular a
12-bit semantic word stored in a byte rotates 16 left by eight to 1. Bit queries
use the existing guarded right shift followed by parity. Unsupported compilation
targets retain the explicit diagnostic from the C# runtime milestone.

`issue193-rotation-bit-source.py` checks symbolic reference contracts, legal
endpoints, exact runtime-case identities and an independent quotient/remainder
oracle, including non-power-of-two and nonnative widths. Invalid counts/indices,
wrong results, live false controls, stricter result predicates, ghost arguments,
malformed calls and member collisions must fail conclusively. Paired ordinary
and ghost definitions, resolution-cache checks, clone/substitution coverage,
serial mutations and baseline Boogie/verdict/resource comparisons qualify the
elaboration boundary. Population-count source support is the following M6b item.


## Population-count source member

The M6b source extension stacks on https://github.com/erniecohen/dafny/pull/623,
which in turn stacks on https://github.com/erniecohen/dafny/pull/621. With
`--integer-bitwise`, a revealed `{:bitWidth N}` integer newtype supports the
direct call `x.PopCount()`. It takes no value or type arguments, has result
type `nat`, and reserves that member name on enabled width declarations.
Opaque exports, first-class member selection and @-labels do not acquire this
capability. Both resolvers retain the use-site and recorded declaration-mode
checks. Other source members and the option-off path retain their prior behavior.

Elaboration is a compiler-identity reference call `IntegerBitwiseModel.popNat(N,
x as int)` followed by an ordinary conversion to `nat`. No receiver-predicate
check applies to the count, and no range theorem or new axiom is injected.
Explicitly include `optional/IntegerBitwisePopulation.dfy`; its checked `View`
lemma transports the existing foundation agreement and sequence-count theorem,
establishing `0 <= popNat(N,x) <= N` for all integers x and natural N, including
zero width in the mathematical API. Call that lemma before source expressions
that need its bounds; source contracts can refer to a verified wrapper function.
The new `AllOnes` proof unfolds the existing recurrence and power equation and
inducts on N; zero and single-bit/power cases reuse the existing checked laws.

C# native carriers use `System.Numerics.BitOperations.PopCount` on their
nonnegative unsigned observation. Arbitrary-precision values are split into
disjoint 64-bit chunks: each low chunk contributes its native count, then a
logical shift on the nonnegative BigInteger exposes the next chunk. The chunk
weights do not affect the number of set bits. This loop terminates as the value
shrinks; it allocates no byte array and does not truncate to the storage width
or the first chunk. The result is a BigInteger natural, and the source receiver
is evaluated once through the existing static binding. Narrow native storage
for larger semantic widths remains valid because all stored high bits are zero.
Python compilation is described below; other targets reject compiled source calls explicitly.

`issue193-popcount-source.py` checks generic reference/sequence agreement, bounds,
zero/all-ones/high-bit/power-of-two laws, complete compiled drivers and independent
arithmetic-digit execution. It exhausts small words and tests structured/seeded
inputs crossing chunk boundaries through width 256. Live false/wrong-count and
missing-range-proof controls, strict predicates, malformed calls, ghostness,
opacity, collisions, recorded library modes, reloads, refinement, recursive
resolution, equality/cache controls and serial mutations qualify the boundary.
Consumer/control scopes retain the 200,000-RU ceiling; dependency closure uses
2,000,000. Exhaustion is never accepted as a proved theorem or failing control.
No alternate bitvector solver engine is used.

Other runtime backends and the final cumulative acceptance matrix remain
separate milestones for https://github.com/erniecohen/dafny/issues/193.


## Python runtime lowering for integer newtypes

The opt-in source surface also compiles to Python: `&`, `|`, `^`, `!`, `<<`,
`>>`, `RotateLeft`, `RotateRight`, `bit` and `PopCount`. This milestone stacks on
https://github.com/erniecohen/dafny/pull/624. It changes no resolved reference,
verification obligation, result-membership check or added-axiom setting.

Python represents both native-hinted and nonnative integer newtypes with
arbitrary-precision integers. The backend binds source operands once, left to
right, as arguments to a lambda with fresh parameters. Complement and left
shift are masked with `2^N-1`; a full-width shift returns zero. Right shift is
logical because the source operand is nonnegative. Rotations combine the two
shifted parts at the declared semantic width, with identity cases at zero and
N. Bit queries reuse the checked right-shift and parity elaboration.
Population count uses `bin(value).count('1')`, which counts every significant
bit and is available on Python 3.8; it does not require `int.bit_count`.
Native storage hints never truncate a Python intermediate or determine its
semantic width. The already checked complete result predicate still applies.

These operations refine the existing weighted-digit model: AND, OR and XOR
act on corresponding nonnegative binary digits; masking takes the remainder
modulo `2^N`; complement flips precisely those N digits; shifts move digits
with zero fill. The disjoint shifted parts of a rotation partition the N
positions, and counting the binary ones equals the sum of the individual
digits. Leading zeros change none of those sums. No runtime reference body,
bitvector conversion, new axiom or assumed range fact is introduced.

`issue193-runtime-python.py` reuses the C# program generators and independent
arithmetic-digit oracles. It checks the complete drivers before executing them,
compares exact case identities and all outputs, exercises native and wide
carriers, narrow-storage/wide-semantic words, both count endpoints and strict
predicates, and inspects single operand evaluation and Python 3.8 syntax.
The same wrong-result, invalid-count and live-false controls must fail under
both resolvers and additional-axiom settings. Serial backend mutations and
paired parent Boogie/verdict/per-VC resource and option-off generated-source
comparisons cover the compilation boundary. C# remains qualified separately;
other targets retain an explicit unsupported-feature diagnostic.


## Java runtime lowering for integer newtypes

This opt-in runtime milestone stacks on https://github.com/erniecohen/dafny/pull/625.
Java compiles the same integer-newtype Boolean, shift, rotation, bit-query and
population-count surface. Resolution and verification remain the checked
compiler-reference path. No axiom, assumption, count check or result predicate
changes.

The backend reuses the existing beta-redex emitter to bind operands once,
left to right, outside a lambda, and unbox its parameters inside the body.
Native carriers at semantic widths up to 64 use long intermediates. Unsigned
bytes, shorts and ints are widened without sign extension; unsigned longs keep
their complete bit pattern. A mask retains the low N digits after complement
or left shift. Right shift uses `>>>`, and full-width shifts and rotation
endpoints are guarded before Java can mask a host count. The same weighted-digit
argument as for C# applies: truncation modulo `2^64` commutes with reduction
modulo `2^N` for N at most 64. Rotation parts occupy disjoint digit positions.

Nonnative values and wider semantic words use BigInteger operations and an
N-bit mask. A native value entering that path is observed through the existing
unsigned conversion helper where needed. Only the final result is converted
back to its checked native carrier. Native population count uses Long.bitCount;
BigInteger.bitCount counts nonnegative arbitrary-precision values. Both counts
become BigInteger naturals. Bit queries reuse the source right-shift/parity
elaboration. These APIs are available within the backend's existing Java target;
no runtime-version requirement changes.

`issue193-runtime-java.py` uses the Python gate's shared runner with the same C#
program generators and independent arithmetic-digit oracles. It verifies complete
drivers, checks exact runtime case identities, both source resolvers and axiom
settings, unsigned carrier boundaries, strict predicates and single argument
evaluation. An extra 80-bit semantic word on an unsigned 64-bit carrier checks
both endpoints and a high-bit wrap that distinguishes unsigned BigInteger
observation from sign extension. The same negative and live-false controls,
parent Boogie/verdict/resource comparisons, cache checks, and serial backend
mutations qualify the boundary. The original Python entry point retains its
existing CLI, programs, oracle checks and Python 3.8 syntax inspection.


## Go runtime lowering for integer newtypes

This opt-in milestone stacks on https://github.com/erniecohen/dafny/pull/626.
Go compiles the same Boolean, shift, rotation, bit-query and population-count
surface. The checked compiler references remain the verifier meaning; no axiom,
assumption, count obligation or complete result predicate changes.

The existing beta-redex emitter binds arguments once, left to right, outside
the function body. Native carriers at semantic widths up to 64 use uint64
intermediates. Masking after complement and left shift retains the N low
digits. Go shifts never mask their counts: a shift by the full carrier width
yields zero. Thus full-width source shifts and both rotation endpoints have
their checked meanings without a host-count adjustment. Rotation parts occupy
disjoint digit positions. Truncation modulo 2^64 commutes with the N-bit mask
for N at most 64, as in the C# native argument.

Nonnative words and wider semantic widths use the existing immutable Int
runtime operations with an N-bit mask. Native inputs enter that path as
unsigned mathematical observations, including a 64-bit carrier at width 80.
Only a final result proved to satisfy its complete native predicate is narrowed
back to that carrier. Bit queries reuse right shift and parity elaboration.
Population count repeatedly replaces a nonzero nonnegative value x with
x AND (x-1), removing exactly its least set digit. The count starts at zero,
increases by one per removed digit and ends at the weighted-digit reference
cardinality. Native counts are at most 64; nonnative counts use Int throughout.
No runtime helper or target version requirement changes.

`issue193-runtime-go.py` reuses the shared checked program generators and
independent arithmetic-digit oracles, including the Java milestone's wide
unsigned carrier and interior native/wide count controls. It checks exact case
identities, both resolvers and axiom modes, strict predicates, native high bits,
wide masks, endpoint counts and single argument evaluation. Generated Go is
also inspected for single evaluation outside the function body. Live-false and
wrong-result controls, serial backend mutations, paired parent proof/resource
comparisons and fresh/cold/warm/audit equality checks qualify this boundary.
