# User-defined operators: revised design and critical review (#194)

**Status:** round-2 design recommendation; no product implementation.
**Target:** `review/4.11.0`.
**Source audited:** `428e348b0b125cb67505f03a0026b3f497f98e48`.
**Prior design reviewed:** PR #239 at `5339faad320aab58b16f2a35ebae623f81c3413d`.
**Interacting design:** PR #247 at `a61b06c71d742ff19b280592d26d1a0e2ce9c87c`.
**Brief:** [round-2 request](https://github.com/erniecohen/dafny/issues/194#issuecomment-6806811006).
**Authorship:** AI-assisted, Astra Pro, with independent cloning, bitwise,
first-milestone and example reviews reconciled here.

This document replaces the affected design sections in place. All source paths
refer to the exact audit pin unless explicitly labelled otherwise. `AGENTS.md`
and `REVIEW.md` at that pin govern later implementation. It answers the requested
equality, bitwise, clone-transport, smallest-milestone and usability questions,
and answers the next inference-design question in 13.3. The complete design is
separated from the deliberately smaller first PR. Checked existing-syntax
examples are distinguished from unimplemented operator examples and from line/
performance estimates.

## 1. Recommendation and implementation boundary

Implement user operators as **notation for ordinary calls**, behind
`--user-defined-operators`, default false. Use attributes on ordinary named
members and a source-syntax wrapper whose semantic expression is an ordinary
call. Complete this lowering during resolution, before any analysis that uses
primitive opcodes, call edges, termination, bounds, ghostness, or triggers. Do not
teach the verifier algebraic laws about an operator attribute.

**Keep `==` and `!=` intrinsic everywhere.** A library's mathematical equality
is a named predicate such as `eq`; equivalence and order laws are ordinary
proved lemmas. This replaces the proposed equality-override policy, rather than
adding a ghost/executable exception to it. The exact equality-site matrix and
counterexample are in section 6. There is no equality-override milestone.

**Start with one useful refreshed-resolver PR:** concrete classes and inductive
datatypes, directly declared function/predicate operators, deterministic
built-in-first selection, and ordinary-call verification/compilation. No
`"override"` mode, compound assignment, trait slots, bounded-type lookup, or
legacy resolver implementation in that PR. It must nevertheless include the
small binding-preservation and export checks described in 5.6; postponing those
would make even the restricted feature unsafe. Section 12 specifies the accepted
subset, every deliberate rejection, per-file size estimates, and tests.

The pinned resolver gives datatype rank meaning to **`<` and `>` only**. It does
not supply datatype `<=` or `>=`. Thus a direct rational datatype can already
support `a * b + c <= d` in the no-override milestone. Numeric-newtype `+` and `*`
remain intrinsic, so modular arithmetic with those spellings needs a later
non-equality override milestone or a distinct datatype wrapper. Do not describe
all four datatype comparisons as rank operators. See
`Resolver/PreType/PreTypeResolve.Expressions.cs::ResolveBinaryExpr` and
`Resolver/ModuleResolver.cs::ResolveOp`.

**Keep issue #193's bounded-integer bitwise operations as independently enabled
intrinsics.** A library surface over the integer reference functions is
semantically possible. Native execution still needs a verified executable
implementation or a canonical compiler specialization, and the ghost reference
functions are not an executable implementation by themselves. Section 6.4
compares the three concrete routes and specifies coordinated document changes.
#193 must not wait for #194; #194's first PR does not add user bitwise symbols.

| Decision | Recommended complete design | First useful PR |
|---|---|---|
| Declaration | `{:operator "symbol"}` on a named callable; a later `"override"` mode only for non-equality symbols | One literal symbol argument; functions/predicates only |
| Ownership | Static left-operand/unary-operand nominal view; no imported extension-operator search | Direct class/datatype members, including enclosing type parameters |
| Instance/static | Both; instance slots can later inherit; static symbolic slots belong to their owner | Both directly declared forms |
| Equality | All explicit and implicit equality remains intrinsic; named `eq` for a different relation | Same rule |
| Comparisons | `< <= > >=` individually declared; no implicit reversal or law | Fallback only; intrinsic datatype `<`/`>` still win when applicable |
| Intrinsic escape | Contextual `builtin.<op>(...)`, before non-equality override support | Deferred; existing intrinsic spellings/casts suffice |
| Chains | Existing grammar and short-circuit conjunction of adjacent edges | Supported for supported function comparisons |
| Compound assignment | Capture location and old value once; ordinary call/store expansion | Rejected |
| Methods | Later arithmetic whole-RHS calls only; no nested effects | Annotated methods rejected; function-by-method remains an ordinary function |
| Traits | Ordinary dispatch and proved library lemmas, with recorded slot identity | Deferred; no inferred operator capability from a trait bound |
| Integer bitwise | #193's independent intrinsic capability and integer semantics | No dependency; all six user bitwise symbols rejected |
| Semantic representation | Ordinary primitive expression or ordinary call, never an overloaded semantic opcode | Same invariant |

The complete design remains deliberately smaller than general overload
resolution: one slot per symbol/effective arity in a nominal view; no
result-directed search, user conversions, literal conversions, right-operand
search, or method type parameters. Its selection rule is specified separately
from milestone availability. Accepting an unsupported attribute and quietly
ignoring its requested mode is never the enabled-mode behavior.

The changes here are confined to this design document. Existing explicit-call
example files in PR #239 remain unchanged. No product source, expected-verdict
file, release version, or proof assumption changes in this revision.

## 2. Declaration syntax and eligibility

### 2.1 Attribute grammar and disabled behavior

The declaration form uses existing attribute syntax, with no new Coco/R token:

```ebnf
OperatorAttribute = "{:" identifier("operator") StringLiteral
                    [ "," StringLiteral("override") ] "}" ;
```

The bracketed mode is reserved for the later non-equality override milestone;
the first PR rejects it. With the feature enabled, validate literal arguments,
arity, callable kind, owner, symbol, and duplicate attributes. Reject unknown
modes, repeated operator attributes on one member, and member type parameters.
With the option **off**, `operator` remains an ordinary unknown attribute:
perform no new validation, suppression, warning, lookup, or serialization change.
Even malformed operator-looking attributes follow the old path. This distinction
is required for byte-identical disabled output, including errors.

```dafny
datatype Vec = Vec(x: int, y: int) {
  function {:operator "+"} plus(b: Vec): Vec { Vec(x + b.x, y + b.y) }
  function {:operator "-"} neg(): Vec { Vec(-x, -y) }
  function {:operator "*"} scale(k: int): Vec { Vec(x * k, y * k) }
}
```

These are proposed declarations, not syntax claimed to work in the pinned
compiler. Today the attributes are inert; named calls remain usable.

### 2.2 Owners, members, and arity

The eventual owners are classes, inductive datatypes, codatatypes, newtypes, and
traits where the ordinary member is legal. The first PR supports only direct
members of classes and inductive datatypes; section 12 gives the further view
restrictions. Subset types and transparent type synonyms introduce no slots.
A visible subset uses its underlying nominal owner's slots and retains ordinary
result-subtype obligations. Built-in types and module-level extension functions
cannot acquire operators.

Internally, an unrevealed owner may be represented by an `AbstractTypeDecl`
constructed by `AST/Modules/ScopeCloner.cs`. That is an export view, not permission
to discover hidden members. Later trait support permits a source abstract type
to inherit and implement a trait's operator-bearing member; this does not add a
new independently annotated standalone abstract-type owner. Full view transport
is required before accepting such uses.

| Member | Unary `-` | Binary symbol |
|---|---:|---:|
| Instance function/predicate | No explicit operand | One explicit operand |
| Static function/predicate | One explicit operand | Two explicit operands |
| Later single-result arithmetic method | Same effective arity | Same effective arity |

The instance receiver is the first operand. A static member's first formal must
have its declaring owner's instantiated nominal type, after expanding
transparent synonyms/subsets; for reference owners it may be nullable. Static
slots are not inherited symbolically. The other operand and arithmetic result
may have different types: `Vec * int -> Vec` is useful. Two different `*` slots
on `Vec` are still an ambiguity, even if their other operand/result types differ.
Ordinary enclosing-owner type parameters are allowed. No overload search uses
the expected result type.

Use positional operands, no default or `nameonly` operands, and no member-level
type parameters. Ghost functions/formals remain subject to ordinary ghost-use
checks. Ordinary predicates, opaque functions, and functions with a by-method
implementation are eligible. A by-method function is not an effectful expression:
its existing function contract and implementation checks still apply.
Constructors, lemmas, iterators, extreme predicates/lemmas, and two-state members
retain named syntax. Later method operators must have exactly one result;
comparisons are always functions/predicates returning `bool`.

### 2.3 Symbols and collision policy

| Symbol | User declaration | Intrinsic/collision policy |
|---|---|---|
| Binary `+ - * / %` | Function; later single-result method | Built-in first; later explicit non-equality override may take priority |
| Unary `-` | Function; later single-result method | Separate arity; preserve intrinsic negative-literal handling |
| `< <= > >=` | Function/predicate returning `bool` | Each has its own slot; no generated order laws or operand reversal |
| `== !=` | **Forbidden in enabled mode, with or without `"override"`** | Existing equality/inequality everywhere; use a named relation |
| `+= -= *= /= %=` | No declaration | Later syntax for the selected binary operation followed by assignment |
| `& \| ^`, unary `!`, `<< >>` | Function/predicate fallback after B1; ordinary by-method allowed | Existing intrinsic families win; user shift count has the declared parameter type; #193 stays independent (19) |
| `[]` | Function/predicate, one or more indices; direct owners after E1 | Existing indexable families retain their errors/obligations; one multi-index call differs from nested calls (15) |
| `[:=]` | Pure function, indices followed by value; E2 expression form has one index | Existing sequence/map/multiset update wins; nested calls need no batch law (16) |
| `[_.._]`, `[_..]`, `[.._]`, `[..]` | Four fixed function/predicate slice shapes after E2 | Pass only written bounds; no invented zero, length, optional value or slice law (16) |
| `()` | Function/predicate application, zero or more arguments, after E1 | Named calls and existing arrows keep priority; no synthesized requires/reads projections (17) |
| `[]:=` | Zero-result method setter in M6b; no read-expression use | Singleton statements only initially; value-owner destinations instead use pure `[:=]` and whole-root assignment (18) |
| Other symbols/spellings | Forbidden unless explicitly admitted in the addendum | No membership, conversion, cardinality, short-circuit, prefix-equality or arbitrary-fixity extension |

`!=` remains the existing complement of intrinsic equality. It is not derived
from a user `eq`. Separately declared `>` and `>=` avoid right-operand lookup and
evaluation-order changes. A library may implement `greater(b)` as an ordinary
call to `b.less(this)` and prove their relationship.

A fallback declaration does not hide an applicable primitive. For a datatype,
`a < b` may therefore mean rank even if `less` has a fallback attribute; `a <= b`
can call `le` because datatype `<=` has no intrinsic interpretation. A failed
compiled-rank restriction does not trigger fallback. In a numeric newtype,
ordinary same-type `+` continues to mean the existing arithmetic, not modular
addition. Hover should identify the selected intrinsic; named calls always give
an explicit way to request the member. The first PR rejects **the override
request**, not an otherwise valid fallback declaration merely because a
particular use selects an intrinsic.

Later non-equality override is deliberately stronger: once a unique visible
slot requests it, ordinary call errors are errors and never cause fallback. The
attribute must be inherited and preserved as signature metadata, not as an
accidental property of whichever implementation body is visible.

### 2.4 Alternatives

**Attributes versus dedicated syntax.** A dedicated `operator *(...)` declaration
is prominent and could encode arity directly. It also needs callable names,
export rules, parser productions, and contextual-keyword/error-recovery design.
The attribute preserves an ordinary name for explicit calls, proofs, visibility,
refinement, and tools. It introduces no scanner reservation or declaration
ambiguity. Its disadvantages are strings and less visible intent; enabled
validation and hover address them without altering disabled behavior. Prefer the
attribute. Reserved names such as `op_Addition` offer no additional capability
and unnecessarily constrain naming.

**Static versus instance.** Instance-only is enough for algebraic traits, but
nullable reference operands and some heterogeneous operations are awkward.
Static-only avoids a receiver check but does not express existing trait-member
implementation obligations. Both are small extensions of ordinary call lookup;
keep both for direct owners, and reserve polymorphic slots for instance members.
Do not invent a static typeclass/dictionary mechanism.

**Functions versus methods.** Functions give the useful nested arithmetic syntax
with ordinary purity/reads semantics, and by-method supplies verified efficient
execution. Whole-RHS method sugar is possible but adds statement resolution and
evaluation obligations. Defer it with compounds; do not make it a prerequisite
for rational/matrix arithmetic. Never lift arbitrary method effects out of an
expression tree.

## 3. Uses, precedence, chains, and method contexts

### 3.1 Existing expression grammar

Keep `RelationalExpression`, `ShiftTerm`, `Term`/`AddOp`, `Factor`/`MulOp`, and
`UnaryExpression` precedence and associativity in `Source/DafnyCore/Dafny.atg`.
For direct rational members, the first milestone can resolve:

```dafny
a * b + c <= d
// a.times(b).plus(c).le(d)
```

The comparison is fallback `<=`, not datatype rank and not an override. Unary
`-a` selects a unary slot; do not first invent `0 - a`. Preserve existing negative
numeric literal behavior on the intrinsic path (`LiteralExpr.cs`,
`NegationExpression`, and inference completion).

Every source `==`/`!=` follows the ordinary intrinsic path, including one whose
operands contain operator calls: `(a + b) == c` compares the returned values
intrinsically. There is no special ghost or executable selection rule.

### 3.2 Chaining

Retain today's ascending/descending chain restrictions, at most one inequality,
and prefix-equality/disjointness rules. Expand adjacent comparisons using the
current short-circuit conjunction semantics:

```dafny
a <= b <= c
// a.le(b) && b.le(c), when both fallback slots are selected
```

Resolve each eligible edge independently; an equality edge is intrinsic. No law
relates the endpoints. The second call's precondition may use the first edge's
truth. The current expansion does not promise to evaluate the middle source
expression only once; this proposal keeps that behavior. Comparisons cannot be
methods, so it adds no method-effect sequencing to a chain.

`AST/Expressions/Operators/ChainingExpression.cs::ComputeDesugaring` constructs
edges itself. Its enabled source edges must use the source-operator factory;
wrapping only `BinaryExpr` nodes created in `Dafny.atg` would miss them. Conjunctions
and disjoint-chain helper unions stay generated intrinsics. Preserve a selection
record **on the source chain for each edge**, not only on disposable desugaring
nodes. `calc` connectors are a separate construct and remain intrinsic (6.3).

### 3.3 Later arithmetic methods

Only where today's single-result method call is permitted may a later method
operator supply the whole RHS:

```dafny
z := a * b;
var z := a * b;
a *= b;
```

When `*` names a method, nested `a * b + c`, assertions, predicates, and return
expressions requiring a function remain errors. Selection precedes the check of
call kind: the resolver must not choose a different callable merely because the
context rejects a method. Function-by-method can already support ordinary nested
function uses.

The legacy whole-RHS route recognizes `ApplySuffix` and `MethodCallInformation`
in `AST/Statements/Assignment/AssignStatement.cs::Resolve`; the refreshed route
is in `Resolver/PreType/PreTypeResolve.Statements.cs`. Extend those routes later.
Do not manufacture a `FunctionCallExpr` pointing to a method. The first PR rejects
all operator-annotated methods, even where their named calls are legal.

### 3.4 First-milestone module-attribute restriction

At the pin, module attributes still use the **legacy expression resolver even
with refresh enabled** (`ModuleDefinition.Resolve` calls
`ModuleResolver.ResolveModuleAttributes`). M1 therefore keeps operators in module
attribute arguments intrinsic-only. Ordinary named calls remain available there;
member bodies elsewhere still use the normal operator rules. This is a temporary
context restriction, not a new ghost/executable distinction.

Immediately before `Dafny.atg` constructs a `ModuleDefinition`, both leading `@` attributes and
post-keyword attributes are already collected in `dmod.Attributes`. On the enabled
path, a parser helper recursively converts source operator wrappers in those
attribute argument trees back to their intrinsic `BinaryExpr`/negation syntax,
and rebuilds chains with an intrinsic-only edge-factory policy retained by their
clones. Keep origins, grouping, argument order, nested named calls and attribute
text. Traverse full pre-resolution syntax, including nested lambda/comprehension
attributes/triggers and statement-expression statements, not only resolved-child
`SubExpressions`. If normalization is placed after module construction, assign the
new attributes to `module.Attributes`; updating only `dmod.Attributes` is too late.
Do this after the declaration context is known, not by globally disabling
operators in all attributes (function triggers are a different context).

Thus `module {:foo 1 + 2} M {}` still reaches the legacy resolver as ordinary
intrinsic syntax. A module-attribute expression that would require a user `+`
is rejected by intrinsic resolution; its named-call form can be used instead.
No unknown `OperatorSyntaxExpr` may reach that legacy phase. Add nested primitive,
chain, leading-attribute and user-operator negative tests. A later shared-policy
legacy integration may remove this restriction.

## 4. Naming the intrinsic operation

### 4.1 Deferred, uniform escape

Before introducing non-equality override, add an option-gated contextual form:

```dafny
builtin.+(a, b)
builtin.-(a)
builtin.-(a, b)
builtin.<(a, b)
builtin.<=(a, b)
builtin.==(a, b)  // redundant but uniform: == is always intrinsic
builtin.!=(a, b)
```

It selects the existing intrinsic for the supplied static types. It is not
module/member lookup, cannot be shadowed, and is not a first-class operator value.
`builtin.f` and an identifier named `builtin` retain ordinary meanings. Only the
exact previously invalid `builtin . operator (` pattern is contextual when
enabled. Arguments resolve normally; only the outer operation is forced.

The escape does not unwrap an opaque type, waive compiled-equality support,
reveal a function, change the heap, or waive subset/newtype constraints. For an
accessible numeric representation, existing explicit conversions remain useful:

```dafny
(a as int) + (b as int)
builtin.+(a as int, b as int)
```

`builtin.+(a,b)` with `a,b:N` keeps the intrinsic result type and obligations for
`N`; it does not promise an `int` result. Since equality stays intrinsic,
`c == null` already means reference identity and requires no special escape.
Named null-tolerant relations may use ordinary static predicates on `C?`.

The first PR defers this entire syntax. No intrinsic is hidden by its fallback
operators, so that deferral leaves every intrinsic reachable. Any future addition
of escape forms for #193 symbols must use #193's ordinary capability checks;
it must not activate integer-bitwise implicitly.

### 4.2 Alternatives

| Form | Benefit | Cost or limitation |
|---|---|---|
| `===` / `!==` | Short identity notation | Unnecessary when `==` is intrinsic; new scanner tokens and no arithmetic escape |
| `builtin.<op>(...)` | Uniform, explicit, existing tokens, ordinary argument grouping | Verbose and needs a contextual parser hook |
| `a ==@builtin b` | Retains infix shape | New interactions with labels, chaining, precedence, and recovery |
| Casts alone | Already work for accessible numeric representations | Cannot choose rank rather than a later datatype comparison override at the same static type |
| Expression attribute | Might reuse the attribute vocabulary | Dafny lacks a general attribute-on-expression form; placement and scope would be new design work |

Prefer the contextual call for the complete design. Equality's permanent
intrinsic rule lets the first PR omit it altogether. This is a reduction in the
initial parser work, not an argument to leave arithmetic intrinsics inaccessible
once arithmetic overrides are implemented.

## 5. Resolution, ownership, and stable choices across views

### 5.1 Operator slots

Use one shared declaration descriptor/index, not separate policy in each
resolver:

```text
OperatorKey = (symbol, unary-or-binary)
OperatorDecl = (key, priority, declaring member, original override slot,
                instance/static, function/method)
```

Validate attributes after signatures are available. For later trait support,
build effective instance slots from the ordinary inherited member table, using
`ModuleResolver.RegisterInheritedMembers`, `AddToInheritedMembers`, and
`ProcessInheritedTraitMembers`. Deduplicate the same original slot in a diamond;
substitute parent arguments as named member resolution does. A normal member
override inherits the operator symbol/arity/priority. Repeating an attribute must
agree. Two unrelated visible slots with the same key are ambiguous; their names,
other-operand types, result types, or function/method kinds do not break the tie.

Static operators are helpers owned by the declaring nominal type, not abstract
static implementation obligations. Ordinary static names may be inherited, but
symbolic slots are not: inheriting a name must not silently change its first
formal's owner. Instance slots supply the later trait interface.

Metadata belongs to the visible member signature, not its body. Filter the
ordinary export/member view before considering candidates. Do not scan hidden
members or unwrap opaque representations. Cache raw descriptors separately from
instantiated lookup; the latter must include receiver substitution, visibility
view, and feature mode. The first PR indexes only directly declared slots on
supported concrete owners.

### 5.2 Selection, intrinsic applicability, and call checking

Every explicit `==`/`!=` goes directly to existing equality resolution. Every
generated primitive, including calc connectors, bypasses this machinery.
For an eligible enabled source unary/binary operator:

1. If the occurrence already has a saved selection, **replay it** as specified
   in 5.6. Do not run candidate discovery again.
2. Otherwise resolve operands once in their source lexical/heap-label context.
   Preserve nominal newtype identity; expand only visible transparent aliases
   and subsets. Use the static left/unary operand's nominal view, mapping a
   non-null class view to its owner. Never search right-operand declarations.
3. Find its unique visible slot, if any. A later bounded type obtains instance
   slots from its declared instantiated trait bounds. Multiple unrelated slots
   are an error, not a list on which to perform expected-result inference.
4. In the later non-equality override milestone, a slot explicitly marked
   `"override"` wins. All ordinary call checks then apply. An argument, ghost,
   receiver, precondition, result or method-context failure is an error.
5. Otherwise, if the existing intrinsic is type-applicable, use that existing
   resolution path. Applicability is determined from static types/capability,
   not successful verification or compilability. Division by zero, ghost rank
   comparison in runtime code, and a failed #193 result/count check do not
   cause fallback.
6. Otherwise choose the visible fallback slot, or report a missing operator.
   Resolve it as the corresponding ordinary named call, with identical
   receiver/argument/type-view checks. Record its selection and emit the
   ordinary call before semantic consumers run.

No user conversion, numeric-literal conversion, solver proof, expected-result
search, or global imported-function search participates. Ordinary declared
upcasts and subset inclusion are ordinary call compatibility. A selected result
is checked against its context; it does not select a different slot. `0 + a`
does not invent an embedding of integer zero into a rational datatype.

Some type constraints are necessarily delayed. Retain one pending operator
obligation, not speculative whole-expression reruns. Capture the existing
call-resolution context and already resolved operands before the lexical scope
is popped; later finalization must not synthesize an unresolved name and call
`ResolveApplySuffix` under the wrong scope. Finalize only once its receiver head
is determined by ordinary evidence. If it remains underdetermined, require a
receiver annotation or named call. Section 13.3 gives the scheduling rule and
its non-heuristic failure boundary.

For #193, a validated semantic-width capability supplies an intrinsic family;
#194 does not infer that family from a public operator attribute. The selection
record distinguishes its semantic width from a native storage carrier. The
four option combinations and failure rules are in 6.5.

### 5.3 Newtypes, subsets, and visibility

`AST/TypeDeclarations/NewtypeDecl.cs` is member-bearing and nominal.
`SubsetTypeDecl.cs` is a constrained synonym. A visible subset of `T` uses T's
operator view; a subset of `N` stops at nominal newtype N. The selected function
returns its declared result type. There is no automatic lift back into a subset
or newtype and no inferred preservation of its predicate.

Find the nominal owner before `NormalizeToAncestorType`/`UrAncestor` erase it.
For intrinsic selection, retain the existing numeric-newtype semantics; for
member calls, retain the ordinary receiver conversions and result type. Respect
`Types.cs::NormalizeExpand` and `NewtypeDecl.RhsWithArgument` visibility checks.
An escape or an attribute cannot reveal a hidden representation.

Ordinary `--general-newtypes`, `--general-traits`, and backend restrictions
remain. This feature does not enable those flags or finish incomplete general
trait support. The first PR defers all newtype-owned slots, but plain intrinsic
newtype expressions remain unchanged. The modular example in section 10 is
explicitly a later non-equality override example.

### 5.4 Traits and bounded types: later prerequisites

The parser accepts bounds such as `T extends Ordered<T>`, but at the pin both
member-lookup implementations expect a `TopLevelDeclWithMembers`, and
`TypeParameter` is not one. Existing bounded-polymorphism tests require an
explicit cast before selecting the trait member. The legacy resolver also lacks
some declared datatype-to-trait/type-parameter-to-trait compatibility supported
by the refreshed resolver. Attaching a trait `Function` to a raw-T call can
reach an invalid `Types.cs::AsParentType` assumption; that is not an implementation
of bound-aware lookup.

A later trait PR must enumerate instantiated bounds, deduplicate slot lineage,
reject unrelated collisions independently of bound order, and introduce the
ordinary bound-typed receiver view before call checking. Retain the original
expression for every well-formedness obligation. Support its explicit named-call
expansion under the same mode, so operator correctness can be compared to real
source. Reuse existing declared-parent substitution and verification/backend
conversions; add no unchecked cast.

Legacy parity needs feature-gated declared-view support in its subtype/cast and
`Equatable`/`EquatableArg` paths, not a global relaxation of unrelated type
relations. It is a separate PR, not an unstated premise of the first milestone.
The refreshed-only option must fail early and clearly when used with the legacy
resolver.

Generic selection is fixed in the generic body's static view. A later
`T extends Add<T>` operator call stays a call after specialization to a numeric
newtype, even though a newly written expression at that concrete newtype might
select intrinsic arithmetic. This is ordinary static resolution plus ordinary
instance dispatch, not specialization-time overload lookup. Equality is
intrinsic in both cases; `T(==)` does not mean a library-equivalence capability.

### 5.5 Diagnostics

Use stable new identifiers only on enabled paths. Representative messages:

```text
--user-defined-operators currently requires --type-system-refresh
operator '==' cannot be declared; use a named equivalence predicate
operator '!=' cannot be declared; use a named predicate or its negation
operator mode 'override' is not supported in this milestone
operator '*' is not defined for left operand type 'Rat'
operator '+' has ambiguous slots: A.plus and B.add
operator '<=' must return bool
operator '-' has invalid operand arity
operator members cannot have method type parameters or default/nameonly operands
operator receiver type is not determined; annotate it or use a named call
operator use requires an unsupported hidden or abstract owner view
operator selection cannot be transported into this export/refinement view
```

Later stages add trait, method-context, and priority-agreement diagnostics.
Reuse ordinary argument/contract errors at the operator token with the selected
member as related information. Hover should distinguish an intrinsic from a
member; an applicable intrinsic is not silently replaced to avoid an error.
Disabled diagnostic identifiers, text, locations, ordering and exit codes remain
exactly those of the baseline.

### 5.6 Concrete binding records and every cloning path

A bare `ResolvedExpression` or `MemberDecl` pointer is insufficient. The pin's
`RefinementCloner` deliberately discards ordinary resolved fields; exports
rebuild owner views; chains recompute desugaring. The saved object is an
**immutable replay recipe**, separate from `ResolvedExpression`:

```csharp
// Design types, not existing definitions or a product patch.
readonly record struct DeclKey(Declaration Origin, ModuleViewId View);
readonly record struct BinderKey(Declaration Origin, int Index, ModuleViewId View);

abstract record FrozenType;
record ParameterType(BinderKey Parameter) : FrozenType;
record NominalType(DeclKey Declaration,
  ImmutableArray<FrozenType> Arguments) : FrozenType;
record PrimitiveType(PrimitiveTypeKind Kind) : FrozenType;
// Add immutable collection/arrow constructors wherever a receiver argument needs them.

abstract record OperatorSelection(OperatorKey Key);
record IntrinsicSelection(OperatorKey Key, IntrinsicFamily Family,
  ImmutableArray<FrozenType> OperandViews,
  int? SemanticBitWidth) : OperatorSelection(Key);
record MemberSelection(OperatorKey Key, DeclKey SelectedMember,
  DeclKey OriginalSlot, CallForm Form, FrozenType ReceiverView,
  ImmutableArray<FrozenType> OwnerArguments) : OperatorSelection(Key);

// On OperatorSyntaxExpr, independent of CloneResolvedFields:
OperatorSelection? Selection;
SelectionPhase Phase; // FreshSyntax, AwaitingInitialInference, or Frozen
// On ChainingExpression: one such entry per source edge.
// On a future CompoundAssignStmt: its underlying operation selection.
```

`Origin` is declaration identity within this resolution transaction, not a source
name or hash assumed globally unique. `ModuleViewId` distinguishes actual module,
export, abstract-signature and refinement instantiations: two refinements of the
same origin must not collapse to one binding. Allocate these identities only on
the enabled path. They are not serialized object addresses. The checked clone
context maps origin/view/binder keys to the target environment; original source
can reconstruct them on an artifact round trip.

`OriginalSlot` identifies normal member-override lineage; `SelectedMember` is
the signature selected at the occurrence. Freezing a trait slot does **not**
freeze its runtime implementation body. `FrozenType` contains neither a mutable
`PreType`/proxy nor an old local-variable pointer. Rebuild types using the target
binder/declaration correspondence. Provisional inference state may select a slot
while solving constraints, but publish this immutable record only when its view
recipe is complete, before the semantic-analysis boundary.

`IntrinsicFamily` is semantic identity, not a blindly copied backend opcode.
Intrinsic equality under a legal type refinement remains intrinsic equality;
rank `<` must not turn into numeric `<` or a user predicate. Replay checks that
the same intrinsic meaning is valid in the target view. #193's family also keeps
its semantic bit width, independently of `NativeType.Bitwidth`. If the target
view cannot express the saved operation, report inconsistency. Member replay
resolves exactly the corresponding selected named member/slot and applies all
ordinary receiver, type, argument, visibility, ghost, frame and termination
checks. Neither replay route falls back to another operation.

**Declaration correspondence.** Add an `OperatorCloneContext` shared by a whole
module/signature construction, with source-to-final owner/member/binder/view
maps and deferred fixups. Register declaration shells first where possible;
otherwise finish fixups only after final signature objects exist. Cloning an
expression often precedes cloning the member it calls.

Do not implement this with `CloneMember(member, true)`. In `AST/Cloner.cs`,
`CloneMember` uses a private `memberClones` cache and `GetOrCreate`; a reference
clone can return and cache the *old* declaration before its definition clone is
created. `GetCloneIfAvailable` for a type parameter also silently falls back to
the original, so it cannot certify checked transport. The new correspondence
must explicitly name the final objects. In particular:

- `RefinementTransformer.MergeClass` creates final replacement functions and
  methods and must register them after construction; its absent-member path
  sets `MemberDecl.RefinementBase`.
- `MemberDecl.RefinementBase` and `OverriddenMember` supply checked lineage
  edges, not an all-purpose complete environment map.
- The pin's synonym-target refinement branch maps an abstract owner to the
  checked target with its type-argument substitution; register that final target.
- Do not reuse cardinality-specific provenance fields: their equivalence
  relation is not the operator-view correspondence.
- `ScopeCloner` must map to the *final* `AbstractTypeDecl` view it creates, not
  to an intermediate clone. If the view hides a selected callee or intrinsic
  capability, export consistency must fail.
- `CloneDeclarationForAbstractSignature` creates a new
  `AbstractSignatureCloner` per declaration. The shared correspondence must
  therefore belong to signature construction, not one cloner instance. Its
  manually built `AbstractModuleDecl` facades also need mapping.

These are the **14 `Cloner` classes at the pin**, including indirect subclasses.
Paths are relative to `Source/DafnyCore/`:

| Class / path | Required treatment |
|---|---|
| `Cloner`, `AST/Cloner.cs` | Wrapper/chain constructors transport selections independently of `CloneResolvedFields`. Unresolved parsed clones retain null; resolved semantic copies retain the actual ordinary call/intrinsic. |
| `ClonerKeepParensExpressions`, `AST/Cloner.cs` | Preserve source records through recursive children, not solely on a disposable parenthesis. |
| `DeepModuleSignatureCloner`, `AST/Cloner.cs` | Carry module-view/declaration correspondence through signatures and alias/facade roots; never find a saved target by a fresh name search. |
| `ClonerButDropMethodBodies`, `AST/Cloner.cs` | Dropped bodies may drop their records. Retained contracts, frames and decreases keep records; `ExpectContracts` uses this with resolved fields. |
| `ScopeCloner`, `AST/Modules/ScopeCloner.cs` | Filter visibility and register final owner/member export views, including manually created abstract owners. Fail a surviving untransportable selection instead of rebinding. |
| `AbstractSignatureCloner`, `AST/Modules/AbstractSignatureCloner.cs` | Same scope rule; removed bodies/exports need no record. Share correspondence across all per-declaration instances and manually rebuilt facades. |
| `RefinementCloner`, `Rewriters/RefinementTransformer.cs` | Its `base(false, false)` is intentional. Keep records separately, map final merged declarations, then replay after signatures exist. Fresh refining source has no inherited selection. |
| `SubstitutingCloner`, `AST/SubstitutingCloner.cs` | Substitute operand identifiers/types with normal rules and transport the view recipe; it holds no captured local-variable object. |
| `ClonerButIVariablesAreKeptOnce`, `Resolver/ClonerButIVariablesAreKeptOnce.cs` | Inherit base/parenthesis handling despite retained/recloned variables; no new lookup. |
| `QuantifiedVariableDomainCloner`, `AST/Expressions/Variables/BoundVar.cs` | Origin wrapping does not alter selection. Parsed unresolved occurrences legitimately carry null. |
| `QuantifiedVariableRangeCloner`, `AST/Expressions/Variables/BoundVar.cs` | Same rule for range-origin wrapping. |
| `ExtremeCloner`, `AST/ExtremeCloner.cs` | Classify the ordinary resolved expression. Only true intrinsic equality gets prefix/coinductive treatment; source portions retain replay records. |
| `ExtremeLemmaSpecificationSubstituter`, `AST/ExtremeLemmaSpecificationSubstituter.cs` | It explicitly bypasses concrete wrappers to clone `.Resolved`. This is safe only if the resulting ordinary named call/intrinsic retains its meaning when re-resolved; never emit bare unpinned infix text. |
| `ExtremeLemmaBodyCloner`, `AST/ExtremeLemmaBodyCloner.cs` | Keep source records through focal/recursive rewriting. User comparisons are ordinary calls and do not enter primitive equality transformations. |

`AST/Substituter.cs` is not a `Cloner` subclass, but is another essential route.
Its concrete-syntax case substitutes the resolved child into a resolved
parenthesis, and its function-call case preserves the selected Function while
substituting receiver/arguments/types. It may discard the surface record because
this is semantic lowering, provided the result never returns to user lookup.
The same rule applies to match flattening: keep the normal call, not a user
symbol with a lost choice.

`ConcreteSyntaxExpression` copies `ResolvedExpression` only when
`CloneResolvedFields` is true. The new selection must be copied outside that
condition. `ChainingExpression` rebuilds `E` through `ComputeDesugaring`; restore
selections from the source edge array while rebuilding. A future compound keeps
its operation record on the statement, not only a temporary generated node.

**Lifecycle:**

1. `AST/Grammar/ProgramParser.cs` captures `AfterParsingClone` with a plain
   cloner before resolution; `Pipeline/Compilation.cs` also keeps parsed
   pipeline state. These genuinely unresolved clones have no selections yet.
2. `Resolver/ProgramResolver.cs` resolves modules in dependency order.
   `RefinementTransformer.PreResolve` sees an already resolved base, so its
   inherited occurrences do have selections before cloning.
3. First resolution commits the choice and emits the ordinary semantic AST.
   Export/refinement/signature source clones copy and transport the record even
   while discarding other resolved fields.
4. `ModuleResolver.CheckModuleExportConsistency` creates a `ScopeCloner`,
   re-resolves its view, and accepts an `EffectiveModule` only after consistency
   succeeds. Transport must be installed before that re-resolution.
5. Semantic-only copies/substitutions can erase wrappers only after retaining
   the actual selected calls/intrinsics. No later pass repeats priority lookup.
6. `Backends/Library/LibraryBackend.cs` serializes `AfterParsingClone`. With
   compatible language modes, resolving original dependency/refinement source
   reconstructs choices in order. A flattened inherited source view must instead
   serialize a stable replay encoding or print explicit named calls/intrinsic
   escapes. Bare infix reprinting is insufficient.

A parsed unresolved occurrence and a lost binding are distinct states. A
`Frozen` occurrence requires a non-null selection. A clone context marked
`ResolvedSource` requires records for every eligible source occurrence before
copying, independently of the occurrence's field defaults; it must fail on a
missing record rather than label the copy fresh. Only the parser/known
pre-resolution clone path creates `FreshSyntax`. Semantic-only clones keep the
ordinary expression and never return to lookup.

This needs no preliminary whole-program second resolution. It does need complete
checked correspondence before a cloned source view is accepted. A missing map
is an error, not permission to reuse an old pointer or choose a new candidate.

### 5.7 Concrete wrong-verification programs if records are dropped

**Refinement without equality override.** This prospective regression uses a
trait-owned `+`; there is no independently annotated standalone abstract type:

```dafny
abstract module A {
  trait Add<T> {
    function {:operator "+"} plus(y: T): T
  }
  type T extends Add<T> {
    function plus(y: T): T
  }
  lemma same(x: T)
    ensures x + x == x.plus(x)
  { }
}
module B refines A {
  newtype T = int {
    function plus(y: T): T {
      ((this as int) + (y as int) + 1) as T
    }
  }
  lemma bad() {
    same(0);
    assert false; // Must fail.
  }
}
```

In A, both sides of the postcondition are the same call. If B discards the
selection and repeats built-in-first lookup, its left side becomes numeric
addition while its right side remains `plus`. Calling `same(0)` then publishes
`0 == 1`. `RefinementCloner` drops resolved fields;
`Verifier/BoogieGenerator.Methods.cs` guards inherited implementation
postconditions with `$_reverifyPost`; `RefinementTransformer.ContainsChange`
recognizes extended predicates, not this hypothetical operator reinterpretation.
Correct replay keeps the left side as the call. M1 rejects this feature-bearing
refinement; the later trait/refinement stage must accept the positive expansion
and still reject `assert false`.

**Export without refinement or override.**

```dafny
module M {
  datatype D = D {
    function {:operator "<"} less(other: D): (r: bool)
      ensures r
    { true }
  }
  lemma rank_irreflexive(x: D)
    ensures !(x < x)
  { }
  export provides D, D.less, rank_irreflexive
}
module Client {
  import M
  lemma bad(x: M.D) {
    M.rank_irreflexive(x);
    assert false;
  }
}
```

Inside M, intrinsic datatype rank wins and the lemma is true. In the opaque view,
rank is unavailable. A faulty export clone could select visible `less` instead,
while that member's contract makes it true, so the exported lemma yields false.
Correct intrinsic replay rejects the inconsistent export. This example explains
why "no refinement in M1" does not justify omitting selection preservation.
M1 must reject the hidden view or its unavailable intrinsic; revealing D yields
a positive export-consistency test (the deliberate false assertion still fails).

Both feature-bearing programs are **unchecked prospective regression programs**:
operator syntax is not implemented. The first program's today's-syntax control
replaced only `x + x` by `x.plus(x)` and produced **3 verified, 1 expected error
at `assert false`**. The second, with the inert attribute omitted, produced its
**two expected resolution errors** for the opaque rank use/export inconsistency.
These controls ran on public release
`4.11.0+fcb2042d.review.bf47de16`, Z3 5.1.0, refreshed resolver (and
`--general-traits=datatype` for refinement), additional axioms off. They are not
claimed tests of a binary built from the source audit pin.

## 6. Equality, intrinsic reasoning, and issue #193

### 6.1 Equality recommendation and exact site rules

**Reject user declarations for both `==` and `!=`, on every eligible owner and
in every mode of the enabled feature.** Use ordinary `eq`/`equiv` predicates for
non-structural relations. This is the recommended final policy, not merely a
first-PR restriction. Disabled compilers continue treating the attribute as
unknown metadata, as required by compatibility.

The old proposal could make a uniform source `==` an ordinary predicate call
without automatically assuming substitution. That is a possible sound notation
feature. It cannot simultaneously mean "all ghost equality in specifications is
intrinsic" and "an explicit `a == b` selects the user member" without a further
context-dependent rule. Nor can "compiled equality stays intrinsic" cover the
compiled form of a source expression that was verified as a user predicate.
If equality override were retained, **every explicit use**, including requires,
ensures, assertions, old expressions, and compiled expressions, would have to
keep its selected call; only generated machinery could remain intrinsic. This
revision chooses the simpler uniform intrinsic rule instead.

Here **I** is the existing equality for the static type under the ordinary
visibility/typing rules, and **E** is an explicitly named user predicate call.
They are different relations. No coercion or mode changes E into I. Intrinsic
does not mean "shallow fields for every type": class equality is reference
identity, inductive datatype equality compares its constructor/fields, collection
equality uses its existing extensional definition, and codatatypes retain the
existing coinductive equality and compiled-use restrictions. Existing equality
on abstract/generic/function-containing values retains its current rules; this
proposal supplies no new extensionality or runtime equality algorithm.

| Source/implicit site | Exact rule in the recommended design |
|---|---|
| `a == b` in an expression, statement RHS, function body, `if`, `print`, or return value | I, regardless of whether the expression is ghost or compiled. Operands may contain ordinary user calls. |
| `a != b` | Existing negation of I, including existing collection-specific handling; never negation of a library `eq` unless written `!a.eq(b)`. |
| `requires`, `ensures`, invariants, assertions, assumptions, lemma contracts, witnesses, type/subset/newtype constraints, quantified ranges/bodies | Every explicit `==`/`!=` is I. Writing `a.eq(b)` explicitly requests E, with ordinary reads, preconditions, visibility and termination. |
| Equality in a mixed/chained relation | That edge is I. Arithmetic/order edges use their own recorded selection. Chaining contributes conjunction, not substitution/transitivity of a user relation. |
| `calc`, default connector or explicit `==`/`!=` connector | Existing intrinsic connector and composition rule. `calc <=` is also intrinsic; it never calls an attributed `le`. |
| Expressions and hints within `calc` lines | Normal expression rules: explicit equality is I; named E remains E; an eligible arithmetic/order use may be a user call. |
| `==#[k]` / `!=#[k]`, generated codatatype equality and coinduction | Existing prefix/coinductive semantics, with ordinary restrictions; not member lookup. |
| `match`, constructor tests, literal patterns, pattern exhaustiveness, discrimination and generated field comparisons | Existing intrinsic constructor/value reasoning. A scrutinee or branch may call E, but that does not redefine matching. |
| Set/iset membership and extensional equality; multiset multiplicities and equality | Existing intrinsic element identity. A predicate inside a comprehension is evaluated normally; E does not change element identity or quotient the collection. |
| Sequence membership, equality, prefix relations, and element lookup | Existing intrinsic element equality. No element `eq` method is consulted. |
| Map/imap key membership, lookup, update, duplicate-key consistency, and equality | Existing intrinsic key equality and value equality. E cannot supply hashing or identify two different keys. |
| Set/map/multiset/sequence literals and comprehensions | Element/key/value expressions use ordinary calls; collection construction and identity remain intrinsic. `{a,b}` may have two elements even when E(a,b). |
| `T(==)` and equality-support inference | Existing compiled-equality capability, not an equivalence-trait requirement. A proved equivalence or user `eq` supplies no new `(==)` characteristic. |
| Generic bodies and later specialization | Explicit equality is I at the generic static view. Substituting a type with an `eq` member never changes that expression to a call. |
| `old(e)` / `old@L(e)` | Existing heap interpretation of `e`. `old(a == b)` is I on the operands evaluated by old's ordinary rules; `old(a.eq(b))` is E in that heap, with its ordinary call obligations. Old is not a blanket equality rewrite. |
| `fresh` / `allocated` | Existing allocation and reference-identity machinery. Computations producing their arguments may contain calls, but do not change allocation identity. |
| `unchanged` | Existing generated equality of the specified locations/fields, and intrinsic frame membership. The frame-producing expression may contain E or other calls. |
| `reads`, `modifies`, frame checks, heap succession | Existing object/field identity and frame-set membership. Explicit source computations retain their own ordinary meanings. |
| `decreases`, `decreases to`, lexicographic tie tests, generated call/loop termination checks | Existing intrinsic equality and well-founded orders on the computed measure values. A named E in a measure merely computes a Boolean; it does not replace the termination order. |
| Triggers, generated trigger terms, bound discovery, exact bounded pools | Explicit `==` is the existing intrinsic relation; a named E is an ordinary function application. E must never receive `EqCommon`-based exact-bound reasoning. |
| Assignment/forall postconditions, let substitution, refinement checks, generated proof equalities | Existing intrinsic equality. An already resolved user subexpression stays its call. No generated `BinaryExpr` participates in user lookup. |
| Compiled explicit `==`, generic equality, generated datatype equality, runtime collection equality/hash, null tests | Existing backend equality consistent with I, wherever compilable. User `eq` compiles as its ordinary function. Unsupported intrinsic equality stays an error; it does not fall back to E. |
| Export/refinement/library clones | The equality choice remains I. Calls to named E remain calls and retain normal declaration/view transport. Non-equality operators need the extra records in 5.6. |
| `builtin.==(a,b)` / `builtin.!=(a,b)` once the escape exists | Redundant explicit I. The escape does not waive type comparability or compiled-equality restrictions. |

The source basis for the distinction is explicit:
`Resolver/ModuleResolver.cs::ResolveOp` chooses `SetEq`, `MultiSetEq`, `SeqEq`,
`MapEq`, or `EqCommon`; `Verifier/Expressions/TranslateBinaryExpr.cs` handles
`EqCommon` with datatype/codatatype special cases;
`AST/Types/UserDefinedType.cs::SupportsEquality` controls compiled support.
`Verifier/BoogieGenerator.Decreases.cs::ComputeLessEq`,
`BoogieGenerator.ExpressionTranslator.cs` (`OldExpr`, `UnchangedExpr`, prefix
comparison), and `Resolver/BoundsDiscovery/BoundsDiscovery.cs` are examples of
implicit machinery that must remain untouched. These are existing definitions,
not new equality axioms supplied by #194.

### 6.2 A concrete misleading program, and why to remove that case

Consider noncanonical rational values:

```dafny
type Pos = d: int | 0 < d witness 1
datatype Q = Q(n: int, d: Pos) {
  predicate eq(b: Q) { n * b.d == b.n * d }
}

method equality_example() {
  var a := Q(1, 2);
  var b := Q(2, 4);
  assert a.eq(b);       // mathematical rational equality
  assert a != b;        // different representations
  assert b !in {a};
  assert |{a, b}| == 2;
  assert a.n != b.n;
}
```

With the old `{:operator "==", "override"}` on `eq`, the first assertion could
be spelled `assert a == b;` while the set still has two elements and `b !in {a}`.
For clarity, the old design's assertions would instead be:

```dafny
// Q.eq is annotated {:operator "==", "override"} in the superseded design.
assert a == b;
assert b !in {a};
assert a.n != b.n;
// assert b in {a}; // must fail
```

The natural conclusion `assert b in {a};` would fail, as would substitution of
`b` for `a` in `a.n == 1`. This is not evidence that ordinary call lowering is
logically unsound: the failed implications are exactly what that lowering should
refuse. It is evidence that an equality token which lacks equality's defining
substitution behavior is costly in a verification language. A ghost-only
intrinsic exception would make identical tokens mean different things between
specifications and executable expressions and would add another source of
surprise.

The recommended rule removes the case: `a == b` is false, `a.eq(b)` is true, and
the syntax states which relation is being used. A quotient collection can still
be implemented as a library with an explicit relation and a proved API. A library
can also choose canonical rational representations and prove that representation
equality coincides with mathematical equality. Neither facility follows from an
operator attribute or from an equivalence proof alone.

| Equality policy | Argument for it | Argument against it | Decision |
|---|---|---|---|
| Overrides on classes only | Familiar value-versus-reference distinction | Mutable/nontransitive/partial relations remain possible; excludes the main numeric-datatype use case without removing the semantic split | Reject |
| Overrides on all nominal types, uniformly in every explicit context | Convenient mathematical notation; can be sound as plain calls with no new laws | Loses substitutivity of `==`, differs from collection identity and calc, complicates generic/export tooling; needs escape and persistent bindings | Do not recommend |
| Intrinsic `==`/`!=`, named equivalence | One equality meaning in specs, execution and collections; ordinary congruence reasoning remains valid | Mathematical relations use a named call; noncanonical rationals cannot borrow `==` | Recommend |
| Allow override only after proving equivalence/congruence | Appears to recover familiar reasoning | Equivalence is insufficient; congruence for every observer in an open API essentially demands equality of all observations, not a mere notation feature | Outside this feature |

The displayed current-syntax program was checked on public release
`4.11.0+fcb2042d.review.bf47de16`, Z3 5.1.0, refreshed resolver, additional axioms
off: **2 verified, 0 errors**. Adding `assert b in {a};` gives **1 verified,
1 error** precisely at that added assertion. A separate checked runtime driver
prints `true false false 2 false` for named equivalence, intrinsic equality,
membership, set cardinality and numerator equality. These are existing-syntax
checks; the superseded override fragment is an unchecked design counterexample.

Confidence is high on the soundness and implementation simplification. The
remaining tradeoff is notation preference, not a claim that all equality
overloading is impossible or inherently inconsistent.

### 6.3 Why `calc` connectors stay intrinsic

`AST/Statements/CalcStmt.cs::BinaryCalcOp.ResultOp` has fixed composition rules.
`Verifier/Statements/BoogieGenerator.TrStatement.cs::TrCalcStmt` proves adjacent
steps then assumes the endpoint relation. It does not establish arbitrary user
relation transitivity, and its connector well-formedness path was not designed
to check an arbitrary user predicate's preconditions.

A relation "the integer representations differ by at most one" holds between
0 and 1, and between 1 and 2, but not between 0 and 2. It cannot use the intrinsic equality calculation rule.
Keep every connector intrinsic, including default equality and all `< <= > >=`
connectors. Thus `calc <=` over rational datatypes is an intrinsic type error,
even though `a <= b` outside a connector can call `le`. This distinction is
retained because calc is an explicit proof construct with fixed composition
rules. The alternative of silently importing laws is unsound; banning all
intrinsic calc on types with operators would unnecessarily remove existing
proofs. Document/hover the connector's intrinsic meaning.

Users may invoke `le`'s transitivity lemma, or calculate over Boolean
propositions containing calls. Law-aware calculation syntax would need explicit
verified evidence of composition and call-domain obligations; it is a separate
feature. Library laws become usable through their ordinary proved contracts and
explicit lemma calls; no law is activated by an operator attribute.

### 6.4 Can issue #193 be a user-operator library?

**Yes for notation and reference semantics; not for its full native-execution
promise by adding attributes alone.** The comparison must distinguish three
routes:

1. **Ordinary executable library.** Extend the user-symbol set to `& | ^ ! << >>`,
   supply named members over executable integer references or verified
   function-by-method implementations, and compile the resulting ordinary calls.
   This adds no bitwise axiom, but may execute the recursive/iterative reference
   algorithm rather than a native integer instruction.
2. **Library API with canonical native specialization.** Verify those ordinary
   calls, but compile the exact bundled canonical implementation identity to
   native instructions for the known semantic width/carrier. This can satisfy
   #193, with no BV conversion. It is compiler work: provenance, width
   specialization, backend correspondence and generated-code checks remain.
   A public attribute, method name, or arbitrary user implementation must not
   authorize the specialization.
3. **#193's intrinsic bounded-newtype surface.** A revealed integer newtype with
   validated semantic-width metadata acquires the operations directly. The
   verifier uses the integer-reference meaning; the compiler uses a width-bearing
   intrinsic descriptor. It needs no per-newtype operator facade and no #194
   dependency.

Recommend **route 3 for the staged implementation**, keeping named reference
functions and proved library lemmas. Route 2 is a plausible later implementation
alternative, not a reason to wait for the general operator feature. This respects
#193's accepted architecture and default-off `--integer-bitwise` option.

The #193 reference definitions at PR #247's audited revision are **ghost**. A
normal executable `band` function cannot merely call ghost `andNat`.
`Resolver/ExpressionTester.cs` checks ghost calls, while
`Resolver/ModuleResolver.cs` handles the actual by-method exception. An
executable definition, verified by-method, or reviewed compiler implementation
is required. `SinglePassCodeGenerator.Expression.cs` and
`SinglePassCodeGenerator.cs::CompileFunctionCallExpr` compile an ordinary
`FunctionCallExpr` as its ordinary call; an operator attribute is not a native
instruction selector.

| Dimension | Ordinary library operators | Canonical library specialization | #193 intrinsics |
|---|---|---|---|
| Verification | Ordinary calls/contracts over integer references | Same call semantics | Integer-reference semantics after intrinsic lowering |
| Proof cost | Same mathematical problem; wrappers may add unfolding/contract work | Same frontend cost as library route | Can avoid a facade layer; no automatic algebraic advantage from infix notation |
| Native execution | Not guaranteed; reference loop/recursion may remain | Possible with explicit compiler mapping and exact identity | Explicit backend mapping is part of the feature |
| Conversion through bitvectors | Unnecessary | Unnecessary | Unnecessary |
| Declaration burden | Six direct member/attribute declarations per owner without additional inheritance/generation support, plus contracts | Same facade burden unless another mechanism generates it | One semantic-width annotation plus the existing type constraint |
| Count and result diagnostics | Ordinary member precondition or explicit-cast errors | Same, plus specialization/target checks | Dedicated width/count/result/target diagnostics at the intrinsic use |
| New trusted surface | General call notation/compiler only, but lacks the native guarantee | Canonical identity, width/carrier specialization, each native implementation | Capability recognition, intrinsic-to-reference lowering, canonical model identity, each native implementation |
| Dependency | General user bitwise symbol support must exist first | Both that support and native specialization | Independent #193 work |

There is no measured performance winner between these unimplemented frontends.
For identical normalized calls/contracts, the logical problem is the same modulo
source metadata; wrappers can add ordinary cost. PR #247's published
[reference-wrapper measurements](https://github.com/erniecohen/dafny/blob/a61b06c71d742ff19b280592d26d1a0e2ce9c87c/docs/dev/design/193-evidence/public-measurements.md)
proved 68/68 reference-wrapper contracts at 2,994–7,511 RU on Z3 5.1.0 and
3,002–7,519 on 4.12.1 in that corpus. They do not measure an implemented
library-versus-intrinsic frontend or general algebraic automation. The separate
3,744–15,735-RU composed trusted-bridge results are client-cost projections, not
proofs of bridge soundness or an advantage for either spelling. This review
performs no new performance measurement and does not discharge #193's still
required effective vacuity controls.

**Whole-type closure also differs.** For `N = n:nat | 0 < n < 256`, `1 & 2` is
zero. A library function returning `N` needs an appropriate precondition, a wider
return type plus explicit cast, or a proof that will fail at its definition. The
intrinsic design checks the complete original predicate at each use, not just the
width range. Similarly a named composite helper can compute `a & !b` in its
integer model and check only its final result; the written two-intrinsic
expression checks the intermediate result too. Neither route gets arbitrary
newtype closure from a width annotation.

**Native trust is not removed by moving syntax into a library.** Signedness,
carrier masks, full-width shifts, shifts by zero, sub-carrier semantic widths,
wide JavaScript values and target limitations remain. A canonical specialization
must show the same generated result as the integer reference. It must also avoid
unnecessary arbitrary-integer boxing on fixed native carriers; relying on an
unmeasured host optimizer is not the promised generated-code behavior. Do not
widen every existing `IsBitVectorType`/`BooleanBits` predicate to include integer
newtypes: the pin's BV translation assumes a real BV width.

### 6.5 Cross-feature selection and exact document changes

Let U be `--user-defined-operators` and I be `--integer-bitwise`. The first #194
milestone does not add user bitwise symbols:

| U | I | Supported ordinary user arithmetic | Width-qualified revealed integer-newtype `a & b` | Existing `bvN` bitwise / Boolean `!p` |
|---:|---:|---|---|---|
| off | off | Existing behavior, attributes inert | Existing rejection | Existing intrinsics |
| on | off | #194 fallback selection | Existing rejection | Existing intrinsics |
| off | on | Existing behavior, attributes inert | #193 intrinsic | Existing intrinsics |
| on | on | #194 fallback selection | Same #193 intrinsic | Existing intrinsics |

Static intrinsic applicability depends on the operator, operand types, visible
capability/semantic width and recorded language options. It does not depend on
proving a particular count/result obligation. An eligible intrinsic with an
unprovable count or result predicate fails; it never retries a user member. An
invalid enabled width declaration is a declaration error. An opaque export does
not expose a capability merely because a source attribute was copied.

If a later separately approved stage adds bitwise user slots, existing bool/BV
and enabled eligible #193 operations are in the same intrinsic-first category;
only an inapplicable intrinsic allows fallback. This proposal does not include
bitwise override. A frozen member choice remains that call even if an imported
view/client mode would now admit an intrinsic, and a frozen intrinsic remains
intrinsic. #194's exact artifact-mode checks and #193's per-source capability
provenance must each hold; one option never enables the other. M1's library-input ban concerns user-supplied
trusted/precompiled inputs, not compiler-owned canonical model support injected
by an independently enabled #193. Only #193's authenticated support provenance
may distinguish that case; a file name or public attribute cannot bypass the ban.

`--additional-axioms` is independent of U and I. #193 bridge collection must use
resolved native-BV operation identity and genuine integer observations, never
source tokens or names/attributes on arbitrary user functions. User operators
receive no bridge facts by virtue of their notation.

**Changes to this document are normative:** sections 1/2 exclude user bitwise
symbols from the first PR; 5.2 defines static applicability; 5.6 accommodates
intrinsic family/width identity; 8/9 distinguish ordinary user calls from #193
intrinsic descriptors; 12 requires the four-flag tests. The comparison above
replaces any suggestion that an operator library alone supplies native code.

**Required companion changes to `193-bitwise-integers.md`** (specified here for
its coordinator; this commit deliberately changes only the #194 document):

| Section in PR #247 at `a61b06c7` | Exact replacement/addition required |
|---|---|
| 6.3, issue-194 interaction paragraph | State that #193 is independent of #194. Use the four-row U/I table above. Define static eligibility and no fallback on count/result failure; preserve frozen choices across views/reload. User bitwise override is not part of either first milestone. |
| 6.4, reference packaging | Retain the ghost-reference/executable-operation distinction. State that a library replacement requires an executable or by-method implementation, or exact canonical native specialization; arbitrary member names/attributes confer no privilege. Retain identity/provenance checks for either source or prelude model machinery chosen in #193's own revision. |
| 7, compilation | Keep direct integer code with no BV intermediate. If route 2 is substituted later, require generated-code parity: no reference recursion, materialized bit sequence, or avoidable arbitrary-integer conversion on a native carrier; retain masks, endpoint counts and all target limitations. |
| 8, code inventory | Do not depend on #194 parser/operator support. A shared intrinsic-applicability/semantic-width descriptor is allowed, but globally widening BV predicates is not. Preserve the I-off path. |
| 10, `Operator API` alternatives | Replace the two-way shorthand with the three routes in 6.4; recommend the intrinsic surface now and retain named library APIs. |
| 11–12, tests/milestones | Add all four U/I combinations, opaque capability tests, failed count/result controls and a user function with a misleading native name that receives neither native privileges nor bridge facts. #194 completion discharges none of #193's model/vacuity/backend gates. |

Useful exact-pin paths are `Resolver/NativeTypeAnalysis.cs`,
`AST/TypeDeclarations/NewtypeDecl.cs`,
`Resolver/PreType/PreTypeResolve.Expressions.cs`,
`Resolver/PreType/PreTypeConstraints.cs`,
`Verifier/Expressions/TranslateBinaryExpr.cs`, and the backend bitwise switches.
They separate semantic eligibility, storage carrier, BV translation and ordinary
call compilation; the implementation should preserve those distinctions.

## 7. Compound assignment (later milestone)

After its dedicated milestone, the accepted forms are `lhs += e;`, `-=`, `*=`, `/=`, and `%=`. They have no
separately declarable operators. Only one ordinary assignable location is allowed:
a local/out variable, a writable field, or an array element, including supported
multidimensional array elements. Slices, immutable locations, patterns, multiple
LHSs, and such-that/failure assignments are excluded.

For a local, `x *= y` has the checked meaning of `x := x * y`. For a general
location, the expansion must capture the location and old value exactly once.
For example, the conceptual expansion of `a[i] *= y` is:

```dafny
var arr := a;
var index := i;
var left := arr[index];
var right := y;
var result := left.times(right);  // or selected static/method/intrinsic operation
arr[index] := result;
```

Generated names are fresh and hygienic. Resolve the symbol against the static
type of `left`. First capture the receiver and indices left-to-right, check/read
the old location, evaluate the RHS, invoke the operation, and assign the result
to the captured location. A method may mutate the original field, array, or
objects used in the LHS expression; it does not cause those expressions or the
old value to be read again. No implicit newtype conversion is inserted for the
final store. Existing subset, null, bounds, initialization, ghost, and modifies
obligations remain. Capturing a field/array location is not permission to write it.

Use a source statement wrapper, `CompoundAssignStmt`, with ordinary resolved
statements. Reuse the fresh-local and statement-desugaring pattern in
`AST/Statements/Assignment/AssignOrReturnStmt.cs::DesugarElephantStatement`, not a
backend-specific compound operation. The ordinary method-RHS path supplies its
`CallStmt`. Do not add a verifier rule for compound assignment.

## 8. AST and parser architecture

### 8.1 Source wrapper, ordinary semantic AST

Add `OperatorSyntaxExpr : ConcreteSyntaxExpression`, containing:

- symbol, effective arity, original operand expressions, and operator token/range;
- source form (`infix`, `unary`, or `builtin` escape);
- lookup mode (`ordinary` or `intrinsic-only`);
- resolved binding metadata for diagnostics/navigation;
- the immutable selection and checked clone-context correspondence specified in
  5.6, independent of whether ordinary resolved fields are copied;
- an ordinary `ResolvedExpression`, or the existing method-RHS information while
  resolving a permitted statement context.

`AST/Expressions/ConcreteSyntaxExpression.cs` and `Expression.Resolved` already
support this distinction. After successful expression resolution, the wrapper
contains an intrinsic expression or `FunctionCallExpr`. Non-overloadable equality
uses the existing intrinsic nodes directly. A statement-only method use instead becomes ordinary resolved
statements containing a `CallStmt`. There is no unresolved operator at a semantic
analysis/translation boundary.

Pre-resolution visitors see original operands; semantic visitors see the resolved
call exactly once. Preserve all receiver/argument evaluations, type substitutions,
heap labels, ghost information, and source origins when cloning, substituting,
refining, serializing, or exporting. A selected static binary operator expands
to `Owner.f(a, b)` with a type-only static receiver (unary uses `Owner.f(a)`). Do not also put `a` in a
`StaticReceiverExpr.ContainerExpression`: it is already an argument, and doing
so would duplicate its evaluation/checks. Where a genuine expression receiver
exists in an ordinary named call within the source operands, do not drop its
well-formedness checks merely because that member is static; the receiver-loss
bug documented for #39 in `REVIEW.md` is the regression precedent.

Source printers retain source notation. Resolved/debug printers can show the
ordinary expansion. The source wrapper must not add new trigger terms, call
permissions, axioms, or intermediate Boogie expressions merely to represent an
infix use. Do not change primitive `BinaryExpr.ResolvedOpcode` meanings.

**Alternatives:** replacing the source immediately with a call gives a small
downstream implementation but loses notation before the callee is even known,
unless equivalent provenance is added elsewhere. Annotating `BinaryExpr` with
an optional callee preserves precedence machinery but requires every semantic
consumer to remember a special exception. Bounds discovery, coinduction, constant
folding, compilation, and termination already use primitive opcodes. The wrapper
wins because it makes the semantic invariant structural rather than conventional.
This applies to arbitrary user operators. #193's compiler-owned width-bearing
intrinsic is a different AST category; a user attribute never manufactures it.

### 8.2 Disabled parsing must remain byte-identical, including errors

M1 changes only guarded construction actions for already parsed expressions.
The following escape/compound hooks belong to later milestones and are not part
of M1. No milestone may relax the disabled parser/error identity requirement.


The attribute declaration needs no grammar change. The escape and compound forms
can also avoid new scanner terminals. This matters for invalid old inputs as well
as valid programs: adding a token or Coco alternative can change error numbers,
FIRST/FOLLOW/recovery sets, and disabled diagnostics even if guarded by `IF(option)`.

Describe the accepted extension as:

```ebnf
BuiltinOperatorCall = contextual_identifier("builtin") "." Operator
                      "(" Expression { "," Expression } ")" ;
CompoundAssignment = Lhs AdjacentArithmeticOperatorAndEquals Expression ";" ;
AdjacentArithmeticOperatorAndEquals = "+" "=" | "-" "=" | "*" "="
                                     | "/" "=" | "%" "=" ;
```

These are explanatory grammar fragments, **not instructions to add these literal
productions to Coco**. Implement guarded semantic-action hooks in existing
productions, with helpers in `AST/Grammar/ParserNonGeneratedPart.cs`:

1. Immediately after `NameSegment` recognizes its identifier, detect the exact
   enabled `builtin . operator (` suffix before normal name/generic/hash-call
   processing. Consume it using existing token/argument parsing routines and
   return a forced-intrinsic wrapper. Preserve ordinary expression-context
   restrictions, suffix processing, origins, and error recovery.
2. Immediately after the first `Lhs` of `AssignStatement`, detect an enabled
   adjacent arithmetic-token/`=` pair. Parse the single expression and semicolon
   and return a compound statement before entering the unchanged alternatives.
3. At existing expression-construction actions, choose source wrappers only when
   enabled. With the option off, execute the same constructors and actions as
   before, including existing chain and unary handling.

The disabled guards do no peeking, name allocation, attribute processing, or
resolver initialization. Do not reserve `builtin` or `operator`, and do not add
`+=`, `*=`, or `===` scanner tokens. Check adjacency by token positions, as
existing shift and disjoint-operator parsing already does; retain the original
tokens rather than mutating their text. These hooks introduce no new LL(1)
alternatives: the new suffixes were invalid at those points, and enabled lookahead
selects them explicitly. Nesting and malformed forms need dedicated parser tests.

`DafnyCore.csproj::RunCoco` generates `Source/DafnyCore/Parser.cs` and `Scanner.cs`;
they are not tracked source files at this commit. Regenerate them; do not edit
them manually. Compare token constants, scanner tables, grammar sets, and error
mappings with baseline. If semantic hooks cannot keep those unchanged, use an
isolated opt-in parser path instead of relaxing the disabled-mode requirement.

### 8.3 IDE, formatter, and rename

Hover on an operator should show its selected member and signature, or say which
intrinsic was selected. Go-to-definition and find-references should connect an
operator token to its callable. Source formatting keeps the existing infix
precedence/spacing and handles the compound/escape forms without exposing the
desugaring.

There is a concrete rename hazard: `AST/IHasReferences.cs` currently stores only
the reference range and referred declaration, and
`DafnyLanguageServer/Handlers/DafnyRenameHandler.cs` replaces every reference range
with the new identifier. If `*` simply becomes an ordinary reference to `times`,
renaming `times` would corrupt every `*` use.

Add reference-kind/renameability metadata, defaulting to normal textual-name
behavior for existing references. Symbolic operator references participate in
navigation and find-references, but not textual replacement. Rename edits the
member declaration and explicit named calls, leaving symbols unchanged. A rename
initiated at a symbol may target its backing declaration using that same policy.
Coordinate `IHasReferences`, `Workspace/SymbolTable`, definition/hover/reference
handlers, and rename handling. Avoid duplicate references from wrapper and
generated call, and do not hide the actual source operator origin as autogenerated.

## 9. Verification, compilation, and a relative soundness argument

For every accepted enabled program, erase source wrappers and expand compounds. The resulting program uses existing primitive operators and
ordinary calls. For functions, the call has the same selected member, receiver,
arguments, instantiated types, visibility, ghostness, heap context, and source
well-formedness obligations as the explicitly written call. For methods, the
expansion has only currently permitted method statements. For compounds, the
explicit temporaries fix the storage location and evaluation order.

The verifier must see this expansion before call-graph construction, bounds
discovery, ghost checking, co-call/extreme-predicate analysis, decreases inference,
trigger collection, and translation. In particular:

- `BoundsDiscovery.cs` must see named equivalence predicates as ordinary calls,
  never as `EqCommon`; it uses intrinsic equality to construct exact bounded pools.
- Codatatype equality consumers must never mistake a user relation for
  coinductive equality.
- `CallGraphBuilder`, `ModularTerminationRewriter`, and ordinary function/method
  well-formedness checks must see every selected call.
- Opaque/reveal/fuel behavior must be identical to the corresponding named call.
- Generated intrinsic equalities and `calc` relations must never return to
  overload lookup.

Subject to the soundness of the existing call/type-view machinery, a proof of the
expanded program proves the same source proposition. The feature adds notation
and checked expansions, not a new equality, order, algebra, or termination axiom.
All source equality remains intrinsic. #193's separate, opt-in reference/bridge
work is not licensed by an operator attribute and is not trusted reasoning added
by this proposal.
Proof traits carry ordinary contracts and lemma bodies: implementations verify
their obligations, and clients obtain laws by ordinary lemma calls. The language
does not auto-call every law or treat an interface name as evidence.

This is a **relative** argument, not a claim to repair every existing Dafny
soundness defect. At the audited commit, `--modular-termination` is experimental
and explicitly says that function-value calls remain unchecked. #211 is in
progress. #194 must neither introduce `{:termination false}` nor
`@AssumeCrossModuleTermination` to get an example accepted, enable another option
silently, nor bypass an ordinary trait-call rejection. The same source call and
operator call must get the same termination result under each supported policy.
Keep the primary example in one module; cross-module tests cover the current
#211 contract and are updated as that separate work lands.

Compilation needs no operator-specific runtime ABI, operator methods in each
target language, or changes to built-in equality/hash generation. Existing
`FunctionCallExpr`, `CallStmt`, and by-method compilation do the work. Check each
supported backend that already supports the named-call feature combination;
record pre-existing unsupported combinations rather than promising to implement
general-newtype trait compilation here.

For simple expression uses, normalized semantic AST and Boogie should agree
with the named-call form, apart from source-location/reporting metadata. This is
the concrete way to pursue the requested same-proof-cost acceptance criterion.
Do not assert exact resource equality before measuring it: names, ordering, and
triggers must also be compared. Compounds are compared with their explicit
capture-and-call expansion. When disabled, the stronger requirement is literal
byte-identical diagnostics/Boogie and unchanged verdicts/resource counts under
the fork's deterministic comparison procedure.

## 10. Worked examples, notation counts, and library laws

The following three blocks form one existing-syntax program when concatenated.
Append the `Main` below to reproduce the positive run. The commented infix forms
are proposed source, not implemented syntax; the declaration changes are shown
explicitly. The separate Ordered/Rat program in 10.5 is not concatenated with them.

All three share their function bodies, representation constraints and proof obligations between the columns. The operator version adds only the shown declaration attributes and changes the client assertion. It does not make rational equivalence structural equality, and the matrix/modular `==` remains intrinsic.

### 10.1 Rationals

```dafny
type Pos = x: int | 0 < x witness 1

datatype Rat = Rat(n: int, d: Pos) {
  function plus(b: Rat): Rat { Rat(n * b.d + b.n * d, d * b.d) }
  function times(b: Rat): Rat { Rat(n * b.n, d * b.d) }
  predicate le(b: Rat) { n * b.d <= b.n * d }
  predicate equiv(b: Rat) { n * b.d == b.n * d }
}

method rationals() {
  var a := Rat(1, 2);
  var b := Rat(2, 3);
  var c := Rat(1, 6);
  var d := Rat(1, 2);
  assert a.times(b).plus(c).le(d);  // today
  // assert a * b + c <= d;        // proposed, M1
  assert Rat(1, 2).equiv(Rat(2, 4));
  assert Rat(1, 2) != Rat(2, 4);    // structural equality remains distinct
}
```

Operator declarations add `{:operator "+"}` to `plus`, `{:operator "*"}` to `times`, and `{:operator "<="}` to `le`; no attribute on `equiv`. No `"override"` is necessary for `<=` on `Rat`. The single positive-denominator subset condition and witness are unchanged. No new requires/ensures/reads/decreases/lemma annotation is needed for notation.

### 10.2 2x2 integer matrices

```dafny
datatype Mat = Mat(a: int, b: int, c: int, d: int) {
  function plus(m: Mat): Mat {
    Mat(a + m.a, b + m.b, c + m.c, d + m.d)
  }
  function times(m: Mat): Mat {
    Mat(a * m.a + b * m.c, a * m.b + b * m.d,
        c * m.a + d * m.c, c * m.b + d * m.d)
  }
}

method matrices() {
  var a := Mat(1, 2, 3, 4);
  var b := Mat(0, 1, 1, 0);
  var c := Mat(1, 0, 0, 1);
  assert a.times(b).plus(c) == Mat(3, 1, 4, 4); // today
  // assert a * b + c == Mat(3, 1, 4, 4);      // proposed, M1
}
```

Add `{:operator "+"}` and `{:operator "*"}` to the two declarations. Equality of matrices is structural equality of their four integers; no bespoke equivalence or proof annotations are needed.

### 10.3 Modular-arithmetic numeric newtype

```dafny
newtype Mod17 = x: int | 0 <= x < 17 witness 0 {
  function plus(b: Mod17): Mod17 {
    (((this as int) + (b as int)) % 17) as Mod17
  }
  function times(b: Mod17): Mod17 {
    (((this as int) * (b as int)) % 17) as Mod17
  }
}

method modular() {
  var a := 5 as Mod17;
  var b := 7 as Mod17;
  var c := 14 as Mod17;
  assert a.times(b).plus(c) == (15 as Mod17); // today
  // assert a * b + c == (15 as Mod17);      // proposed only after arithmetic override
}
```

The operator version requires `{:operator "+", "override"}` and `{:operator "*", "override"}`. This is intentionally outside M1. Ordinary `Mod17` arithmetic is constrained integer arithmetic, not modular reduction: `5 * 7` cannot become 1 merely because a fallback `times` member exists. No-override M1 must reject the attributes; it must not claim that this program has modular infix behavior. The body casts to `int` make its intrinsic arithmetic explicit; `% 17` proves the cast back is in range. The bound/witness/casts and proof obligations are identical in the named and future operator forms. A wrapper datatype is an alternative available in M1, but that changes the representation, so do not present it as the same newtype example.

### 10.4 Reproducible counts and checks

Count Unicode code points in exactly the single client assertion as printed below, including internal spaces and semicolon, excluding indentation, newline, trailing comments and Markdown. ASCII-only examples make this equal to UTF-8 byte counts. Also report whitespace-free counts to show the result is not an artifact of formatting.

| Type | Today's exact assertion | Operator assertion | Chars today → operator | Whitespace-free | Reduction |
|---|---|---|---:|---:|---:|
| Rat | `assert a.times(b).plus(c).le(d);` | `assert a * b + c <= d;` | 32 → 22 | 31 → 15 | 10 (31.25%) |
| Mat | `assert a.times(b).plus(c) == Mat(3, 1, 4, 4);` | `assert a * b + c == Mat(3, 1, 4, 4);` | 45 → 36 | 39 → 26 | 9 (20.0%) |
| Mod17 | `assert a.times(b).plus(c) == (15 as Mod17);` | `assert a * b + c == (15 as Mod17);` | 43 → 34 | 38 → 25 | 9 (20.93%) |

| Type | Added declaration annotations | Added characters including one following space each | Added use annotations | Added proof/representation annotations |
|---|---:|---:|---:|---:|
| Rat | 3 | 49 | 0 | 0 |
| Mat | 2 | 32 | 0 | 0 |
| Mod17 | 2 | 56 | 0 | 0 |

For a whole snippet with just one such use, operator metadata increases source size by 39/23/47 characters respectively; do not hide that cost. Reusing the same pattern 5/4/7 times amortizes the metadata. This count measures source notation, not comprehension or proof performance. A stronger quantitative usability claim requires a real library and client corpus.

```python
pairs = [
  ('assert a.times(b).plus(c).le(d);', 'assert a * b + c <= d;'),
  ('assert a.times(b).plus(c) == Mat(3, 1, 4, 4);',
   'assert a * b + c == Mat(3, 1, 4, 4);'),
  ('assert a.times(b).plus(c) == (15 as Mod17);',
   'assert a * b + c == (15 as Mod17);'),
]
for named, operators in pairs:
  print(len(named), len(operators),
        len(''.join(named.split())), len(''.join(operators.split())))
```

**Validation performed for the three explicit-call examples:**

The complete positive driver is:

```dafny
method Main() {
  rationals();
  matrices();
  modular();
  print "ok\n";
}
```

Save the three code blocks and this driver as `194-usability-explicit.dfy`, then
use the public release's `dafny` with an explicit installed Z3 5.1.0 path:

```sh
dafny verify 194-usability-explicit.dfy --type-system-refresh --solver-path="$DAFNY_194_Z3"
dafny run 194-usability-explicit.dfy --type-system-refresh --target py --no-verify
```

For the negative version, replace only the final print with `assert false;` and
verify again; an assertion error is required, not a parser/type failure.

Using public release `4.11.0+fcb2042d.review.bf47de16`, Z3 5.1.0, `--type-system-refresh`, additional axioms OFF:

- The complete explicit-call program verifies: **10 verified, 0 errors**.
- Python compilation/execution (`run --target py --no-verify` after verification) prints **`ok`**.
- The same complete file with `Main`'s final print replaced by `assert false` reports **9 verified, 1 error**, at that deliberate assertion.
- The initial attempt without an explicit solver path failed with `Z3 is not found`; no proof claim comes from that attempt.

This is an existing-call baseline, not a test of proposed syntax or an exact build of source pin 428e348. All proposed `{:operator}` meanings/infix examples are unchecked because the feature is not implemented. The separate original Ordered/Rat explicit-call example and its earlier 25-verified result remain distinct evidence, below.

The declaration changes, side by side, are equally small; function bodies and
all representation/contract annotations remain identical:

| Program | Today's member head | Proposed member head |
|---|---|---|
| Rat | `function plus(b: Rat): Rat` | `function {:operator "+"} plus(b: Rat): Rat` |
| Rat | `function times(b: Rat): Rat` | `function {:operator "*"} times(b: Rat): Rat` |
| Rat | `predicate le(b: Rat)` | `predicate {:operator "<="} le(b: Rat)` |
| Mat | `function plus(m: Mat): Mat` | `function {:operator "+"} plus(m: Mat): Mat` |
| Mat | `function times(m: Mat): Mat` | `function {:operator "*"} times(m: Mat): Mat` |
| Mod17, later only | `function plus(b: Mod17): Mod17` | `function {:operator "+", "override"} plus(b: Mod17): Mod17` |
| Mod17, later only | `function times(b: Mod17): Mod17` | `function {:operator "*", "override"} times(b: Mod17): Mod17` |

The counted representation annotations are one positive-denominator constraint
and witness for Rat, none for Mat, and one range constraint and witness for
Mod17. All remain in both versions. Casts in Mod17's bodies remain unchanged.
There are no added law lemmas or requires/ensures/reads/decreases annotations for
these notation changes. Declaration metadata is a real up-front cost, so these
small examples support a modest repeated-use notation benefit, not a quantitative
claim about human comprehension or proof speed.

### 10.5 Ordered trait and rational implementation (later trait milestone)

This separate complete proposed program retains the original checked library
pattern while replacing equality override with a named `eq`. The only trait
operator is fallback `<=`; its implementation is inherited through the ordinary
instance slot. Laws use explicit named calls, so their statement does not depend
on reader assumptions about overloaded equality.

`self` is an ordinary ghost function with an intrinsic identity contract, not a
built-in self type. Noncanonical positive-denominator rationals give an order
that is antisymmetric **modulo `eq`**, not modulo representation equality. The
congruence lemmas make it a partial order on equivalence classes; they do not make
numerator/denominator observations congruent or change built-in sets/maps. A
canonical representation would be needed for structural antisymmetry.

This program requires the later trait milestone and ordinary
`--general-traits=datatype`; direct arithmetic in 10.1 requires neither the
trait stage nor legacy-resolver parity.

```dafny
type Pos = x: int | 0 < x witness 1

trait Ordered<T extends Ordered<T>> {
  ghost function self(): T
    ensures this == (self() as Ordered<T>)
  predicate eq(other: T)
  predicate {:operator "<="} le(other: T)
  lemma eq_reflexive()
    ensures eq(self())
  lemma eq_symmetric(other: T)
    requires eq(other)
    ensures (other as Ordered<T>).eq(self())
  lemma eq_transitive(other: T, last: T)
    requires eq(other) && (other as Ordered<T>).eq(last)
    ensures eq(last)
  lemma le_congruent(other: T, last: T)
    requires (other as Ordered<T>).eq(last)
    ensures le(other) == le(last)
  lemma le_congruent_left(other: T, last: T)
    requires eq(other)
    ensures le(last) == (other as Ordered<T>).le(last)
  lemma reflexive()
    ensures le(self())
  lemma antisymmetric(other: T)
    requires le(other) && (other as Ordered<T>).le(self())
    ensures eq(other)
  lemma transitive(other: T, last: T)
    requires le(other) && (other as Ordered<T>).le(last)
    ensures le(last)
}

datatype Rat extends Ordered<Rat> = Rat(n: int, d: Pos) {
  ghost function self(): Rat
    ensures this == self() as Ordered<Rat>
  { this }
  predicate eq(other: Rat) { n * other.d == other.n * d }
  predicate le(other: Rat) { n * other.d <= other.n * d }
  function {:operator "*"} times(other: Rat): Rat { Rat(n * other.n, d * other.d) }
  function {:operator "+"} plus(other: Rat): Rat { Rat(n * other.d + other.n * d, d * other.d) }
  lemma eq_reflexive()
    ensures eq(this)
  { }
  lemma eq_symmetric(other: Rat)
    requires eq(other)
    ensures other.eq(this)
  { }
  lemma eq_transitive(other: Rat, last: Rat)
    requires eq(other) && other.eq(last)
    ensures eq(last)
  { }
  lemma le_congruent(other: Rat, last: Rat)
    requires other.eq(last)
    ensures le(other) == le(last)
  { }
  lemma le_congruent_left(other: Rat, last: Rat)
    requires eq(other)
    ensures le(last) == other.le(last)
  { }
  lemma reflexive()
    ensures le(this)
  { }
  lemma antisymmetric(other: Rat)
    requires le(other) && other.le(this)
    ensures eq(other)
  { }
  lemma transitive(other: Rat, last: Rat)
    requires le(other) && other.le(last)
    ensures le(last)
  { }
}

function test(a: Rat, b: Rat, c: Rat, d: Rat): bool {
  a * b + c <= d
}

method Main() {
  var a := Rat(1, 2);
  var b := Rat(2, 3);
  var c := Rat(1, 6);
  var d := Rat(1, 2);
  print a * b + c <= d, " ", a.eq(Rat(2, 4)), " ", a == Rat(2, 4), "\n";
}
```

Intended output remains `true true false`: the first Boolean is the arithmetic
comparison, the second named mathematical equality, and the third intrinsic
representation equality. The last two meanings now differ visibly in the source.
No `==` override and no non-equality priority override are needed.

The existing PR files
[194-ordered-rat-explicit.dfy](examples/194-ordered-rat-explicit.dfy) and
[194-ordered-rat-vacuity.dfy](examples/194-ordered-rat-vacuity.dfy) remain unchanged.
Their historical validation used public release
[`v4.11.0+fcb2042d.review.9a4451d7`](https://github.com/erniecohen/dafny/releases/tag/v4.11.0%2Bfcb2042d.review.9a4451d7),
Z3 5.1.0, refreshed resolver and `--general-traits=datatype`, additional axioms
off: **25 verified, 0 errors**, Python output `true true false`, and one expected
vacuity error in the negative client. Its legacy invocation reported nine
trait-view/comparability errors. These are the original explicit-call checks,
not new measurements of the rewritten trait syntax or the pin used for this audit.
The first PR does not promise to remove those legacy errors.

A generic client may eventually write `a <= b` with `T extends Ordered<T>`,
expanding to `(a as Ordered<T>).le(b)`. Until bound-aware lookup is implemented,
write that explicit cast/call. No stage should accept an unsupported bound and
pretend that this cast happens automatically.

## 11. Concrete implementation map

### 11.1 Option and staged artifact compatibility

Add `CommonOptionBag.UserDefinedOperators` as `Option<bool>`, default false, and
expose it through `DafnyCommands.ParserOptions` for parse/verify/compile/format/IDE
and project settings. Do not add it to the module `{:options}` allowlist. Preserve
command-line-over-project precedence, including explicit false:

```toml
[options]
user-defined-operators = true
```

First-PR help must describe the actual implemented subset:

> Enable fallback arithmetic and comparison function operators on concrete
> classes and inductive datatypes. Requires --type-system-refresh. Equality and
> module-attribute operators remain intrinsic. Override mode, compounds, trait
> operators and library/precompiled input reuse are not yet supported. Disabled
> by default.

**M1 is source-only.** Reject feature-on `.doo` production/consumption, user-supplied
trusted `--library` inputs, standard-library compiled reuse, and precompiled
module reuse, including equivalent programmatic paths. Fully checked source,
concrete imports and the compiler's own independently authenticated support
remain allowed. An early shared input gate covers verification-only invocations;
a backend-only check would be too late for those.

Suppress automatic `.dtr` output when enabled; reject explicit record input or
output requests before code emission. `ExecutableBackend.ProcessTranslationRecords`
must still initialize `Compilation.AlreadyTranslatedRecord` with
`TranslationRecord.Empty(program)`, because outer-module, Python and Go paths use
it. Then reject nonempty `TranslationRecords` or non-null `TranslationRecordOutput`
and skip record read/write. Both `ExecutableBackend.Compile` and
`DafnyExecutableBackend.Compile` must stop on the rejection, rather than continue
after a helper returns. Their own output record is never read back by the same
compilation, so normal complete-source compile/run needs no reusable record.
Keep the whole OFF path unchanged, including malformed-record errors and
serialization. No new artifact schema or OFF validation rule belongs to M1.

**The later artifact milestone** replaces those restrictions with this protocol:

1. Initially require matching producer/consumer operator modes for `.doo` and
   recorded precompiled modules; absent metadata means false. A client cannot
   reinterpret formerly inert attributes or source operators merely by enabling
   its own flag. Do not assume a `Cli` registry entry enforces compatibility.
2. `.doo` stores original source/pre-resolution form. Validate mode in its
   manifest before reparsing. Original module/refinement source can reconstruct
   saved choices in dependency order; flattened inherited source needs stable
   replay data or explicit named/intrinsic spellings (5.6).
3. `.dtr` needs a **record-level** true marker as well as appropriate module
   coverage: `TranslationRecord` omits the default module and empty modules from
   `OptionsByModule`. A top-level-only feature program otherwise has no marker.
   Omit the field on OFF serialization and test actual TOML bytes. Missing means
   false; a malformed/nonboolean value is an error. Validate supplied true mode
   against an OFF consumer before merge/reuse; current `Validate` does not check
   arbitrary option equality. Reconcile this new-schema rejection with the
   old-format OFF compatibility requirement before that milestone ships.
4. Require complete interpretation coverage for every nonempty reused compiled
   module on the enabled path. `ProcessTranslationRecords` runs during
   compilation, after source resolution; do not claim that it protects earlier
   parsing or verification-only trusted inputs. A `Validate` check alone cannot
   protect modules absent from all records. For omitted default-module cases,
   supply an unambiguous identity or reject enabled reuse.
5. Supplied records must protect both mismatch directions. Existing raw trusted
   source or foreign code without any record retains its caller responsibility;
   strict OFF compatibility does not authorize a new blanket missing-record
   error on old invocations. Feature libraries must distribute their required
   manifest/record. Removing all metadata is outside the checked reuse route.
6. Initially, prebuilt libraries (including Std) need the same mode. A future
   per-module interpretation scheme could relax this, but must preserve callable
   metadata and selection provenance. Never treat #193's different capability
   protocol as permission to skip #194's checks.

Tests cover mode mismatches, missing/malformed metadata, default/named modules,
hidden members, complete reused-module coverage, original-source round trips,
and flattened-source rejection. Compare interpretation/source payloads while
accounting separately for mandatory product-version and incidental ZIP metadata;
this does not relax scanner, diagnostics or literal Boogie checks.

Keep `{:operator}` available to source/serialization printers. Enabled validation
consumes it as front-end metadata, never an SMT instruction; inspect
`BoogieGenerator.ExpressionTranslator.cs::TrAttributes` before suppressing any
emission. Compare operator/named-call programs with identical attributes. OFF
unknown attributes must follow their old path, including printed output.

### 11.2 Implementation map beyond the first PR

Section 12.2 is the per-file M1 map and estimate; 5.6 names every clone route.
The remaining complete-design changes are these staged extensions, not extra
work hidden in M1:

| File/location | Later change |
|---|---|
| `Dafny.atg`; `AST/Grammar/ParserNonGeneratedPart.cs` | Contextual escape/compound hooks, using existing tokens; no scanner changes; remove the module-attribute restriction only with shared legacy support |
| `AST/Expressions/LiteralExpr.cs::NegationExpression`; source wrapper/chain classes | Keep ordinary unary-literal behavior and source selections when adding new owners/priorities |
| Proposed `AST/Statements/Assignment/CompoundAssignStmt.cs` | Source statement plus ordinary capture/call/store expansion |
| `Resolver/OperatorResolution.cs`; `AST/Members/MemberDecl.cs`; `Resolver/ModuleResolver.cs` | Extend direct slots to inherited trait slots, bounds, newtype views and non-equality priority; share policy across resolvers |
| `AST/OperatorSelection.cs`; `AST/OperatorCloneContext.cs`; cloners in 5.6 | Full binder/view/refinement/abstract-signature correspondence, not name-based fallback |
| `Rewriters/RefinementTransformer.cs`; `AST/Modules/ScopeCloner.cs`; abstract/export signature construction | Replace M1 rejection guards only after checked final-member transport exists, including synonym-target refinement |
| `DooFile.cs`; `Options/OptionRegistry.cs`; `Options/OptionCompatibility.cs`; `Options/TranslationRecord.cs` | Replace source-only restrictions with the exact interpretation/coverage protocol in 11.1 |
| `Backends/Library/LibraryBackend.cs`; `Backends/ExecutableBackend.cs`; `Backends/DafnyExecutableBackend.cs` | Preserve original source provenance and check every reused compiled module before lifting reuse guards |
| `AST/Grammar/Printer/Printer.Statement.cs`; `TokenNewIndentCollector.cs` | Print/format compound and escape source without exposing generated temporaries |

Paths in this table are under `Source/DafnyCore/`. Keep the shared declaration/
applicability policy separate from resolver-specific constraints. Semantic-only
substitution may preserve just a resolved parenthesis/call; source/export clones
still need their distinct checked replay metadata. No table entry authorizes
operator-specific verifier or target-language equality semantics.

### 11.3 Legacy resolver (separate later PR)

M1 performs only an early enabled-mode prerequisite check and keeps the legacy
expression resolver unchanged. The parser's module-attribute conversion in 3.4
is mandatory because that phase still uses legacy resolution under refresh.
The complete feature later needs the following.

In `Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs`:

1. Handle `OperatorSyntaxExpr` before `BinaryExpr` intrinsic constraints. The
   current `Plussable`, `Minusable`, `Mullable`, `RankOrderable`, and
   numeric constraints must not reject or erase a possible user owner first.
2. Add a deferred operator obligation to the existing constraint-solving loop.
   Resolve operand expressions once, retain an unresolved result proxy, and wake
   obligations when receiver/type information is available. When committed to an
   intrinsic, enter the original operator logic. When committed to a call, enter
   the ordinary `ResolveApplySuffix` / `ResolveFunctionCallExpr` checks.
3. Reuse the current member-selection behavior, including instantiated parent
   views; do not simply set a `FunctionCallExpr.Function` field on an incompletely
   resolved call. `ResolveMember` and `PartiallyResolveTypeForMemberSelection` are
   the relevant existing entry points.
4. Add the feature-gated declared-trait-view support before the early rejection
   in `ConstrainSubtypeRelation_Aux`, which currently calls
   `Types.cs::IsHeadSupertypeOf`. Cover concrete value types and bound parameters,
   with substituted parent arguments, without relaxing unrelated constraints.
5. Audit `XConstraint.cs`'s `Equatable`/`EquatableArg` cases and the legacy cast
   branch for the same view relationships. Both comparability cases call
   `Types.cs::FromSameHead_Subtype`, whose differing-head path is currently
   restricted to reference/class-like relationships. Add a feature-gated resolver
   helper that supplies the correctly substituted declared trait view before
   those checks; retain `AllowSuperSub` and `TreatTypeParamAsWild` behavior.
   `self`'s intrinsic identity contract
   requires comparability of a value and its trait view as well as the conversion.
   Keep the change local to the enabled compatibility path rather than globally
   changing the old static `Type` helper's behavior.
6. In `CheckTypeInferenceVisitor.cs`, finish wrappers before resolving intrinsic
   opcodes or completing unary negation. Report an unresolved receiver explicitly
   rather than coercing it to a primitive family.
7. Extend `AST/Statements/Assignment/AssignStatement.cs` only for whole-RHS method
   operators, and resolve compound expansions through ordinary statements.

Deferred finalization must not rerun source name resolution after its lexical
scope has been popped. The existing call entry points consult mutable state such
as `scope.AllowInstance`, `currentClass`, and dominating statement labels; the
refreshed path also consults enclosing method-call/formal context. Resolve operand
names, labels, and source evaluations once in their original context, capture
the required call/visibility/lexical information, and factor the existing
selected-member argument/type-check finalization so it accepts those resolved
operands and that context explicitly. Queueing a freshly synthesized source call
for later `ResolveApplySuffix` is not a correct substitute. The refreshed guarded
constraint path must use the same discipline.

Use a worklist/guarded obligation, not speculative whole-expression retries that
leave duplicate constraints, diagnostics, or call edges behind. Return the exact
old control path immediately when the option is off. Primitive-only enabled
expressions should also fall through to the existing intrinsic implementation
once their selection is known.

### 11.4 Refreshed resolver (first useful PR)

In `Resolver/PreType/PreTypeResolve.Expressions.cs`, handle source wrappers before
`ResolveBinaryExpr` adds family confirmations/default advice. Use
`PreTypeConstraints.cs`'s guarded-constraint infrastructure and receiver
approximation to delay unresolved owners. Design descriptors and selection policy for later
reuse by the legacy resolver; use the refreshed resolver's normal member/call
resolution to construct the selected application.

Before `UnderspecificationDetector` assigns intrinsic opcodes, each wrapper must
have a chosen expansion or an enabled-mode diagnostic. Update/check
`PreTypeToType.cs` and `TypeRefinementVisitor.cs` so the wrapper and expansion
have consistent result types, including subtype refinements. A result expectation
constrains the selected call; it does not select a different operator. In the
later method/compound milestone, extend `PreTypeResolve.Statements.cs::ResolveAssignmentRhs`/`ResolveUpdateStmt` for method
RHS wrappers and compound expansions. Ordinary general-newtype receiver handling
and parent substitutions must remain intact.

### 11.5 Semantic consumers, backends, and editor

The following are an audit checklist of places where the new semantic invariant
must hold, not permission to add a second operator semantics in each consumer:

| Existing path | Required invariant/check |
|---|---|
| `Resolver/CallGraphBuilder.cs`; `Resolver/ModularTermination.cs` | Calls are present early, including trait calls; no lost edges or synthesized termination exemptions |
| `Resolver/BoundsDiscovery/BoundsDiscovery.cs` | User relation never yields intrinsic exact/numeric bounded pools |
| `Resolver/CoCallResolution.cs`; `CollectFriendlyCallsInSpecVisitor.cs`; `Abstemious.cs` | Named equivalence and user arithmetic/order operators get ordinary-call treatment |
| `AST/ExtremeLemmaSpecificationSubstituter.cs`; `ExtremeLemmaBodyCloner.cs` | No conversion of named equivalence calls to coinductive/prefix equality |
| `Resolver/ConstantFolder.cs`; `FillInDefaultLoopDecreasesVisitor.cs` | No primitive algebra or well-founded order inferred from source spelling |
| `Resolver/ExpressionTester.cs` and ordinary ghost-interest passes | Compilability, ghost formals, and by-method treatment match explicit calls |
| `Triggers/TriggersCollector.cs` | Same candidates and selected trigger structure as the named call |
| `Verifier/BoogieGenerator.ExpressionWellformed.cs` | All ordinary receiver, reads, requires, self-contract, and decreases checks remain |
| `Verifier/BoogieGenerator.ExpressionTranslator.cs` | Translate a function call as a function call, including old/labeled heaps |
| `Verifier/Expressions/TranslateBinaryExpr.cs` | Continues to handle intrinsic opcodes only |
| `AST/Statements/CalcStmt.cs`; `Verifier/Statements/BoogieGenerator.TrStatement.cs::TrCalcStmt` | Connectors stay intrinsic; only line/hint source expressions can contain user calls |
| `Backends/SinglePassCodeGenerator/SinglePassCodeGenerator.Expression.cs` and existing call-statement emission | Erase wrappers; retain ordinary static/instance/by-method ABI and runtime checks |
| `AST/Grammar/Printer/Printer.Expression.cs`; `Printer.Statement.cs` | Source/resolved modes print deliberately; preserve parse precedence and `builtin` grouping |
| `AST/Grammar/TokenNewIndentCollector.cs::SetIndentUpdateStmt` | Recognize compound source ranges, retaining token trivia |
| `AST/IHasReferences.cs`; `DafnyLanguageServer/Workspace/SymbolTable.cs` | Distinguish symbolic navigation references from identifier replacement ranges |
| `DafnyLanguageServer/Handlers/DafnyRenameHandler.cs`, hover/definition/reference handlers | Rename the callable's textual names without replacing symbolic uses |

Paths in this table are relative to `Source/DafnyCore/` except the explicit
`DafnyLanguageServer/` paths, which are relative to `Source/`. Existing generated
type equality and runtime hash code should need no change. If a backend needs an
operator-specific equality hook, the permanent equality rule has been violated.

### 11.6 Documentation and repository requirements for later product PRs

Update the Reference Manual in:

- `docs/DafnyRef/Types.md`: eligible owners, inherited instance slots, static
  ownership, subsets/newtypes, trait bounds, and equality-support distinction.
- `docs/DafnyRef/Expressions.md`: selection priority, unchanged precedence/chains,
  intrinsic escape, null/static views, and ordinary call obligations.
- `docs/DafnyRef/Statements.md`: compound evaluation order, method-RHS limits,
  and intrinsic `calc` connectors.
- `docs/DafnyRef/Attributes.md`: exact `operator` attribute grammar and validation.
- `docs/DafnyRef/UserGuide.md` and option documentation/help: default false,
  project setting, library-mode matching, and independent prerequisite options.
- Grammar/reference listings where the repository derives or presents the
  accepted syntax; describe the contextual forms without reserving new words.

A concise first-milestone manual statement should say:

> With user-defined operators and the refreshed type system enabled, a supported
> source arithmetic/comparison operation selects the applicable intrinsic first,
> otherwise the unique directly declared function operator at its static operand
> type. A selected user operator has exactly the checks and meaning of its named
> call. Equality and inequality, collection identity, equality-support
> characteristics, termination orders and calc connectors remain intrinsic.
> This initial milestone does not support override mode, compound assignment,
> symbolic trait/bounded dispatch or library/precompiled input reuse.

Later updates add non-equality priority and intrinsic escape, ordinary trait/view
support, checked artifacts and whole-RHS/compound syntax only when those stages
are implemented. Do not document the final feature as available in M1.

Each later product PR needs the appropriate `docs/dev/news/194.feat` fragment
(or staged descriptive fragment following repository practice), regression and
expectation files, `.github/review/base` and `.github/review/regressions` updates,
and the gate evidence required by `AGENTS.md`/`REVIEW.md`. Do not update expected
verdicts speculatively. Follow the prescribed fail/artifact/reasoned-baseline
procedure if a new test changes a gate inventory. Merge product PRs one at a time
and reconcile the base according to current repository rules. This design PR
does not claim the feature has shipped and needs no product-version change.

## 12. Smallest useful PR, size, tests, and subsequent stages

### 12.1 Exact first-milestone contract

**Yes: refreshed resolver only, no override mode, and no compound assignment is a
coherent useful first PR.** It implements real rational/matrix expressions, not
merely attribute parsing. Add the feature prerequisite and limit the accepted
surface deliberately; do not claim the complete design already exists.

M1 accepts directly declared ordinary functions/predicates (including supported
opaque/ghost/by-method functions) on concrete classes and inductive datatypes.
Both instance and directly owned static forms work. Owner type parameters use
ordinary call substitution; member type parameters do not. It supports binary
`+ - * / %`, unary `-`, and `< <= > >=` through the existing precedence/chaining
rules and intrinsic-first policy. It checks ordinary contracts/reads/ghostness,
constructs ordinary calls, and supports normal fully checked source modules,
concrete imports and revealed exports.

It intentionally rejects exactly these feature-dependent forms:

1. `--user-defined-operators` without `--type-system-refresh`, even when the
   input contains no operator use. Report one clear prerequisite error.
2. Any `"override"` mode, even on an unused callable; `==`/`!=` declarations
   permanently; every unsupported symbol, including all six bitwise symbols;
   malformed/nonliteral/extra attribute arguments, duplicate attributes or slots,
   wrong effective arity, non-Boolean comparisons, default/`nameonly` formals,
   and member-level type parameters.
3. Operator declarations owned by traits, newtypes, codatatypes, source abstract
   types, modules/default classes, or inherited symbolic slots. Ordinary named
   trait calls inside a supported operator body remain ordinary calls and are
   not prohibited by this restriction.
4. A symbolic use requiring method effects, inherited or bounded-trait lookup,
   a hidden/abstract/general-newtype operator owner, an extension/right-operand
   search, an implicit user conversion, or a nominal receiver still unresolved
   after ordinary inference. Existing intrinsic expressions on unsupported
   owner categories continue to use the existing intrinsic path.
5. Compound assignment and `builtin.<op>(...)` syntax. Neither grammar extension
   is introduced in M1; these inputs retain the existing parse failure. Their
   absence is coherent because fallback operators hide no intrinsic operation.
6. Refinement or module replacement involving a **feature-bearing module/view**,
   meaning a validated operator descriptor or a source occurrence selected as a
   user call in the relevant transitive module/view graph. Include bases,
   refinements, replacements and exposed nested views; a local scan of the new
   refining source is insufficient. A base can contain only intrinsic source
   and a refining module introduce the descriptor. Plain modules using only
   existing intrinsics with no feature-bearing dependency need no new ban.
   Because `RefinementTransformer.PreResolve` precedes validation of newly authored
   descriptors, its early guard conservatively scans enabled source operator
   attributes in unresolved descendants, combines them with completed dependency/
   view summaries, and confirms the closure before accepting a resolved clone.
7. Abstract-signature imports carrying the feature, and opaque export views
   hiding a selected operator owner/member or intrinsic family. Ordinary
   concrete imports/revealed exports are allowed, but **must** transport records
   through `ScopeCloner` to its final member/owner objects. An implicit/default
   export is not exempt from the check. Reject an inconsistent view; never erase
   its metadata and repeat candidate selection.
8. Feature-on `.doo` production/consumption, trusted `--library` source inputs,
   standard-library `.doo` reuse, and precompiled module input reuse. Fully
   checked source can be supplied normally. Suppress automatic `.dtr` output
   and reject explicit record input/output requests when enabled; keep the OFF
   record read/write/error path unchanged.
   Section 11.1 defines the later protocol that relaxes this initial source-only
   restriction. Existing externally trusted code retains its explicit trust
   boundary; no automatic guarantee is claimed for code linked without records.
   Compiler-owned canonical #193 support, if that independent option is enabled,
   is distinguished only by its authenticated provenance as described in 6.5.
9. User-operator lookup inside module-attribute arguments. Those expressions
   retain intrinsic-only syntax in M1 because that phase still uses the legacy
   resolver under refresh. Explicit named calls remain available (3.4).

A valid fallback annotation is **not** rejected merely because a particular
operation selects a built-in. For example, homogeneous datatype `<` remains
rank comparison even if `less` has a fallback annotation; the named call is
available. This keeps one selection rule instead of a second declaration-time
reachability analysis. M1's help/hover/tests must make that behavior clear.
Numeric-newtype operator declarations are excluded entirely in M1; its ordinary
intrinsic arithmetic is unaffected.

M1 needs the simple direct-owner subset of 5.6: records on wrappers/chain edges,
copying with either `CloneResolvedFields` value, checked source-to-final export
member maps, and a failure for unavailable saved operations. It may defer the
full trait/refinement/binder-view correspondence only because the above guards
exclude those accepted-language paths. All other clone/substitution routes must
preserve the semantic call/intrinsic already selected. "No overrides" alone is
not a sound reason to omit this work.

### 12.2 Estimated changed lines by file

These are **planning estimates of added plus deleted hand-edited lines**, not a
measured patch, deadline or performance claim. They include direct instance/static
members, concrete generic owners, chains, simple binding/export transport,
source-only artifact guards/output suppression and safe symbolic references. They exclude generated
parser output and unrelated cleanup. Factoring may move code between the proposed
new files; a roughly 50% planning error is plausible.

| File | Estimated changed lines | Work |
|---|---:|---|
| `Source/DafnyCore/Options/CommonOptionBag.cs` | 20–35 | Option, actual M1 help and registration |
| `Source/DafnyCore/Options/DafnyCommands.cs` | 1–3 | Parser option exposure |
| `Source/DafnyCore/Dafny.atg` | 20–45 | Guarded factories and module-attribute intrinsic conversion hook; no new terminals |
| `Source/DafnyCore/AST/Grammar/ParserNonGeneratedPart.cs` | 50–100 | Source wrapper factory and recursive intrinsic module-attribute conversion |
| New `Source/DafnyCore/AST/Expressions/Operators/OperatorSyntaxExpr.cs` | 75–130 | Syntax, ordinary expansion, cloning/formatting origin |
| New `Source/DafnyCore/AST/OperatorSelection.cs` | 70–130 | Immutable choice/type-view recipe |
| New `Source/DafnyCore/AST/OperatorCloneContext.cs` | 70–120 | Direct-owner correspondence and final-object fixups |
| `Source/DafnyCore/AST/Expressions/Operators/ChainingExpression.cs` | 30–60 | Edge wrappers and saved choices |
| New `Source/DafnyCore/Resolver/OperatorResolution.cs` | 160–260 | Signature validation, direct slots, static applicability, scope diagnostics |
| `Source/DafnyCore/AST/Members/MemberDecl.cs` | 10–20 | Descriptor/slot identity |
| `Source/DafnyCore/Resolver/ModuleResolver.cs` | 45–90 | Integration, prerequisite and feature-view guards |
| New `Source/DafnyCore/Resolver/PreType/PreTypeResolve.Operators.cs` | 230–360 | Deferred constraint, chosen ordinary call and captured context |
| `Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs` | 30–60 | Wrapper branch and selected-call finalization factoring |
| `Source/DafnyCore/Resolver/PreType/UnderspecificationDetector.cs` | 10–25 | Reject unresolved wrappers before primitive completion |
| `Source/DafnyCore/Resolver/PreType/PreTypeToType.cs` | 0–15 | Audit existing concrete-wrapper fusion; change only if needed |
| `Source/DafnyCore/Resolver/PreType/TypeRefinementVisitor.cs` | 0–15 | Audit existing resolved traversal |
| `Source/DafnyCore/AST/Cloner.cs` | 35–70 | Base transport/final correspondence hooks |
| `Source/DafnyCore/AST/Modules/ScopeCloner.cs` | 90–160 | Map final export views and reject lost selections |
| `Source/DafnyCore/Rewriters/RefinementTransformer.cs` | 25–55 | Transitive feature-bearing refinement/replacement guard |
| `Source/DafnyCore/AST/Substituter.cs` | 0–10 | Existing resolved-child path should suffice; regression gate |
| `Source/DafnyCore/AST/Grammar/Printer/Printer.Expression.cs` | 35–65 | Source/resolved printing and grouping |
| `Source/DafnyCore/AST/Expressions/Applications/FunctionCallExpr.cs` | 10–20 | Synthesized symbol is not an editable identifier range |
| `Source/DafnyCore/AST/IHasReferences.cs` | 5–15 | Nontextual reference kind |
| `Source/DafnyLanguageServer/Workspace/SymbolTable.cs` | 10–25 | Preserve reference kind |
| `Source/DafnyLanguageServer/Handlers/DafnyRenameHandler.cs` | 10–25 | Leave symbolic uses unchanged during rename |
| `Source/DafnyCore/DooFile.cs` | 10–20 | Reject feature-on artifact imports before reparsing |
| `Source/DafnyCore/Backends/Library/LibraryBackend.cs` | 5–15 | Reject feature-on library output |
| `Source/DafnyCore/Backends/ExecutableBackend.cs` | 20–40 | Keep empty incoming-record state; reject feature-on record requests and skip automatic output |
| `Source/DafnyCore/Backends/DafnyExecutableBackend.cs` | 5–10 | Stop the second compilation path after a record-input/output rejection |
| `Source/DafnyCore/Options/TranslationRecord.cs` | 0 | No M1 schema/validation changes; later artifact stage owns them |
| `Source/DafnyCore/AST/Grammar/ProgramParser.cs` / existing input-validation owner | 10–25 | Early trusted-source/standard-library input guard |

The ranges sum to **1,091–2,023 product lines**. Allow roughly **650–1,000** lines
of focused tests/expectations and **150–250** of manual/help/release-note work:
about **1.9–3.3K changed lines total**, before generated output. This is a
front-end feature PR, not a small parser-only edit. If final-view transport or
call-context factoring materially exceeds that budget, split tested scaffolding
into a prerequisite PR; do not reduce the safety contract to fit a line count.

No legacy expression-resolver implementation, new verifier axiom/runtime
operator, or per-backend user-operator implementation belongs to M1. Existing
call compilation supplies the semantics. The zero-minimum rows are deliberate:
`PreTypeToType` already fuses concrete wrappers and `Substituter` already
substitutes their resolved child, so an audit/test may be all that is needed.

### 12.3 M1 tests and measurable acceptance

Use `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-194.dfy`
as the driver, with separate `Inputs/github-issue-194-*` positive/negative files
and expectations. Add focused clone/AST/parser tests in `Source/DafnyCore.Test`
and rename tests under `Source/DafnyLanguageServer.Test`. Keep explicit-call
reference programs beside supported operator programs.

| Area | Minimum observable test |
|---|---|
| OFF parser and attributes | Valid and malformed legacy inputs; identifiers `operator`/`builtin`; unknown and malformed operator-looking attributes; future escape/compound strings. Exact old tokens, parse errors, diagnostic order/text/locations and exit codes. |
| Module attributes | Primitive/nested/chain arguments and leading/post-keyword attribute forms reach the existing legacy phase without wrappers; user-operator-dependent arguments reject and named calls remain usable. |
| Option prerequisite | ON without refresh gives the declared single prerequisite diagnostic and never enters unsupported legacy lowering. |
| Positive syntax | Rat `+ * <=`, Mat `+ *`, unary minus, a static member, an enclosing generic owner, nested uses, precedence and source print/format/parse round trip. |
| Actual selected meaning | Datatype `<`/`>` still intrinsic rank; datatype `<=` fallback is its predicate. A rank operation rejected in compiled code does not retry a user predicate. Equality remains intrinsic with a same-named `eq` present. |
| Calls | Corresponding operator/named-call failures for requires, reads, null receivers, ghost formals, wrong operand/result type and termination. Same selected call edge, receiver evaluation and diagnostics class. |
| Declaration negatives | Every excluded mode/symbol/owner/member form in 12.1, including unused malformed/override/equality declarations. Duplicate keys do not choose by result or other-operand type. |
| Inference | Known parameters, nested call results, literal RHS, late receiver constraints, unresolved receiver, constraint-order permutations; no duplicate edges/diagnostics from retries. |
| Chains | Per-edge selection survives rebuilding; second-edge preconditions use short-circuit facts; no endpoint transitivity assumption. |
| Clone/export | Both base-cloner field modes, repeated clones, a selected member declared after its use, final export-member mapping, revealed-export positive, hidden-owner/member negative, the opaque-rank example in 5.7. Unit-injected stale/missing records fail instead of rebinding. |
| Excluded source views | Transitive refinement/replacement/abstract-import cases fail even if the local refining file has no attribute. Ordinary non-feature source views retain their existing behavior. |
| Semantic traversal | A match, lambda, comprehension and old/labeled expression containing a supported user call retain its receiver/argument checks. Ordinary named equivalence receives no equality/bounds/coinduction privilege. |
| Source-only artifacts | Feature-on `.doo`/trusted-source/standard-library/precompiled input restrictions; no automatic `.dtr`, explicit record requests rejected, required empty incoming-record state initialized; OFF artifacts/invalid-record errors unchanged. |
| IDE | Renaming `times` updates its declaration and named calls but never replaces `*`; hover/definition identify the selected member/intrinsic without duplicate editable references. |
| Execution | Checked rational/matrix programs agree with named expansions on Python and another existing supported backend; static/instance/by-method behavior matches ordinary calls. |
| Vacuity | Inhabited successful call contexts followed by `assert false` produce an error at that assertion. A prior parse/type/contract error is not a passing vacuity test. |

For each product milestone, compare baseline and candidate with the feature off
under the deterministic settings in `REVIEW.md`. Required comparisons include
literal parsed/resolved output and Boogie, diagnostic corpus, full suite and
standard-library verdicts, and their required resource-count profiles. Do not
normalize away changed tokens, selection, generated names, axioms or triggers.
Control only independently identified incidental timestamps/build metadata.
The new option/index/worklist does no work on the disabled path.

For enabled syntax versus its named expansion, compare normalized semantic AST,
selected members/type arguments, call graph, Boogie, trigger terms, runtime
results and ordinary well-formedness checks. Source locations can affect report
identifiers/order; explain and minimize such differences before claiming cost
parity. The notation has no intrinsic proof-speed advantage. Existing suite/
standard-library baseline updates follow the fork's fail/artifact/reasoned
procedure, never an anticipatory verdict rewrite.

### 12.4 Subsequent independently reviewable milestones

The first PR is useful without waiting for all later rows. Equality override is
absent from this sequence.

| Stage | Scope | Required gate before relaxing M1 restrictions | Addendum product estimate |
|---|---|---|---|
| D0 | This design/review plus addendum sections 15–24 | Concrete reviewable design only; no product implementation in this packet | Documentation only |
| M1 | Direct function/predicate arithmetic/order operators, refreshed resolver, fallback only, source-only artifacts, simple export records and safe references | Entire 12.3 matrix plus OFF suite/library/output gates | Existing M1, not re-estimated |
| E1, after M1 | Read indexing and value application on M1 owners, with shared suffix records and paired IDE references | Selected ordinary calls before primitive constraints/triggers; named/arrow priority; preserved clones; no hidden array helpers | 636–1,167; per-file 22.1 |
| E2, after E1 | Pure functional update and four slice shapes; nested updates and tuple keys | Missing bounds stay absent; result types are ordinary call results; generated colon slices stay intrinsic | 188–348; per-file 22.2 |
| B1, after M1 | Direct-owner function/predicate bitwise fallback | Existing precedence/mixing/cardinality parsing; family priority; no user native privilege or automatic count rules | 81–161 after E1, or 101–201 independently; 22.2 |
| M2a | Trait descriptor inheritance and explicit trait-view calls, including available surface keys | Diamond deduplication, unrelated ambiguity, stable metadata and ordinary dispatch/frames/termination | Existing M2 scope |
| M2b | Bound-aware lookup and full refinement/abstract/export transport for those keys | All 14 clone routes, both 5.7 negatives, checked view/binder correspondence and frozen generic choices; no inferred assignment policy | Existing M2 scope; 22.4 |
| M3 | Full `.doo`/`.dtr` interpretation/coverage protocol from 11.1 | Both mismatch directions; original-source reload; hidden metadata/flattened-source negatives; full precompiled coverage | Existing M3 scope |
| M4 | Legacy resolver and declared-trait-view parity, including already shipped new surfaces | Exact notation/named-call pairs under both resolvers; intrinsic capability parity checked per surface | Additional adapters 235–440; per-file 22.4 |
| M5a | Supported general-newtype/codatatype owners and contextual intrinsic escape | Nominal/opaque views, existing intrinsic ancestry, no implicit result lifting or new arrow conversion, disabled parser identity | New-surface adapters 65–135; per-file 22.4 |
| M5b | Explicit non-equality arithmetic/order override | Escape already available; frozen choices across generic/refinement/export/library paths; no equality, bitwise, indexing or application override | Existing M5b scope |
| M6a | Compound capture infrastructure and pure value indexed assignment | Ordinary writable roots, pure expression RHSs, one-block expansion, old-value capture, whole-root alias-equality and frame/subtype checks | Part of indexed 403–688 total; 22.3 |
| M6b | Single-result arithmetic methods and zero-result indexed setters | Ordinary call effects; no nested expression methods; reject setter multi-assignment; compounds freeze each read/operation/write choice | Remainder of indexed 403–688; existing arithmetic work additional |
| Independent #193 | Bounded integer bitwise intrinsics and their own reference/model/backend work | U/I matrix; no native privilege for user attributes; all #193 model/vacuity/count/result/codegen gates | Not included here |

If M2b is too large, separate the ordinary checked type-view/binder transport
from symbolic-bound lookup; do not expose an accepted path between those PRs
without its guard. If M3 and M4 are reordered, the compatibility contract remains
unchanged. The full artifact mode protocol can later be relaxed only with a
separately reviewed per-module interpretation scheme.

Later stages retain M1 tests and add:

- no-fallback-on-failure tests for overrides and #193 count/result checks;
- hidden/opaque views, newtype/subset result obligations and bounded generic
  specialization; inherited metadata must be signature-stable;
- compound field/array/multidimensional destinations, aliases and methods that
  mutate the destination or its address computations;
- calc intrinsic connectors, named equivalence versus structural collection
  identity, recursive contracts, old heaps, ghost/by-method and termination pairs;
- all supported backend/artifact combinations and effective vacuity controls.

Slot indexing is linear in visible/effective member count; direct lookup is
constant time after ordinary type/view preparation. Later bound lookup is
proportional to its declared bound slots before deduplication. A worklist avoids
exponential candidate backtracking. These are algorithmic expectations, not
measured speedups; default-off work remains absent.

## 13. Critical review, next question, and implementation decision

### 13.1 Findings, severity first

These are design/implementation risks and dispositions, not claims that a product
patch exists. P0 means possible proof of false or verification/runtime
disagreement; P1 means a required compatibility/capability defect; P2 means a
usability or maintenance issue.

| Rank | Finding | Disposition |
|---|---|---|
| P0 | Re-resolving inherited/exported operators can change a proved proposition even with no equality override. | Separate immutable choices, semantic-family identity, final-declaration maps and checked replay; both concrete negatives in 5.7. M1 includes basic records and rejects complex views. |
| P0 | A user call left as a primitive opcode can acquire bounds, arithmetic, rank or coinduction reasoning it has not proved. | Lower to ordinary calls before semantic consumers; preserve original receiver/argument obligations; no overloaded semantic opcode. |
| P0 | A missing record on resolved source can be mistaken for a fresh operator occurrence. | Distinguish unresolved parsed source from replay-required source; a lost resolved choice is an error, never a new lookup. |
| P0 | Library/precompiled interpretation may differ between verification and execution; per-module `.dtr` metadata misses default-module-only programs. | Source-only M1 guards and no reusable record output; later record-level interpretation marker including default modules, mismatch validation and complete coverage protocol. |
| P0 | `calc` composition could assume unproved laws or skip user preconditions. | Every connector remains intrinsic; named lemma calls or calculations over Boolean propositions remain available. |
| P0 | Arbitrary operator attributes/native-looking names could acquire #193 native implementations or BV bridge facts. | Keep #193 intrinsic identity/capability distinct; canonical specialization, if chosen later, is a separate reviewed compiler mechanism. |
| P1 | Uniform explicit equality override conflicts with a promise that all specification/compiled equality is intrinsic. | Remove `==` and `!=` overriding; named `eq` distinguishes mathematical equivalence. Exact site matrix in 6.1. |
| P1 | An equivalence relation does not preserve representation observers or collection identity. | Checked rational example makes the distinction explicit; no global congruence or quotient semantics inferred. |
| P1 | A ghost integer reference function is not automatically executable/native because a wrapper has an operator attribute. | Compare executable library, specialized canonical library, and intrinsic routes separately; retain independent #193 intrinsics. |
| P1 | Refreshed mode still sends module attributes through the legacy resolver. | Intrinsic-only module-attribute syntax in M1, with explicit named calls available; test the path before claiming legacy resolver unchanged. |
| P1 | “Both resolvers” and “trait bounds” would hide significant missing existing functionality. | Useful refreshed-only M1; explicit rejected forms and later legacy/bound-view prerequisites. |
| P1 | New tokens or parser alternatives can change disabled errors even when normal parsing is guarded. | No new terminals in M1; later contextual semantic hooks, generated-table checks and exact invalid-input OFF corpus. |
| P1 | Queued source re-resolution can lose lexical/label context or make lookup depend on constraint iteration order. | Resolve operands once, capture ordinary-call context, use deterministic delayed head discovery, and error when underdetermined. |
| P1 | Purely textual operator references can corrupt source during rename. | Nontextual reference metadata and rename safety accompany the first callable operator, not a later polish stage. |
| P2 | Valid fallback `<` on a datatype may be hidden by rank meaning; numeric-newtype fallback arithmetic is not modular. | State built-in priority, show selected meaning, keep named calls, and require explicit later non-equality override where necessary. |
| P2 | First source-only milestone cannot import prebuilt standard libraries. | Document the concrete restriction and budget the subsequent artifact stage; do not pretend artifact compatibility is free. |
| P2 | One-slot lookup and no method-generics exclude some convenient overloads. | Keep predictable inference; use ordinary named calls for those operations. |
| P2 | Small client expressions save characters but annotations increase a one-use program. | Report both costs and the 5/4/7-use amortization points; make no unmeasured comprehension/proof-speed claim. |

### 13.2 Alternatives and decision record

The detailed arguments appear at each decision point:

| Choice | Why the recommendation wins | Main cost / rejected alternative |
|---|---|---|
| Attributes (2.4) | Ordinary callable/export identity and no declaration grammar change | Less prominent than dedicated syntax; requires string validation |
| Deferred contextual intrinsic escape (4.2) | Uniform arithmetic/rank coverage using existing tokens; M1 needs none | More verbose than infix annotation; `===` solves only an equality distinction this proposal removes |
| Intrinsic equality (6.2) | Same substitutive relation in proofs, execution and collections | Named mathematical equivalence is longer; classes-only buys no extra soundness |
| Fallback first, later explicit non-equality override (2.3, 5.2) | Useful M1 without hiding intrinsics; later supports modular and strict-order notation deliberately | Two priority modes eventually; declaration-first is simpler but changes more meanings |
| Direct static and instance calls (2.4) | Reuses normal calls; later instance slots carry trait obligations | No symbolic static inheritance/typeclass mechanism |
| Wrapper plus ordinary call (8.1) | Source notation survives while semantic consumers see honest calls | Requires separate selection records for source clones; an overloaded BinaryExpr would rely on every consumer remembering an exception |
| Independent #193 intrinsics (6.4) | Native plan and width metadata without six-member facades or a #194 dependency | Canonical library specialization remains a viable compiler alternative, not a library-only implementation |

### 13.3 Next design question, answered: when may delayed lookup commit?

**Question:** Can an operator use choose a slot as soon as an approximate
receiver type appears, without making meaning depend on inference order or
expected-result search?

**Answer:** commit only after ordinary constraints determine a unique static
nominal head/view; do not use a guessed intrinsic family or candidate's result to
manufacture that head. Use the following bounded, non-backtracking procedure:

1. Resolve source operand names, applications and labels once. Allocate a result
   proxy and a pending operator obligation that retains the ordinary call
   context. Its original static source owner is not yet guessed.
2. Let ordinary receiver/argument declarations, nested already selected call
   results, assignments and comparable-type evidence propagate. An expected
   type may constrain an existing result proxy by ordinary typing, but it does
   not enumerate operator owners or compare candidate return types.
3. When the left/unary head is uniquely fixed, find the slot in that view and
   apply the fixed intrinsic/priority rule. Once chosen, add the selected call's
   normal argument/result constraints exactly once. Freeze the completed view
   recipe when its type arguments are resolved. Owner choice does not get
   reconsidered after a contract/compiled-use failure.
4. If no nominal choice is pending, preserve ordinary primitive literal/default
   inference. Do not default an unresolved would-be nominal receiver to an
   integer merely because the token is `+` and thus bypass an annotation error.
5. At the existing inference completion boundary, a still-underdetermined
   operator gives the explicit annotation/named-call diagnostic. There is no
   search over every declared operator and no retry of a rejected selected call.

For `a.times(b) + c`, the named call's result supplies the owner normally. For
`a * b + c`, the selected `*` result can supply the next `+` owner. A lambda
whose receiver type is known from an ordinary declared arrow type can work;
an otherwise untyped `x => x + x` must not choose some user numeric library by
looking at all available return types. An explicit `(x: Rat) => x + x` or a
named call makes the intent determinate. No new numeric literal embedding is
inferred by this procedure.

`PreTypeConstraints` already has guarded constraints, but the integration must
retain lexical `scope.AllowInstance`, current class, enclosing formals/call
context and dominating heap labels as needed by normal call resolution. It must
not call a source-name resolver later after its scope has been popped. Tests
permute declaration/constraint discovery order, exercise nested operators and
late evidence, and compare selected member IDs and error text. This settles the
design question; actual helper factoring and generated AST parity are M1 work.

### 13.4 Review conclusion and handoff

1. **Settled enough to implement, and what that frees.** The intrinsic equality
   rule, ordinary-call boundary, independent #193 surface, immutable choice
   invariant, and concrete source-only refreshed M1 are specified. A worker can
   implement M1 without waiting for equality overloading, legacy trait parity,
   compound syntax, integer bitwise implementation or full artifact support.
2. **What is expected to change.** Replace the old equality-override policy,
   broad initial rollout and vague clone-record paragraph with these rules.
   Companion #193 edits are precisely listed in 6.5. Later implementation may
   adjust call-finalization factoring, map representation and line estimates;
   it must not weaken the published meaning-preservation/disabled-mode gates.
3. **The next question.** Section 13.3 answers the next design question about
   deterministic delayed lookup. The next empirical question is whether the
   implemented direct operators produce the same normalized calls, VCs,
   triggers and proof costs as their explicit expansions. That needs a real
   M1 patch; this documentation review cannot measure it.
4. **Feedback on this request.** The fixed source/design pins, explicit
   source-only deliverable and priority order made the review concrete. The
   phrase about ghost/compiled equality needed an explicit distinction between
   source expressions and generated machinery. Asking for examples and counts
   also exposed a useful simplification: datatype `<=` needs no override.
5. **Why the review stops.** The six requested questions are answered, the
   proposed source forms are labelled unchecked, and existing-syntax controls
   are identified separately. Product implementation is deliberately outside
   this request, not an unfinished deliverable.

## 14. Primary source references and evidence boundaries

The source audit pin is
[`428e348b0b125cb67505f03a0026b3f497f98e48`](https://github.com/erniecohen/dafny/tree/428e348b0b125cb67505f03a0026b3f497f98e48).
The prior design reviewed is PR #239 at
[`5339faad320aab58b16f2a35ebae623f81c3413d`](https://github.com/erniecohen/dafny/blob/5339faad320aab58b16f2a35ebae623f81c3413d/docs/dev/design/194-operators.md).
The interacting #193 document is PR #247 at
[`a61b06c71d742ff19b280592d26d1a0e2ce9c87c`](https://github.com/erniecohen/dafny/blob/a61b06c71d742ff19b280592d26d1a0e2ce9c87c/docs/dev/design/193-bitwise-integers.md).
Subsequent edits to that live PR are not silently attributed to this audit.

Exact-pin entry points:

- [AGENTS.md](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/AGENTS.md)
  and [REVIEW.md](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/REVIEW.md)
  govern later product changes, default-off comparisons and public provenance.
- [Dafny.atg](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Dafny.atg),
  [pretype expressions](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs),
  and [ModuleResolver](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ModuleResolver.cs)
  establish syntax, strict datatype rank comparison, exported views and pipeline order.
- [Cloner](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Cloner.cs),
  [ScopeCloner](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Modules/ScopeCloner.cs),
  [RefinementTransformer](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Rewriters/RefinementTransformer.cs),
  and [method verification](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs)
  substantiate the source-clone and skipped-inherited-obligation risks.
- [Binary translation](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/Expressions/TranslateBinaryExpr.cs),
  [equality support](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Types/UserDefinedType.cs),
  [calc AST](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/AST/Statements/CalcStmt.cs),
  and [calc translation](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs)
  are the main equality and proof-rule boundaries.
- [TranslationRecord](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Options/TranslationRecord.cs),
  [DooFile](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/DooFile.cs),
  and [library backend](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Backends/Library/LibraryBackend.cs)
  establish the record coverage and original-source serialization constraints.
- [ExpressionTester](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Resolver/ExpressionTester.cs)
  and [call compilation](https://github.com/erniecohen/dafny/blob/428e348b0b125cb67505f03a0026b3f497f98e48/Source/DafnyCore/Backends/SinglePassCodeGenerator/SinglePassCodeGenerator.cs)
  distinguish ghost references, by-method functions and ordinary/native calls.

New functional checks in this revision use the public release
[`v4.11.0+fcb2042d.review.bf47de16`](https://github.com/erniecohen/dafny/releases/tag/v4.11.0%2Bfcb2042d.review.bf47de16),
not a newly built binary of the audit pin. The existing Ordered/Rat example's
historical check uses the separately identified `9a4451d7` release. All proposed
operator syntax, intrinsic escape, clone-record implementation, native-code
comparisons and line estimates remain design material. No implementation,
exact-pin runtime run, new performance benchmark, or completed #193 vacuity gate
is claimed.

## 15. Addendum Q1: read indexing and the shared surface representation

This addendum extends the complete design; it does not expand the already
implemented M1 contract retroactively. Sections 15–24 and the revised tables in
2.3 and 12.4 are the new recommendations. Earlier references to deferring user
bitwise/indexing syntax remain descriptions of M1, not permanent prohibitions.

The audit for this addendum uses source/design
[bb521a95](https://github.com/erniecohen/dafny/tree/bb521a9522ec584d4b0069a8e80ba9e48291dee4),
M1 [558551c8](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/docs/dev/operators-m1.md),
and the independent #193 design at
[5fa590ac](https://github.com/erniecohen/dafny/blob/5fa590ac006d15c78715c03d57064291626e23c3/docs/dev/design/193-bitwise-integers.md).
The [addendum request](https://github.com/erniecohen/dafny/issues/194#issuecomment-6814055563)
is the scope. No product implementation or Dafny execution is claimed here.
The supplied Dafny files have **unverified expected outcomes**. Python model
execution is reported separately in section 24.2.

### 15.1 Slots and ordinary calls

Declare a read slot with `{:operator "[]"}`. With `k >= 1` indices:

```dafny
function {:operator "[]"} get(i1: I1, ..., ik: Ik): R
static function {:operator "[]"} get(x: Owner, i1: I1, ..., ik: Ik): R
```

These are signature schemata; the packet's `q1-positive.dfy` is complete source.
The receiver/first formal supplies the static nominal owner. The indices and
result have their declared ordinary types; there is no implicit integer,
nonnegativity, finiteness, cardinality, or key-membership restriction. A string,
tuple or datatype key is as legitimate as an integer. A predicate may supply a
Boolean result. No method supplies a read expression.

`x[i,j]` is one call, `x.get(i,j)` or `Owner.get(x,i,j)`. In contrast,
`x[i][j]` is two independently selected calls. The outer occurrence uses the
static type of the first result. There is no empty-index spelling `x[]`.

Generalize the existing descriptor from unary/binary to a surface family and
effective operand count, plus a shape discriminator where needed:

```text
SurfaceKey = (family, effectiveArity, shape)
effectiveArity includes the owner, whether receiver or first static formal
IndexRead(k)       -> k+1 operands
Update(k)          -> k+2 operands: owner, k indices, new value
Application(n)     -> n+1 operands
Slice(mask)        -> 1+popcount(mask) operands, mask = written lower/upper bounds
IndexStore(k)      -> k+2 operands, in statement context only
```

An instance get2 and a static get2 collide. Get1 and get2 do not. Argument or
result types never break a same-key ambiguity. Slice(lower) and Slice(upper)
remain different despite equal arity. Retain the existing restrictions on
member type parameters, defaults, nameonly parameters and multiple attributes.
Enclosing-owner type parameters substitute normally. Do not synthesize an
argument tuple for `x[i,j]`: `x[(i,j)]` is a different, one-index slot.

The selected ordinary call supplies requires, reads, ghost-use checks,
termination and compiled admissibility. An ordinary function-by-method is
eligible; verification uses its function contract and compilation uses its
already verified implementation. No getter/updater relationship, extensionality
or array law follows from an attribute. A read-only indexer may read heap state
when its ordinary reads clause permits it.

### 15.2 Decide intrinsic capability before adding intrinsic constraints

Use a pending obligation until ordinary inference determines a receiver head.
Maintain the visible nominal view for slot lookup separately from the existing
intrinsic capability view. Then choose an intrinsic or the unique fallback,
before emitting constraints specific to that intrinsic.

The receiver-family reservations are deliberately per surface:

| Surface | Existing intrinsic family reserved before argument checks |
|---|---|
| Read `x[...]` | Sequence/string, array, map/imap, multiset |
| Functional `x[i := v]` | Sequence/string, map/imap, multiset |
| Range `x[lo..hi]`, including omissions | Sequence/string, array |
| Value application `f(...)` | An existing normalized arrow view |
| Indexed assignment | The existing intrinsic collection/array LHS rules; do not turn immutable sequence/map indexing into an lvalue |

Within a reserved family, wrong dimension, index count/type, key type, bound,
nullness, frame, compiled-use restriction or precondition remains an error on
the ordinary intrinsic route. A two-dimensional array used with the wrong
number of indices does not acquire a fallback; nor does `s[i,j]` for a sequence
newtype. No diagnostic/constraint rollback and no retry after proof failure.

A capability absent for a *different surface* is different: a visible newtype
over a map might later define slicing, because maps have no intrinsic slice
operation, while its map reads/updates remain intrinsic. Owner availability is
still gated by M5. Primitive collection types cannot acquire members.

Transparent aliases and visible subsets use the underlying owner's view.
Preserve existing revealed-newtype intrinsic behavior without losing the
nominal identity for member selection; never inspect hidden representations.
Do not automatically inherit a represented type's operator members into a
nominal newtype.

There is a real resolver parity issue, not just a shared registry task:
refreshed single selection follows `AncestorPreType`, whereas legacy
`Indexable`/`ContainerIndex`/`ContainerResult` starts from `NormalizeExpand`.
Thus general-newtype indexing cannot be advertised as supported by both
resolvers merely because both can find the same slot.
[Refreshed selection](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs#L2472-L2546);
[legacy constraints](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/NameResolutionAndTypeInference/XConstraint.cs#L43-L224).

For an unbounded type parameter, invent no indexing capability. M2 supplies
only visible, instantiated instance slots from declared bounds and the ordinary
bound-typed receiver view. An abstract/exported type needs an exposed member
signature and checked selection transport. Keep M1's guards until those named
call paths work. A saved generic user call remains that call after specialization
to an intrinsically indexable type. A frozen intrinsic does not become a member
call after a representation change.

### 15.3 Concrete hooks, source identity, and triggers

In the refreshed resolver, branch before the `SeqSelectExpr` and
`MultiSelectExpr` cases at
[212–249](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs#L212-L249).
The existing multi-select case imposes an array type and numeric indices
immediately. Running it first would poison a valid arbitrary-key user call.
The later legacy adapter branches before `ResolveSeqSelectExpr` and the
`MultiSelectExpr` case in `NameResolutionAndTypeInference.cs`, not after their
`XConstraint` emissions.

Retain `SeqSelectExpr`/`MultiSelectExpr` only as printable source inside the
generalized concrete-syntax wrapper. The semantic child is either the actual
intrinsic or an ordinary `FunctionCallExpr`. Extend the source-kind switch,
operand enumeration, direct source-root cloning, intrinsic selection identity,
and freezing together. M1 currently assumes binary syntax or unary minus in
[OperatorSyntaxExpr](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/AST/Expressions/Operators/OperatorSyntaxExpr.cs#L14-L36)
and [UnderspecificationDetector](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/Resolver/PreType/UnderspecificationDetector.cs#L278-L303).
Accepting more attribute strings alone is insufficient.

Every record includes family/count/shape, selected callable/slot, static versus
instance call form, receiver/type view, and intrinsic family where applicable.
Transport through all section 5.6 clone paths independently of resolved fields.
Retain source operands and delimiter origins; do not capture mutable inference
proxies or old lexical variables in the immutable selection. For a static call,
pass the owner once as an argument, with a type-only static receiver.

The trigger collector explicitly recognizes `SeqSelectExpr`, `MultiSelectExpr`
and `ApplyExpr` as well as `FunctionCallExpr`
([91–99](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Triggers/TriggersCollector.cs#L91-L99)).
It must see the ordinary selected call, not an index-shaped semantic node.
This applies to explicit `{:trigger x[i]}` and inferred candidates. Preserve
ordinary named-call trigger behavior; add no user-index trigger heuristic or
array-select encoding. Generated membership/collection operations stay intrinsic.
Named-call/notation trigger equivalence is a later implementation check, not a
claim proved by these design examples.

## 16. Addendum Q2: functional updates, repeated updates, and slices

### 16.1 Pure update slots and composition

Use `{:operator "[:=]"}` for a pure update function. It has `k >= 1` index
parameters followed by the new value; static form prepends the owner:

```dafny
function {:operator "[:=]"} put(i: I, v: V): R
static function {:operator "[:=]"} put(x: Owner, i: I, v: V): R
```

Expression `x[i := v]` selects `k=1` and means exactly `x.put(i,v)` or
`Owner.put(x,i,v)`. `R` need not be the owner type. A surrounding assignment
checks its ordinary result compatibility; another suffix selects from `R`.
The broader `k`-index descriptor is useful for section 18's multi-index
*assignment*, which calls a selected function directly without inventing a new
functional-update spelling.

Choose the user path before the existing `SeqUpdateExpr` constraints, especially
the constraint making the result agree with the source collection type
([250–289](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs#L250-L289)).
The legacy `SeqUpdateExpr` case needs the corresponding enabled fork.
Use a wrapper and ordinary call, with the same contract/reads/ghost/by-method
policy as read indexing. No mutation, preservation law or getter is implied.

The exact expansion of `x[i := v][j := w]` is `x.put(i,v).put(j,w)` when both
static result views select those members. Evaluation is receiver, first index,
first value, first call, second index, second value, second call. Each written
operand is evaluated once under ordinary ghost erasure. The second call receives
the first result; an explicit later occurrence of the variable `x` still denotes
that variable. There is no automatic rebinding of expressions inside later
indices/values. Alias variables holding the original value remain unchanged.

Repeated same-index updates are two calls. A conventional sequence wrapper may
have last-write-wins behavior, but the language assumes no such law. No batch
update operation or compiler folding is needed.

### 16.2 Multi-update spellings: a deliberate boundary

The current suffix grammar already parses nested updates and a tuple-valued
index, but does not parse either comma spelling:

| Source | Decision in this addendum |
|---|---|
| `x[i := v][j := w]` | Accept as two nested calls |
| `x[(i,j) := v]` | Accept as one call with a tuple key |
| `x[i,j := v]` | Keep the existing parser rejection; call a named multi-index updater |
| `x[i := v, j := w]` | Keep the existing parser rejection; write nested updates |
| `x[i,j] := v` | Already parsed assignment; section 18 can select a two-index updater/setter |

This is a scope decision, **not** a claim that new terminals or Coco alternatives
are technically unavoidable. As with section 8.2's proposed compound hook, an
enabled handwritten action before the closing bracket could consume existing
`:=`/comma tokens and parse more operands without changing the scanner.
That would still be new parser behavior, with new source representation,
recovery, grouping and evaluation-order commitments. Nested updates already
state sequential composition explicitly; tuple keys/named calls express
multi-index update without that work. Do not silently add the comma spellings
while describing the change as wrapping already parsed syntax.
The packet includes a parser-negative multi-index-update example.

### 16.3 Omitted slice bounds are absent arguments

Use four literal shape attributes; underscores are shape placeholders inside
the attribute string, not identifiers or new source tokens:

| Source | Attribute | Instance parameters | Named expansion |
|---|---|---|---|
| `x[lo..hi]` | `{:operator "[_.._]"}` | `(lo: L, hi: H): R` | `x.between(lo,hi)` |
| `x[lo..]` | `{:operator "[_..]"}` | `(lo: L): R` | `x.from(lo)` |
| `x[..hi]` | `{:operator "[.._]"}` | `(hi: H): R` | `x.until(hi)` |
| `x[..]` | `{:operator "[..]"}` | `(): R` | `x.all()` |

Static forms prepend `x: Owner` and use the same key. Names such as `between`
are illustrative ordinary member names. Only the written bounds are passed,
left-to-right after the receiver. No zero, length call, sentinel, optional
argument, implicit tuple, or standard-library `Option` type is manufactured.
Bounds can have arbitrary declared types, including unequal lower/upper types.
No result type, default endpoint, interval convention or identity law is built in.
In particular, `x[..]` need not equal `x`, and a library may require a nonempty
domain even for that shape.

**Why four shapes:** one function with presence flags reduces the number of
declarations but needs dummy bounds or an optional-value representation.
A numeric zero sentinel excludes other index types and conflates omission with
a written value. A compiler-owned option type adds a new dependency and encoding;
default arguments would change the existing operator eligibility rule. Four
fixed keys preserve exact ordinary named-call expansion with no hidden values.
A library can implement three small forwarding functions to its preferred
common slice helper. Missing one shape is an error; it is not inferred from
the others.

Preserve `SeqSelectExpr.E0/E1` nullness in source/clone metadata. Branch before
`ResolveRangeSelectionExpr` fixes a sequence result and numeric bounds.
Intrinsic sequences/arrays retain their existing endpoint conventions and
obligations; these do not leak into a selected user slice. Functional chaining
after a slice needs only nested ordinary calls.

Colon multi-slicing (`x[n:m:]`) is distinct. The parser constructs a
`SeqDisplayExpr` of generated slices and additions
([4489–4501](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Dafny.atg#L4489-L4501)).
Keep those generated operations intrinsic-only. Do not accidentally overload
this unrequested surface by wrapping every constructed `SeqSelectExpr`.

## 17. Addendum Q3: application of a user value

### 17.1 Shape and selection

`{:operator "()"}` marks an ordinary function/predicate with `n >= 0` argument
parameters; static form has owner plus those parameters. Results and argument
types are ordinary declared types. Zero-argument application is supported.
Require positional actuals on this new user-value path initially, matching the
no-default/no-nameonly/member-type-parameter policy. Existing named calls and
arrow calls retain their own actual-binding rules.

Preserve the order of
[ResolveApplySuffix](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs#L2015-L2148):

1. Resolve a name/member as an ordinary function, method, constructor, type or
   module. An ordinary named-call error is not permission to search an apply slot.
2. For a value with an existing normalized arrow view, use ordinary arrow
   application. Bad arity/type/requires/reads remains its ordinary error.
3. Only for a resolved non-arrow value, wait for its ordinary determined static
   head and select its unique Application(n) slot. A callable-valued field can
   enter this case. Use the captured lexical/heap context and resolved actuals.
4. An unresolved head, absent slot, unsupported view or same-key ambiguity is an
   enabled diagnostic, not a search over types suggested by the expected result.

An owner type name in function-call position does not construct an instance or
invoke an instance slot. A method cannot occur in an expression merely because
it has an operator attribute. Keep apply slots function-only in this addendum;
M6's arithmetic methods/setters do not implicitly broaden that rule.

A synonym/subset with an existing arrow view remains an arrow. A nominal
newtype of an arrow is **not automatically callable** at the pin: refresh tests
`DPreType.IsArrowType(dp.Decl)` and legacy uses `AsArrowType` after
`NormalizeExpand`. M5 may admit its own explicit apply slot; this addendum adds
no representation-to-arrow conversion.

Reject a non-null application `AtTok` on a selected user apply slot. Eligible
slots are one-state functions, so `f@L(args)` must receive the ordinary rule that
an @-label can only be applied to a two-state function. The fallback branch must
not silently discard a label that the existing named/arrow path would check.
Using `old@L(f(args))` remains a different, ordinary heap-context expression.

### 17.2 Nested calls and contract projections

`f(x)(y)` is ordinary composition: first apply `f` to `x`, then apply the
returned value to `y`. The result may be an arrow or another user callable.
There is no automatic partial application or inserted curry object.
`f(x,y)` selects one two-argument slot; it does not mean `f(x)(y)`.

Ghost/compiled use, reads, requires, opaque bodies, termination and by-method
execution are ordinary function-call rules. Do not synthesize `f.requires` or
`f.reads`, because the user value has not become an arrow. It may of course have
ordinary named members; the feature creates no reserved pseudo-members.
Where ordinary Dafny supports it, the actual member function value
`f.invoke` can be used with its usual `.requires`/`.reads` selectors. Inherited
slots use M2's ordinary dispatch and frozen declared view.

### 17.3 AST and implementation

`ApplySuffix` already is concrete syntax and hides its source children after
resolution
([ApplySuffix.cs](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/AST/Expressions/Applications/ApplySuffix.cs#L18-L36)).
Give operator-eligible value applications a saved selection and the same checked
transport service as other surfaces. Retain arrow-versus-user meaning across
clones; do not reselect after type substitution. Ordinary direct named calls
continue through their existing semantics.

Do not teach `ApplyExpr` to mean user application: it remains genuine arrow
application. Reuse M1's selected ordinary call finalization, including actual
parameters and type substitution. Do not manufacture an unresolved
`f.invoke(...)` name after its lexical scope has been popped.

The later legacy hook is `ResolveApplySuffix` in
`Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs`.
The trigger collector's existing `AnnotateApplySuffix` specifically preserves
call terms across module re-resolution
([231–241](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Triggers/TriggersCollector.cs#L231-L241)).
Its saved candidate must preserve the frozen call and nontextual reference.
Neither an extra arrow-application term nor a new user-application trigger
operator belongs in the semantic output.

## 18. Addendum Q4: indexed assignment, aliasing, and frames

### 18.1 Two explicit destination policies

A concrete class/reference destination uses `{:operator "[]:="}` on an ordinary
**zero-result method**. It has one or more index parameters followed by a value;
static form prepends the owner. `c[i,j] := v` selects the two-index setter.
It needs no getter and implies no read-after-write law. A setter can affect
anything permitted by its ordinary modifies clause, including locations unrelated
to the indices.

A concrete value destination, initially an inductive datatype, uses the pure
`{:operator "[:=]"}` updater. Its result must be assignable to the actual root
storage type, including subset obligations. It likewise needs no getter.
`x[i,j] := v` may call a two-index updater directly even though
`x[i,j := v]` remains a parser error. The expression form in section 16 selects
only the one-index key.

Choose and freeze Setter versus Replacement from the static visible capability,
never a runtime type or hidden representation. Initial M6 admits concrete
class/datatype owners. A reference-trait setter can later use M2 dispatch when
its visible slot specifies that policy. A general abstract/bounded destination
whose policy is not determined remains rejected until an explicit visible
capability rule is supplied. Ordinary read/index/apply support through traits
does not by itself authorize generic writeback.

### 18.2 Accepted roots and restrictions

A value replacement needs an ordinary writable root: mutable local/out variable,
mutable field, or intrinsic array element (including a multidimensional element).
Thus `h.value[i] := v` and `a[k][i] := v` can replace the field/array element
that holds the value. Capture the root address once. The root's ordinary
initialization, nullness, bounds, read/write-frame and result-subtype checks remain.

An input parameter, constructor result, function result or intermediate user
value-index result is not a writable root. Do not invent recursive lenses for
`x[i][j] := v` when `x[i]` is a user value read. Conversely, an object returned
by a getter can be the receiver of an ordinary mutable-field assignment,
`x[i].field := v`, or of a class setter. The receiver of a class setter does not
itself need to be an lvalue.

Intrinsic indexing families retain their existing LHS rules. An array bounds
failure cannot trigger a setter fallback; a sequence/map read does not become
mutable merely because an update expression exists. Assignment to a slice is
not added. A getter-only type does not supply storage.

For the first indexed-assignment milestone, require pure expression RHSs in
every statement containing a user indexed destination. Reject method RHSs,
allocation RHSs, havoc and assign-such-that variants on that new path. A
function-by-method still counts as an ordinary pure function expression.
These restrictions make all accepted captures and replacement computations
observe the pre-statement heap. Other existing statements retain their behavior.
Effectful RHS support would need an additional explicit interference/evaluation
design; it is not silently inherited from whole-RHS arithmetic method operators.

### 18.3 Single assignment expansion

For a class setter, the conceptual ordinary expansion is:

```dafny
// r[i,j] := v;
var t := r;
var a := i;
var b := j;
var u := v;
t.set(a,b,u);
```

Evaluate receiver, indices left-to-right, then value, each once, and perform the
ordinary selected call. Static form uses a type-only static receiver and passes
`t` once. The full method precondition, modifies/reads, ghost and termination
checks apply. There is no final store generated after the method, and no
postcondition `t[i,j] == u` unless the method itself supplies it.

For a value in a field:

```dafny
// h.value[i] := v;
var p := h;
var a := p.value;
var k := i;
var u := v;
var b := a.put(k,u);
p.value := b;
```

This is ordinary pure-call-plus-store behavior, including a read of the old
root. A local root needs no address temporary; an array root captures array and
indices before reading its old value. Use hygienic temporaries with ordinary
ghost inference and correct declared/subset types. Do not force ghost operands
into executable temporaries or bypass definite initialization.

### 18.4 Parallel assignment means parallel whole-root replacement

For a multi-assignment containing value-index destinations and no method setter:

1. In LHS order, capture each root's address, old value and index arguments.
   For a normal non-index destination, capture its address without inventing a
   read of its old value.
2. In RHS order, evaluate each pure RHS once.
3. In LHS order, compute each proposed whole-root replacement from its captured
   old value and the corresponding RHS. A later updater does not receive an
   earlier updater's result.
4. Perform one ordinary simultaneous assignment to all captured roots, including
   ordinary destinations in the same statement.

Use the existing pairwise condition: **root locations are distinct, or complete
replacement values are equal**. The verifier already checks this for ordinary
assignment
([CheckLhssDistinctness and multi-assignment](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrAssignment.cs#L127-L184)).
No new frame or alias axiom is needed.

For a conventional row updater and `x = Row([1,2])`,
`x[0], x[1] := 7,8` proposes `Row([7,2])` and `Row([1,8])` for the same
location, so verification fails. Different indices do not prove disjoint roots.
`x[0], x[0] := 7,7` can verify because the complete replacements are equal.
Distinct variable names do not prove distinct heap fields: `p.value` and
`q.value` may alias and get the same check. With distinct roots,
`x[i], y[j] := y[j], x[i]` uses old values as expected.

For ordered composition, write `x := x[i := v][j := w]`. Do not reinterpret a
parallel statement as that fold: arbitrary updaters have no commutation or lens
laws.

Reject every multi-assignment containing a user **method setter**, even when
indices or receiver variables differ. Its frame can overlap another destination,
and one setter can invalidate another's precondition. Successive explicit
statements/calls express the order and expose the ordinary interference
obligations. This conservative boundary is preferable to assuming independent
array-cell semantics from bracket notation.

### 18.5 Preserve the assignment AST contract and relate to M6 compounds

Both resolvers currently resolve LHS expressions before checking assignability.
The new outer destination needs a context-specific path **before getter
lowering**, so getter-free setters/updaters work and an ordinary function call
never masquerades as an lvalue.
[ConcreteAssignStatement](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/AST/Statements/Assignment/ConcreteAssignStatement.cs#L37-L50);
[refreshed assignment](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Statements.cs#L543-L604).

Store a frozen per-LHS selection and destination policy on the original source
statement, transported independently of `ResolvedStatements`. A root-capture
recipe contains source structure/view information, not stale generated locals.
Reconstruct hygienic captures after transport.

Install **one ordinary BlockStmt** as the resolved expansion. Inside it put the
capture declarations and then the ordinary selected CallStmt or one ordinary
parallel AssignStatement. Do not append captures/calls as several entries of
`AssignStatement.ResolvedStatements`: the verifier casts the multi-entry case
to `SingleAssignStmt`
([TrUpdateStmt](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs#L496-L526)).
The existing verifier/backend paths should suffice unchanged.

M6's later `x[i] += y` needs separately frozen getter, binary operator and
setter/updater selections. Capture receiver/root and indices once; read via the
selected getter, apply the selected operation, then call the setter or compute
and store the whole-root update. No getter/setter coherence is assumed.
Single assignment here does not need a getter. This explicit expansion lets
compound and method work reuse the same capture machinery without expanding
their acceptance boundary prematurely.

Initial M6 also rejects a user indexed destination as an **aggregate `forall`
update**. Its capture block is not the special single-assignment body recognized
by the existing resolver; a proof/call body does not acquire heap effects from
this lowering
([forall classification](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Statements.cs#L324-L376)).
An ordinary aggregate whole-root assignment remains available, for example
`forall k | 0 <= k < a.Length { a[k] := a[k].put(0,v); }`, with the ordinary
preconditions and parallel-update checks. Pure user calls in aggregate RHSs
remain permitted. A separate quantified destination lowering is not assumed.

### 18.6 Frame expressions are values or genuine intrinsic locations

The array-element location form puts **a backtick before the bracket**, under
`--referrers`:

```dafny
a`[i]
```

It constructs
`IndexFieldLocationExpression`, not `SeqSelectExpr`
([FieldLocationBody](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Dafny.atg#L4522-L4548)).
It requires a statically available intrinsic array and correct dimensions.
User get/set/update metadata never supplies that cell. Reject a user-owned
non-array operand even if its getter returns an object. The legacy resolver's
independent restriction on field-location expressions remains until separately
changed.

Ordinary `reads x[i]`, `modifies x[i]` or `unchanged(x[i])` instead evaluates
the selected getter as an ordinary function. It is allowed exactly when its
result is an ordinary permitted frame value and its call is well-defined there.
An object result names that object's frame; a permitted collection names its
members. An integer result is not a frame. This never denotes a cell inside `x`
or grants a setter permission.

An object returned by a getter may support its own genuine field-location
expression; an array result may support a subsequent genuine indexed location.
That is ordinary composition through the returned value. It does not create
user-defined storage semantics.

Setter calls must fit their complete callee frame into the caller's frame.
Pure replacement needs the updater's ordinary reads plus permission to read and
write its root field/array element. Retain existing intrinsic location handling
in `ExpressionTranslator.TranslateSeqSelectExpr` and `InRWClause_Aux`;
neither may receive a selected user read disguised as an array selection.


## 19. Addendum Q5: bitwise symbols are ordinary fallback calls

### 19.1 Slots, count types, and no extra meaning

Allow direct function/predicate fallback slots for all six existing spellings:

| Symbol | Instance shape | Static shape | Result/count rule |
|---|---|---|---|
| `& \| ^` | `f(b: U): R` | `f(a: Owner, b: U): R` | Ordinary declared U and R |
| Unary `!` | `f(): R` | `f(a: Owner): R` | Ordinary R; no truthiness conversion |
| `<< >>` | `f(k: K): R` | `f(a: Owner, k: K): R` | K is the declared count parameter type |

Use attributes such as `{:operator "&"}` and `{:operator "<<"}`.
No member gets a semantic width, bitvector identity, axiom, native lowering or
algebraic law from the symbol. Function-by-method remains ordinary verified
execution. Keep method bitwise slots and bitwise compound-assignment spellings
outside this function-only stage; M6 does not silently enable them.

Recommend `int` or `nat` for conventional count APIs, but do not impose either.
A library may declare a count datatype, or a function accepting negative counts
with its own meaning. It must prove its body and satisfy its declared contract.
There is no special bitvector-to-integer count conversion or inferred
`0 <= k <= width` condition on a user call. The unique slot is selected before
argument checks; those checks cannot choose another slot.

Requiring an integer count universally would add a special type rule with no
ordinary-call justification. General declared K is simpler and supports the
same conventional API through the function signature. In particular, the
packet's Bits wrapper explicitly requires `0 <= k <= 8`; its rejection of -1
comes from that contract.

### 19.2 Intrinsic coexistence, including #193

The base supports intrinsic `!` on bool and bitvectors. Intrinsic
`& | ^ << >>` are bitvector operations, **not** Boolean binary operations.
Boolean `&&`/`||` stay short-circuit intrinsic syntax and cannot be declared.
Preserve those exact family distinctions
([binary resolution](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs#L1279-L1295);
[type families](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Resolver/PreType/PreTypeConstraints.cs#L690-L703)).

Reserve an applicable intrinsic family before checking the other operand/count
and its obligations. A bad bitvector count or #193 result predicate is an error,
not permission to invoke a user member. Preserve nominal/visible capability
information and recorded semantic width as section 5.6 requires.

With `U = --user-defined-operators` and `I = --integer-bitwise`:

| U | I | Ordinary supported datatype/class with a slot | Properly width-qualified revealed integer newtype | Existing bool/BV |
|---|---|---|---|---|
| off | off | No selected user meaning | No #193 meaning | Existing rules |
| on | off | Ordinary fallback call | A declared user fallback only after M5 permits that owner; otherwise existing error | Existing rules |
| off | on | No selected user meaning | #193 intrinsic | Existing rules |
| on | on | Ordinary fallback call | #193 intrinsic takes priority; failure does not fall back | Existing rules |

Primitive `int` cannot gain members through this feature. Hidden representations
do not reveal width or slots. A frozen call from an abstract/trait view remains
a call even when a concrete specialization would choose an intrinsic for newly
written source. User attributes never gain #193's compiler privilege. The
independent [#193 selection contract](https://github.com/erniecohen/dafny/blob/5fa590ac006d15c78715c03d57064291626e23c3/docs/dev/design/193-bitwise-integers.md#63-both-resolvers-and-issue-194)
already has the required separation; no #193 implementation is included here.

### 19.3 Preserve the actual grammar, not conventional expectations

From strongest to weakest among these groups: unary `!`, `& | ^`,
`* / %`, `+ -`, `<< >>`. Thus `a << b + c` is `a << (b + c)`.
Repeated same-symbol bitwise operators associate left. Mixed unparenthesized
`a & b | c` remains the existing ambiguity diagnostic; use explicit parentheses.
The current shift sequence allows mixed shifts with its existing left association.

The cardinality production suppresses unparenthesized `& | ^` at the top level
inside `|...|`. Parenthesized expressions re-enter ordinary parsing:
`|(a & b)|` parses and is meaningful if the returned value supports intrinsic
cardinality. Suffix argument expressions also retain their existing contexts.
Do not overload cardinality or treat its closing bar as a user operator.
[ShiftTerm through UnaryExpression](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Dafny.atg#L3596-L3706)
is authoritative.

## 20. Addendum Q6: parser fit and disabled identity

### 20.1 Existing productions and precise guarded changes

All **accepted** surfaces in sections 15–19 already have a parsing path.
Recognition needs no new production, alternative, scanner token, precedence
level or associativity rule. Guarded source-construction/metadata actions do
need edits; "already parses" does not mean "no parser source changes."

| Surface | Existing hook in `Dafny.atg` | Enabled action |
|---|---|---|
| `x[i]` | `Suffix`, `SeqSelectExpr` at 4504 | Retain source root/delimiters in a wrapper |
| `x[i,j]` | `Suffix`, `MultiSelectExpr` at 4477 | Same, and delay intrinsic-only array registration |
| `x[i := v]` | `Suffix`, `SeqUpdateExpr` at 4507 | Wrap this source occurrence with update key |
| Four `x[..]` shapes | `Suffix`, range select at 4488 | Retain both omission bits; no invented operands |
| `f(x)`/`f()` | `Suffix` at 4511–4515; name/dot call actions around 4345/4436 | Reuse ApplySuffix; retain paired punctuation and later frozen selection |
| Indexed assignment LHS | `Lhs` at 3732–3741 uses `Suffix` | No new grammar; carry source context to destination resolution |
| `& \| ^` | `BitvectorFactor` at 3677/3681 | Guarded source factory, unchanged mixing checks |
| `<< >>` | `ShiftTerm` at 3596–3622 | Guarded source factory, unchanged adjacency/token coalescing |
| Unary `!` | `UnaryExpression` at 3704–3705 | Wrap only eligible source Not, not other unary operations |

Source links:
[suffix construction](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Dafny.atg#L4320-L4515),
[LHS/unary/bitwise](https://github.com/erniecohen/dafny/blob/bb521a9522ec584d4b0069a8e80ba9e48291dee4/Source/DafnyCore/Dafny.atg#L3596-L3741).
The symbol strings in attributes are ordinary existing string literals.

Do not route `FieldLocationBody` through user indexing. Colon multi-slicing's
generated additions/selects also remain intrinsic. Genuine source operations
inside their operand expressions still resolve normally; generated machinery
does not become eligible merely because its node class matches a source class.

The multi-index action unconditionally enqueues
`SystemModuleModifiers.Add(b => b.ArrayType(...))` at 4479. When enabled,
defer that registration until intrinsic multi-index selection needs it, before
building that intrinsic's ordinary type constraints. A selected user call should
not leave a synthesized arrayN type that its explicit named-call expansion did
not request. Do not require this change on the disabled path. The later output
comparison must confirm that registration timing has not changed intrinsic
meaning or introduced observable unused helpers.

The two new comma-update spellings rejected in section 16.2 could be implemented
with a guarded pre-`]` handwritten parser hook, using existing tokens and
Expression routines. That would be additional recognition and recovery work.
It is deliberately not part of the accepted surface/table above.

### 20.2 Disabled identity argument and limits of the evidence

When U is false, execute the original construction actions, including array
registration and malformed-input recovery. New guards perform no lookahead,
allocation of generated names, descriptor validation, constraint registration,
member search, serialization changes or reference creation before returning to
that path. Unknown `operator` attributes retain the old inert behavior,
including malformed/unsupported attribute contents.

Leave terminal declarations, Coco alternatives/predicates, scanner tables,
FIRST/FOLLOW sets, error IDs/text and the current mixed-bitwise diagnostic
unchanged. Regenerate rather than hand-edit generated parser files. This is a
constructive argument for the implementation boundary, **not an observed
byte-identity test result**.

Extend M1's
[IntrinsicAttributeCloner](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/AST/Grammar/ParserNonGeneratedPart.cs#L20-L37)
for every new source shape. Module attributes still enter the old intrinsic
legacy phase until M4 changes that restriction explicitly. Function attributes
and triggers are not blanket-disabled. Original unresolved application suffixes
retain normal named/arrow behavior there; a new user-call fallback must not leak
into that phase.

Error recovery for an already invalid `x[i,j := v]`, `a & b | c` or malformed
suffix must remain the existing parse path, regardless of a visible user member.
When grammar succeeds but lookup fails, issue a targeted enabled diagnostic:
missing family/arity/shape, ambiguous slots, unsupported static view, or
undetermined receiver. A selected call's argument/contract failure remains its
ordinary diagnostic, with punctuation as the primary source and callee as
related information.

## 21. Addendum Q7: paired punctuation, navigation, and rename

M1 already represents references with `IsTextual`, marks selected function calls
nontextual, and excludes their ranges from rename edits.
[IHasReferences](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/AST/IHasReferences.cs#L14);
[FunctionCallExpr](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/AST/Expressions/Applications/FunctionCallExpr.cs#L155-L156);
[rename filtering](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyLanguageServer/Handlers/DafnyRenameHandler.cs#L60-L64).

For a selected bracket/application suffix:

- Give its opening and closing delimiter **separate token-sized hit ranges**,
  both targeting the same member and displaying the same selected-call meaning.
- Treat the closing delimiter as an alias of one canonical occurrence at the
  opening token. Find-references returns that occurrence once.
- Keep receiver/index/argument identifiers and nested operations at their own
  ranges. Do not use the entire `[...]` or `(...)` span.
- Rename initiated at either delimiter may resolve the backing declaration,
  but edits only the declaration and explicit named references. Neither
  punctuation token is changed.
- In `f(x)` the identifier `f` still names its variable/member; the parentheses
  name the selected apply slot. Each pair in `f(x)(y)` has its own selection.

The full-span alternative is unsafe with current navigation, which prefers
larger containing ranges
([SymbolTable.GetDeclarationNode](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyLanguageServer/Workspace/SymbolTable.cs#L166-L179)).
It could capture an index's own hover or definition. Two independent occurrences
would avoid that but duplicate find-references. Canonical occurrence plus alias
hits is the recommended small extension.

Add an optional canonical occurrence range to `Reference`, defaulting to its
own `Referer`. Populate hit maps/OperatorMeanings with both punctuation ranges,
but canonicalize `NodeToReferences`. Extend selected call source metadata to
retain both tokens through cloning/substitution, and emit the references from
one source of truth, not both wrapper and resolved call. Keep all old behavior
on ordinary textual references.

For setters, place the paired nontextual reference metadata on the generated
`MemberSelectExpr` and emit it there, rather than adding another reference on
`CallStmt`. Its existing textual reference must be replaced for that selected
occurrence, because the call's child traversal already visits the member select
([MemberSelectExpr references](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/AST/Expressions/Applications/MemberSelectExpr.cs#L297-L298)).
Ordinary method-name references retain their default textual behavior.

Copy this metadata explicitly in `AST/Substituter.cs` when it rebuilds member
selections and function calls. It currently copies `IsOperatorCall` on rebuilt
function calls; new delimiter/canonical-occurrence fields need the same treatment
([reconstruction](https://github.com/erniecohen/dafny/blob/558551c864b84df46c13be1855e4f6b157ff3d74/Source/DafnyCore/AST/Substituter.cs#L122-L180)).
Do not assume ordinary resolved-child substitution alone preserves new IDE data.

An intrinsic selection has no user definition target, but can show its intrinsic
meaning at either token. An indexed assignment shows its selected setter or
updater, not an invented getter. A later compound shows the several selected
operations in hover and can choose its principal destination operation for
navigation. `:=` or `..` may be secondary hover aliases if useful, but they add
no separate occurrence and must not capture operand ranges.

Source printing/formatting retains existing syntax, grouping and spacing;
resolved/debug printing may show the named expansion. An invalid/incomplete
suffix needs no selected-member hover. No formatter or IDE feature may require
type resolution merely to preserve the written delimiters.

## 22. Addendum Q8: milestone placement and incremental per-file estimates

E1/E2/B1 in section 12.4 are independent additions to the useful M1 surface,
not a reason to hold M1 for M2 or M5. E1 establishes shared suffix transport and
IDE infrastructure with reads/application; E2 adds updates/slices. B1 can ship
after M1 alone or reuse E1's structural work. None requires arbitrary symbols,
equality override, new axioms, native code generation, or setter semantics.

M2a/M2b extend the same keys through ordinary trait/bound views; their original
lineage, visibility and clone gates remain. M5 enables only owners/representations
that the equivalent named calls and intrinsic capabilities already support.
M5b's arithmetic/order overrides do **not** silently authorize overriding
intrinsic indexing, slicing, application or bitwise symbols. The new surfaces
remain fallback-only. Read/apply through an abstract view does not imply an
assignment policy; section 18.1's restriction remains.

The estimates below are incremental **added plus deleted hand-edited product
lines against M1**, excluding generated output, tests, documentation and #193.
They are planning ranges, not measured diffs; roughly 50% error remains plausible.
They count shared work once. Paths beginning `AST/` or `Resolver/` are relative
to `Source/DafnyCore/`; LanguageServer paths are relative to `Source/`.

### 22.1 E1: shared suffix infrastructure, read indexing and application

| File | Changed lines | Work |
|---|---:|---|
| `Source/DafnyCore/Dafny.atg` | 15–35 | Source-only construction; defer intrinsic array registration |
| `AST/Grammar/ParserNonGeneratedPart.cs` | 25–50 | Shared factory and module-attribute restoration |
| `AST/Expressions/Operators/OperatorSyntaxExpr.cs` | 80–140 | Variadic source kinds, operands, direct clone reconstruction and punctuation |
| `AST/Expressions/Applications/ApplySuffix.cs` | 30–55 | Saved value-application selection and replay |
| `AST/OperatorSelection.cs` | 40–70 | Surface/count/shape and intrinsic-family recipes |
| `AST/OperatorCloneContext.cs` | 10–25 | Correspondence/replay support for new recipe variants |
| `Resolver/OperatorResolution.cs` | 50–85 | Keys, arities and declaration validation |
| `Resolver/PreType/PreTypeResolve.Operators.cs` | 180–290 | Pending surface selection, ordinary selected-call helpers |
| `Resolver/PreType/PreTypeResolve.Expressions.cs` | 85–150 | Branch before primitive constraints/noncallable diagnostics |
| `Resolver/PreType/UnderspecificationDetector.cs` | 25–50 | Generalized freeze/finalization boundary |
| `AST/Grammar/Printer/Printer.Expression.cs` | 15–35 | Source/resolved output for suffix wrappers |
| `AST/IHasReferences.cs` | 8–16 | Canonical occurrence range with unchanged default |
| `AST/Expressions/Applications/FunctionCallExpr.cs` | 20–40 | Paired nontextual source hits |
| `DafnyLanguageServer/Workspace/SymbolTable.cs` | 35–65 | Alias hits, canonical references and meanings |
| `DafnyLanguageServer/Handlers/DafnyHoverHandler.cs` | 8–18 | Paired delimiter meaning/signature |
| `DafnyLanguageServer/Handlers/DafnyRenameHandler.cs` | 0–8 | Existing filtering should mostly suffice |
| `Triggers/TriggersCollector.cs` | 0–8 | Audit suffix candidate preservation; no new heuristic |
| `Options/CommonOptionBag.cs` | 5–12 | Update enabled option's actual supported-surface description |
| `AST/Substituter.cs` | 5–15 | Preserve new function-call source metadata during reconstruction |
| **E1 total** | **636–1,167** | Shared work is not charged again below |

If E1 needs splitting, land structural scaffolding first with the acceptance
guards still present, then reads/application. Do not expose partially supported
clone or trigger paths between those changes.

### 22.2 E2 and B1 after E1

| File | E2 updates/slices | B1 bitwise | Work |
|---|---:|---:|---|
| `Source/DafnyCore/Dafny.atg` | 8–16 | 10–20 | Existing construction actions |
| `AST/Grammar/ParserNonGeneratedPart.cs` | 12–24 | 10–20 | Source factories/intrinsic attribute traversal |
| `AST/Expressions/Operators/OperatorSyntaxExpr.cs` | 25–50 | 10–20 | Update/slice forms; unary Not |
| `AST/OperatorSelection.cs` | 10–20 | 5–10 | Shape/mask and intrinsic family identity |
| `Resolver/OperatorResolution.cs` | 20–35 | 15–25 | Allowed keys/signatures |
| `Resolver/PreType/PreTypeResolve.Operators.cs` | 70–120 | 20–40 | Fallback/call handling and intrinsic capability classification |
| `Resolver/PreType/PreTypeResolve.Expressions.cs` | 30–55 | 0 | Avoid source/result collection constraints on user path |
| `Resolver/PreType/UnderspecificationDetector.cs` | 8–16 | 8–18 | Complete shape freeze |
| `AST/Grammar/Printer/Printer.Expression.cs` | 5–12 | 0 | Omitted bounds and update printing |
| `DafnyLanguageServer/Workspace/SymbolTable.cs` | 0 | 3–8 | Unary/bitwise meaning strings |
| **Totals** | **188–348** | **81–161** | No duplicate E1 scaffold/paired-reference work |

If B1 precedes E1, allow **101–201** product lines instead, primarily to generalize
the existing unary-only wrapper/freezing paths. Trait, newtype and legacy
availability still follows their respective milestones.

### 22.3 M6 indexed destinations, with existing M6 work accounted separately

These are the *additional indexed-destination* lines, not an estimate for all
previously planned compounds and arithmetic methods.

| File | Changed lines | Work |
|---|---:|---|
| New `Resolver/IndexedAssignmentLowering.cs` | 220–340 | Static policy, root capture, pure-RHS checks and ordinary block construction |
| `AST/Statements/Assignment/AssignStatement.cs` | 35–65 | Per-LHS frozen choice and one-block expansion/clone transport |
| `AST/Statements/Assignment/ConcreteAssignStatement.cs` | 10–25 | Context-aware destination entry before read resolution |
| `Resolver/PreType/PreTypeResolve.Statements.cs` | 65–110 | Pending destination selection and ordinary-call/store finalization |
| `Resolver/OperatorResolution.cs` | 20–40 | Setter method and update arity validation |
| `AST/OperatorSelection.cs` | 25–50 | Destination-policy recipe without captured generated locals |
| `AST/Expressions/Applications/MemberSelectExpr.cs` | 15–30 | Nontextual setter source metadata, replacing the generated textual reference |
| `DafnyLanguageServer/Workspace/SymbolTable.cs` | 10–20 | Destination operation meaning |
| `AST/Substituter.cs` | 3–8 | Preserve setter member-selection metadata |
| **Indexed M6 total** | **403–688** | Split pure replacements and singleton setters if useful |

No new product rule is expected in `BoogieGenerator.TrAssignment.cs`,
`BoogieGenerator.TrStatement.cs`, `BoogieGenerator.ExpressionTranslator.cs` or
the compilers. A need for new alias, heap-location or setter axioms is a reason
to recheck the lowering, not an intended extension.

### 22.4 Existing later milestones: adapter work, not hidden prerequisites

| Stage/file | Incremental range for these surfaces | Obligation |
|---|---:|---|
| M4: `Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs` | 235–420 | Read/apply/update/slice/bitwise/destination forks using the shared policy |
| M4: `Resolver/NameResolutionAndTypeInference/XConstraint.cs` | 0–20 | Prefer emitting existing constraints only on intrinsic branches; no global weakening |
| M5: `Resolver/PreType/PreTypeResolve.Operators.cs` | 35–70 | Nominal owner versus visible ancestor capability per surface |
| M5: `Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs` | 20–40 | Feature-gated supported-newtype parity, if M4 has shipped |
| M5: `Resolver/OperatorResolution.cs` | 10–25 | Eligible owner/ordinary callable constraints |
| M2: existing inheritance/bound/clone implementation | No separate additive budget claimed | Feed generalized keys into M2's already planned work; accept no path lacking its transport proof |

The adapters cannot claim newtype/arrow parity by erasing representations
indiscriminately. Existing M3 artifact guards/protocol and M1 source-only
restrictions remain in force. This packet adds no runner bookkeeping, verdict
ledger, repository snapshot, or speculative baseline updates.

## 23. Addendum Q9: arbitrary symbols and declared precedence

A separate language could parse an operator sequence into operands, existing
token groups and explicit parentheses, then associate it using a scoped fixity
table. Precedence climbing or a stack algorithm is linear in sequence length
for a fixed comparable precedence relation. That association pass is the easy
part of the work.

It would conflict with this request's fixed precedence/associativity policy if
it changes any binding powers. Merely assigning existing symbols to their
existing classes adds little capability. Do not schedule arbitrary declared
precedence as a milestone of this addendum.

"No new scanner tokens" also bounds the available spelling alphabet. A sequence
of existing punctuation tokens can be recognized by a separate enabled parser
hook, but an unrecognized character still fails lexing. Existing shifts,
disjointness, comparisons, generic brackets, arrows, short-circuit operators,
attributes and assignment must retain unambiguous meanings. Guarded handwritten
consumption can leave Coco tables unchanged; it does not remove the need to
design a second enabled grammar and its error recovery.

If future demand justifies it, resolve **syntactic fixity before operand type
resolution**, independently of the selected member. Type-specific fixity is
circular: grouping determines intermediate types, types select owners/operators,
and those choices would determine grouping. It can yield multiple or no
interpretations and contradict the deterministic selection design.
A workable future rule gives each symbol a module/import-scoped fixity, rejects
conflicting imported declarations and honors all explicit parentheses.
It is "after symbol/fixity name lookup", not after type-dependent overload
resolution. Preserve comparison chains separately; their short-circuit and
adjacent-edge semantics are not ordinary binary associativity.

The main costs are:

- Retain flat syntax and meaningful partial trees for incomplete programs;
  diagnose malformed expressions locally before unrelated type resolution.
- Preserve parser recovery; choosing old precedence after a common flat parse
  does not preserve disabled malformed-input behavior.
- Let the formatter operate without successful resolution. Fixity/import edits
  can regroup a module and invalidate its IDE/verification caches.
- Transport the fixity environment with source/module views and artifacts;
  reprinting inherited text under a new environment must not change a proof.
- Preserve source spans, explicit grouping and comparison-chain behavior, and
  specify adjacency/whitespace rules for multicharacter token sequences.

A separate experiment would plausibly require:

| Product area/file | Changed lines |
|---|---:|
| `Dafny.atg` enabled hooks | 100–180 |
| New operator-sequence parser/fixity pass | 250–450 |
| New flat syntax AST | 100–180 |
| Module/fixity collection | 100–200 |
| Printer/formatter | 100–180 |
| Declaration transport and IDE invalidation | 100–200 |
| Options/diagnostics | 80–160 |
| **Total before tests/artifact work** | **830–1,550** |

These are design estimates, not evidence about another language's implementation.
The suffix and fixed bitwise additions deliver useful library APIs without
these costs. Revisit arbitrary fixity only with concrete operators that the
fixed forms cannot reasonably express.

## 24. Next question, answered, and evidence boundary

### 24.1 Where may lowering occur relative to heaps, binders, and control flow?

**Next question:** can a resolver hoist these calls or their capture temporaries
out of `old`, a quantified body, a lambda or a short-circuit branch?

**Answer: no.** Pure expression lowering replaces the selected occurrence *in
place*, retaining its lexical binders, heap label, evaluation context and normal
well-formedness conditions. For example:

```dafny
old(x[i])       // old(x.get(i))
x[old(i)]       // x.get(old(i))
p && f(x)      // p && f.invoke(x), if the selected result is bool
```

These are expansion schemata, not interchangeable heaps. Ordinary `old` rules
still decide which subexpressions read the old heap. No synthesized temporary
may escape its binder or force a call that belonged to an unselected branch.
The resolver must not resolve a delayed member name in a later lexical scope.
Reuse the captured ordinary call context from M1 and the frozen recipe, with
all ordinary checks. The same discipline applies to explicit trigger attributes
and ordinary labelled two-state calls inside operands. Selected user apply
slots themselves reject `@` application labels as section 17.1 specifies.

Only section 18's statement lowering introduces capture temporaries, inside a
new block at the original statement and control-flow position. Every expression
inside those captures is resolved in the original context. A static selected
member uses a type-only static receiver and includes the owner once as an
actual argument; it does not erase a source receiver's well-formedness checks.
The normal call and block semantics, not an operator-specific heap axiom,
provide the relative correctness argument.

This also explains why getters, pure updaters and method setters cannot acquire
implicit coherence laws. A library can prove a lens/order/bitwise interface's
laws through ordinary traits, but merely selecting the same punctuation never
licenses regrouping, parallel setter calls or rewriting a getter after a store.

### 24.2 What the packet establishes

The accompanying standalone Dafny sources cover all five requested surface
groups, including all four slice shapes, single/multi-index reads and writes,
zero-argument and nested application, ghost/reads/by-method cases, tuple-key and
chained updates, bitwise precedence examples, ordinary call-precondition
failures, alias/frame failures, parser/resolver rejection boundaries and
`assert false` controls. Every expected Dafny result is explicitly
**unverified**. Current M1 does not implement these surfaces, and no Dafny
compiler was run for this addendum.

A "positive" file contains positive substantive declarations plus a deliberately
failing named false-control declaration. It is not expected to verify as one
error-free file. A resolver-negative file cannot establish its embedded
false-control outcome; use the separate ordinary named-call control or the
independently resolving positive/control case. No `assume`, axiom, admitted
stub or contradictory precondition is used to make a positive example pass.

The standard-library-only Python models were run:

| Model | Executed result | Coverage |
|---|---|---|
| `models/functional_model.py` | **18/18 tests passed** | Receiver/index/value order and single evaluation; persistent aliases; all slice masks; tuple keys; changing result type; failure stops; 288 exhaustive small chained-update cases |
| `models/assignment_model.py` | **20/20 tests passed** | Root capture; old-value swaps; mixed ordinary/value destinations; root alias equality; multi-index updater; singleton setters; frame/precondition failures; rejected setter multi-assignment |

The 288 exhaustive cases are included in one of the 18 tests, not additional
test methods. Both models exercise ordinary desugarings and explicit expected
states/traces; they do not test Dafny resolution, clone transport, trigger output,
backend execution, source recovery or disabled byte identity.

Later implementation validation should compare accepted notation with its exact
ordinary expansion and retain effective negative/false controls. It should
check selected-call/call-graph/trigger/frame behavior, source clone transport,
paired IDE references and disabled output at the already required gates.
There is no new test-runner or acceptance ledger proposed in this packet.
