# Native Real correspondence (issue 124)

This note specifies the experimental B3 native Real source extension. The source
implementation and acceptance controls are present; fresh verification and
runtime acceptance evidence are pending. The existing integer and
map controls remain required. Bitvector support is a separate remaining P7
extension; this Real phase does not narrow the backend's intended final scope.

## Representation and typing

The primitive `real` sort denotes all mathematical reals. A rational literal
stores two arbitrary-precision integers, numerator and denominator, with a
strictly positive denominator. Rational constants do not restrict the carrier of
Real variables, functions or quantifiers to rational numbers.

Raw external objects are resolved and checked before use. A nonpositive literal
denominator is rejected by executable checks, not merely by a subset-type
annotation. The resolved well-formedness predicate records the same condition.
The typechecker implementation and `TypeCorrectExpr` agree: addition,
subtraction, multiplication, comparisons and unary minus use one homogeneous
Int or Real operand type; integer `div` and `mod` stay integer-only; real division
uses Real operands; `ToReal` maps Int to Real; `ToInt` maps Real to Int. No general
mixed-sort promotion is introduced.

The protocol transmits bounded exact integer strings. A Boogie `BigDec` value
with mantissa `m` and exponent `e` means `m * 10^e`. Nonnegative exponents produce
numerator `m * 10^e` and denominator one; negative exponents produce numerator
`m` and denominator `10^(-e)`. Zero is represented by `(0, 1)`. Bounds are checked
before powers of ten or expanded strings are allocated; exponent negation is
widened before handling the minimum signed integer. Excessive literals are
reported unsupported, never rounded, clamped or approximated.

The SMT term for a literal is
`(/ (to_real numerator) (to_real denominator))`, with signed integer numerals
printed using unary minus. Real sort is therefore explicit even when the
denominator is one. B3 text syntax exposes `#real(n, d)`, `#to_real(e)`,
`#to_int(e)` and `/`; these prefixes do not collide with existing legal
identifiers. Raw, resolved and diagnostic expression printers preserve the
literal and coercion forms.

## Primitive interpretation and source producers

