# Integer bitwise references and newtype frontend (issue 193)

`--integer-bitwise` is disabled by default. It installs the ghost class
`IntegerBitwiseModel` and its explicit one-step unfold lemmas. It also enables the six basic bitwise operators in ghost code on
explicitly width-qualified integer newtypes. The optional native companion below
provides checked conversion and Boolean bridges; runtime operators remain later
milestones. The independent
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
or attribute can replace the compiler-owned target. Compiled uses receive the
ordinary ghost-function diagnostic; backend lowering is explicitly deferred.

`issue193-newtype-operators.py` verifies actual source clients against M1 at widths
8, 16, 32 and 64, checks live false assertions with every definition active, and
rejects missing range proofs, strict-predicate violations, invalid counts,
invalid width declarations, mixed nominal types, opaque capability and compiled
uses. It preserves existing BV and Boolean intrinsics. Negative controls must
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

Remaining native equations include subtraction, multiplication, unsigned
division/remainder, ordering, rotations and population count. General word APIs
at other widths and runtime lowering of integer-newtype operators also remain.

## Checked signed word interpretation

Include `Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeSigned.dfy`
with `--integer-bitwise`. Modules `Std.Arithmetic.IntegerBitwiseNativeSigned8`,
`16`, `32` and `64` provide ghost functions `Encode(a)` and `Decode(x)`, and
checked lemmas `Range`, `RoundTrip`, `Inverse` and `SignBit`.

For width N, let M=2^N and H=M/2. `Encode` requires `-H <= a < H` and converts
`a+M` when a is negative, or a otherwise. `Decode` takes the existing unsigned
integer observation U and returns U below H, or U-M otherwise. `Range` bounds
the decoded result by `-H <= Decode(x) < H`. `RoundTrip` proves
`Decode(Encode(a)) == a`; `Inverse` proves `Encode(Decode(x)) == x`. `SignBit`
relates `((x>>(N-1))&1)==1` to `Decode(x)<0`.

The existing `x as int` observation remains unsigned. These are explicit proof
interfaces for a signed interpretation; they do not add signed arithmetic or
change integer-newtype operators, division/remainder conventions, direct cast
checks, or runtime lowering. A client can specify a native high-bit test using
the signed interpretation, or compose the two inverses at a bounded signed
integer newtype.

### Model and qualification boundary

Interpret each native word as its unsigned little-endian weighted digit sum U,
with `0 <= U < M`. Subtracting M on the upper half maps the word bijectively to
`[-H,H)`. Negative a encodes as `a+M` in `[H,M)` and nonnegative a encodes
unchanged in `[0,H)`. Both conversions therefore use an in-range unsigned
carrier, and the existing checked unsigned round trip and inverse prove the
two signed inverse laws. The high digit is `floor(U/H)`, which is zero below H
and one at or above H; the checked right-shift and low-digit rules identify it
with the native shift-and-mask test. No assumption, added axiom, exemption or
compiler change is introduced.

The new functions and lemmas, their dependency closure and every control are
checked with pinned Z3 5.1.0 and the default bitvector engine. Both resolver and
additional-axiom modes are qualified. The sign-bit proofs fix the power of two
for the high digit once, then combine two helpers per width: the native high
digit equals its own low bit, and an integer's quotient by H is one exactly on
`[H,M)`. The integer-based engine
(`smt.bv.solver=2`) is not used: it can report unsatisfiable for satisfiable
queries (https://github.com/erniecohen/dafny/issues/618).

Positive controls cover symbolic native words and bounded signed integers,
a Boolean-returning native sign-test client, minimum and maximum signed values,
minus one, zero and the high-bit boundary. Negative controls reject the two
signed bounds, wrong encodings and unsigned interpretations, wrong signs and
invalid direct conversions. Reachable false assertions follow each lemma
separately and all four together; controls must reject conclusively.

Serial mutations negate every lemma's postcondition, remove each explicit
encoding/round-trip premise, and perturb both conversion definitions. Every
mutation first checks its corresponding unmodified baseline on the same
inhabited inputs. A mutation that hangs is killed only through its own process
group after approximately three times that clean baseline, with a five-second
minimum. Such a kill is recorded separately from a failed proof; no timeout or
resource exhaustion qualifies a baseline or control. Body/client and
negative-control ceilings remain 2,000,000 and 200,000 resource units per scope.

## Checked arithmetic right shifts

Include `Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeSignedShifts.dfy`
with `--integer-bitwise`. The modules
`Std.Arithmetic.IntegerBitwiseNativeSignedShifts8`, `16`, `32` and `64`
extend [the signed interpretation milestone](https://github.com/erniecohen/dafny/pull/611).
Their executable opaque function `Right(x,k)` requires `k <= N` and proves
`Decode(Right(x,k)) == Decode(x) / P(k)`. Division here is Euclidean floor
division by the positive power, so shifting -3 once yields -2. Count zero
preserves the word; count N yields zero for nonnegative words and all ones
(signed -1) for negative words. Existing native `bvN` shifts remain logical.
This API does not change integer-newtype operator lowering.

### Model and checked derivation

Put M=2^N, H=M/2 and U=`x as int`. A nonnegative word has U<H, and the
existing checked logical-shift bridge gives U/P(k); its value remains below H.
For a negative word put a=M-1-U, so a<H and `Decode(x)=-a-1`. Complement the
word, shift it logically, and complement the result. The existing checked
complement and logical-shift bridges give unsigned result M-1-a/P(k). Its
signed value is -1-a/P(k). `NegativeQuotient` proves this equals
(-a-1)/P(k) by constructing quotient -1-a/P(k) and remainder
P(k)-1-a%P(k), then invoking checked quotient uniqueness. The remainder is
nonnegative and below P(k). The identity holds for every integer a and
positive divisor, including endpoint powers. `SignDecision` checks that the
native unsigned comparison with H selects the decoded sign; `Observe` checks
the native result expression, and `Right` constructs that result and calls
`Observe`. Every function and lemma has a checked body. No axiom, assumption,
verification exemption or compiler fact is added.

The new bodies and the signed-interpretation dependency are qualified with
Z3 5.1.0's integer-based BV engine. The complete existing logical-shift and
conversion closure is checked independently with the default engine, as are
symbolic word/signed-integer clients and endpoint/concrete clients. Both
resolvers and additional-axiom settings are required for qualification. The existing issue-284
backend consistency qualification remains applicable.

Negative controls reject invalid counts, incorrect result witnesses, invalid
casts, wrong rounding/direction/sign results and nonpositive arithmetic
divisors. Reachable false assertions follow both sign cases, each rule,
zero/full-width function calls and the rules together; negative and vacuity
controls run with both BV engines. For the integer-based engine, concrete
observation controls construct the result expression and establish input and
sign-threshold conversions using checked `RoundTrip` calls, so it need not
infer literal conversion identities. Default-engine controls use literal result
words for the same reachable calls. Serial mutation mode negates each
postcondition and removes each explicit precondition, and separately changes
the branch, sign boundary, positive shift direction, negative sign extension,
inner complement and count in each executable function. Every mutant requires
a passing baseline. A mutant exceeding three times that baseline (with a
five-second minimum) is stopped by its own process group and recorded
separately; no baseline/control exhaustion or timeout passes.

Runtime qualification compares C# and JavaScript with an independent signed
integer oracle. It exhausts all 8-bit inputs, exercises structured and seeded
wider words, and tests every count from zero through N for every selected word.
The proof/control ceilings are 2,000,000/200,000 resource units per scope.
This adds a source-level native-word helper; rotations, signed division and
remainder conventions, arbitrary-width APIs and runtime lowering of bounded
integer-newtype operators remain separate work.