`ToReal` is the standard integer embedding. `ToInt` is floor: the greatest
integer no greater than its argument, including negative arguments. In
particular, floor of `-1.3` is `-2`. This follows the
[SMT-LIB mixed integer/real theory](https://smt-lib.org/theories-Reals_Ints.shtml).
Real division agrees with ordinary division at nonzero denominators. At zero,
the total SMT primitive has an unspecified value; source and target use the
same interpretation. This is the
[SMT-LIB Real theory](https://smt-lib.org/theories-Reals.shtml).

The pinned Boogie translator lowers `ArithmeticCoercion` to `to_real`/`to_int`.
For `RealDiv` it independently embeds any Int operand into Real before emitting
`/`; the bridge follows this specific rule. Integer division/modulo are distinct
operators and retain the integer correspondence described in
[arithmetic-correspondence.md](arithmetic-correspondence.md).

Dafny's prelude declares `Int(real): int` and `Real(int): real` using native
coercions. These ordinary declarations carry active defining axioms rather than
inline Bodies. `_System.real.Floor` has an actual inline Body calling `Int`.
`LitReal` uses the existing eligible identity projection. The source producer
for an ordinary real-to-integer conversion emits the integrality obligation
`Real(Int(o)) == o`; the Floor field does not acquire that obligation. The
source producer also emits `DivisorNonZero` for real division. The bridge
preserves these checks and adds no nonzero or integrality assumptions.

## Eligible definition substitution

A unary primitive Body is eligible only when the resolved monomorphic function
has exactly one correctly typed formal, one correctly typed result, and its
actual Body is the matching coercion applied directly to that formal object.
Names, claimed builtin attributes and detached metadata do not establish this.

An axiom route additionally requires an AlwaysRevealed function and its exact
`DefinitionAxiom` object in the current top-level declaration list. The axiom
must be an unconditional monomorphic universal equality with exactly one
correctly typed binder, no where predicate, a call to the same function object
at that binder on the defining side, and the matching native coercion of that
same binder on the other side. Only the typed identity coercion installed around
the defining call by Boogie is removed. Guards, captures, existential binders,
wrong formal identities and residual type instantiations are rejected.

Floor's Body composition is a separate narrow case: its unary actual Body calls
an independently eligible primitive conversion function at its own formal.
This is exact formal substitution, not a name rule or general body expansion.
Primitive arithmetic definitions outside the reviewed routes are reported
unsupported instead of being advertised as native support.

## G1 model argument

Fix a typed source model, including its interpretations of the total primitive
operations at zero denominators. Preserve the Int and Real carriers and these
interpretations in the normalized model. Exact literal construction preserves
denotation; every native operator and explicit coercion preserves its denotation
by the preceding rules. Body substitution is justified by formal substitution.
The eligible active defining equality justifies the corresponding unary rewrite
for every argument. Floor composition applies these rules twice.

Interpret each existing opaque normalized symbol by its source interpretation
at the corresponding closed type instance. Structural induction on recursively
typed expressions then preserves denotation, including Real-bound quantifiers,
lets and conditionals. Existing omitted source axioms and constraints enlarge
the target model class. No boxing law, collection equation or new global
arithmetic axiom is introduced. Consequently each source countermodel projects
to a normalized countermodel, and normalized validity implies source validity,
conditional on the separately reviewed command correspondence.

This is an English model argument, not a mechanized theorem for the entire
backend. Vendored `RawAst/Values.dfy` still contains placeholder value semantics
and `Semantics/Semantics.dfy` is commented out. The executable library's resolver
and typing contracts are checked separately; their verification does not prove
solver interpretation, C# capture correctness, or complete pipeline soundness.
The existing recursive let typing predicate is strengthened to agree with its
already recursive executable check; no existing contract is weakened.

## Source checkpoints and acceptance

1. Extend vendored types, expressions, proof predicates, executable checking,
   solver lowering and all parser/printer/visitor cases coherently.
2. Extend schema, signature validation and RawAst construction; update protocol
   and normalizer versions and all package/fixture consumers. Old packages fail
   before verification.
3. Add bounded exact `BigDec` capture and the narrow typed source rewrites.
4. Add structural, protocol, worker and actual Dafny controls, the release note,
   and updated source provenance. A fresh verifying library build is mandatory.

Controls include exact decimals and large numbers beyond floating-point
precision; exponent/allocation bounds; all malformed numeric signatures and
nonpositive denominators; nested parser/printer round trips; active versus
detached/guarded/wrong-identity definitions; native Real state, functions, lets,
quantifiers and map observations; negative Floor; failed fractional conversion;
division-by-zero diagnostics; false arithmetic/Floor assertions; and an
`assert false` with a satisfiable irrational witness (`x*x == 2.0`). These prevent
rounding, truncation, missing source checks and a rational-only carrier from
passing as native Real support. Unknown stays inconclusive and errors cannot
become proof success. Existing anti-vacuity and obligation-accounting controls
remain in the gate.

Source inventory hashes describe the edited source only. The previous library
bootstrap receipt does not apply after these edits. Fresh verification, both
shared runtime target compilations, focused controls and actual corpus outcomes
must be recorded before support is reported accepted.

The runtime round-trip fixture extracts its expression through the exact
resolver-created `LabeledStmt` with label `return` (`Ast/Resolver.dfy:784-789`),
then requires the original one-Check Block (`Ast/StmtResolver.dfy:134-151`). The
wrapper is part of procedure return control, not a printer transformation. The
fixture previously expected that Block directly and failed before exercising
resolved printing. All exact text, literal, coercion, precedence and denominator
checks remain required after correcting extraction. This fixture-only repair
changes no parser/printer/solver implementation or proof contract; its edited
inventory still requires a fresh runtime receipt.


The universal identity corpus fixture marks only its arithmetic quantifier with
`{:nowarn}`. The quantified formula remains `x + 0.0 == x`; no trigger or logical
premise is introduced. The frontend supports this attribute to report a missing
trigger as information, so its warning exit does not obscure the actual backend
verdict. The earlier public run verified both units but exited 2 for that frontend
warning; it remains a failed corpus receipt. The Java scratch compiler output is
named `b3`, matching the original build recipe, because a default-package class
`B3` would shadow the generated `B3` namespace in its own main method. Neither
change alters worker arithmetic or the strict negative corpus expectations.
