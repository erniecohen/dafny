# User-defined operators: design and critical review (#194)

**Status:** design proposal, ready for owner review; no implementation in this PR.
**Target:** `review/4.11.0`.
**Source audited:** `62bae981952337d312cbb3d43c39f9813cca6084`.
**Publication-time delta checked:** `a5a664d969e07856a0bb83b38a694c2cf3e827c7`
adds #236's synonym/abstract-type refinement option; its operator interaction is
covered in 5.6 without changing the pinned source references below.
**Brief:** [issue #194, requested work](https://github.com/erniecohen/dafny/issues/194#issuecomment-6757455420).
**Authorship:** AI-assisted, Astra Pro; separate parser, resolver, and soundness reviews were reconciled into this document.

This document specifies the surface language, resolution, AST, implementation
boundaries, tests, and PR sequence. The critical review is in section 13, with
alternatives also evaluated at their decision points. Paths refer to the audited
commit, not to a presumed future state of the branch. `AGENTS.md` and `REVIEW.md`
govern later implementation; this documentation-only change changes no language,
verifier, compiler, test baseline, or shipped product version.

## 1. Recommendation and one necessary qualification of the brief

Implement operators as **notation for existing calls**, behind
`--user-defined-operators`, default false. Declare them with attributes on ordinary
named members. Preserve the source notation in a concrete-syntax AST wrapper and
resolve that wrapper to an ordinary function call, an ordinary permitted method
call, or an intrinsic operator. Do not introduce an overloaded opcode into the
semantic `BinaryExpr` representation. All contracts, frames, ghost restrictions,
termination checks, compilation behavior, and proof costs come from the selected
ordinary call.

There is a real inconsistency in the requested requirements. Unqualified
"built-in first whenever applicable" makes user `==` unreachable. It also hides
user arithmetic on numeric newtypes and user `<`/`>` on inductive datatypes,
where Dafny already supplies rank comparison. An equality-only exception does
not solve the whole problem. The existing cases are in
`Source/DafnyCore/Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs`,
`ResolveExpression`, and `Source/DafnyCore/Resolver/ModuleResolver.cs`, `ResolveOp`.

**Recommended qualification:** keep built-in-first for ordinary operator
declarations, and let a declaration explicitly request priority over a built-in:

```dafny
function {:operator "+"} plus(other: Rat): Rat
predicate {:operator "==", "override"} eq(other: Rat)
predicate {:operator "<", "override"} less(other: Rat)
```

The second string is a lookup-priority instruction, not a new semantic assumption
and not Dafny's member-override mechanism. It may also be used on a trait whose
current type has no corresponding intrinsic operator, so implementers inherit a
stable policy. This is an explicit amendment to the absolute built-in-first
wording, proposed here for design approval, not an unnoticed reinterpretation of
that wording.

The main decisions are:

| Question | Recommendation |
|---|---|
| Declaration | `{:operator "symbol"}` or `{:operator "symbol", "override"}` on a named member |
| Scope | Operators owned by the static left-operand type, or unary operand type; no imported extension-operator search |
| Instance/static | Both; instance slots inherit, static symbolic slots belong to their declaring owner |
| Equality | User `==` on all eligible nominal user types; it means an ordinary Boolean call |
| Derived symbols | `!=` is negation of the selected `==`; `>` and `>=` are separately declared |
| Built-in escape | Contextual `builtin.==(a, b)`, `builtin.+(a, b)`, `builtin.-(a)`, etc. |
| Chains | Existing syntax and conjunction of adjacent comparisons; no transitivity inference |
| Compound assignment | `+= -= *= /= %=`; capture the lvalue location and old value once |
| Methods | Arithmetic operators only, in existing whole-RHS method-call contexts; comparisons are functions/predicates |
| AST | Source wrapper with an ordinary resolved call; generated intrinsic operations never perform overload lookup |
| Laws | Ordinary library traits and verified lemmas, with no compiler recognition of names such as `Ordered` |

This design is intentionally smaller than general overload resolution: one
operator slot per symbol and effective arity in a nominal view, no result-type
overloading, no user conversions, no new literal conversions, and no generic
operator methods beyond the type parameters of their owner. These restrictions
make lookup predictable and leave ordinary named functions available for more
specialized operations.

## 2. Declaration syntax and eligibility

### 2.1 Attribute grammar

The following describes accepted attribute values, not new Coco/R terminals:

```ebnf
OperatorAttribute = "{:" identifier("operator") StringLiteral
                    [ "," StringLiteral("override") ] "}" ;
```

The enabled resolver requires one literal symbol string, optionally followed by
the literal string `"override"`. Nonliteral arguments, extra arguments, repeated
operator attributes on a member, and unknown symbols/modes are errors. In
disabled mode the attribute remains an ordinary previously unknown attribute:
do not validate, consume, rewrite, suppress, or warn about it specially. Rejecting
it when disabled would itself violate the compatibility requirement.

Examples:

```dafny
datatype Vec = Vec(x: int, y: int) {
  function {:operator "+"} plus(b: Vec): Vec { Vec(x + b.x, y + b.y) }
  function {:operator "-"} neg(): Vec { Vec(-x, -y) }
  function {:operator "*"} scale(k: int): Vec { Vec(x * k, y * k) }
}

newtype Word = int {
  static function {:operator "+", "override"} add(a: Word, b: Word): Word {
    ((a as int) + (b as int)) as Word
  }
}
```

The examples are surface-language examples, not claims that this feature already
exists. Existing conversions retain their usual checks; a bounded newtype would
need an appropriate contract, computation, or proof before the final conversion.

### 2.2 Members and arity

Operators can be declared in classes, inductive datatypes, codatatypes, newtypes,
and traits wherever the corresponding ordinary member is legal. They cannot be
declared on built-in types or as unowned module-level extension operators.
Subset types and ordinary type synonyms have no member bodies and introduce no
operator slots; see section 5.

Do not confuse that source-declaration restriction with internal opaque views:
`AST/Modules/ScopeCloner.cs` represents unrevealed supported owners as
`AbstractTypeDecl` with their exposed members. Those views must retain operator
lookup through the exposed signature. This increment does not add operator
declarations directly to source-written standalone abstract types; ordinary
trait-provided/view-carried slots remain available where their named members are.

| Source member | Unary `-` | Binary operator |
|---|---:|---:|
| Instance function/predicate/method | 0 explicit operands | 1 explicit operand |
| Static function/predicate/method | 1 explicit operand | 2 explicit operands |

An instance receiver is the first operand. A static operator's first formal must
be its declaring nominal owner with the owner's type parameters, after expanding
transparent synonyms/subsets. For a reference owner it may be the nullable owner
type. The other operand and arithmetic result need not have the owner's type;
`Vec * int -> Vec` and `Vec * Vec -> int` are individually expressible, but both
cannot occupy the same owner's binary `*` slot. Use a named function for the other
operation. Enclosing-type parameters are substituted by ordinary member lookup.

Require positional operand formals without default values or `nameonly` formals.
For this increment, reject operator-member type parameters: supporting them later
requires a deliberate inference design, not a search over return types. Ordinary
ghost formals are legal where the existing callable rules allow them; they do not
make an operator usable in a compiled context that rejects its explicit-call
form. Ordinary functions, predicates, opaque functions, ghost functions, and
function-by-method declarations are eligible. Arithmetic methods must have
exactly one result and otherwise obey the ordinary method-call rules. Constructors,
lemmas, iterators, extreme predicates/lemmas, and two-state declarations use their
named syntax in this increment.

### 2.3 Symbol set and derived operations

| Symbol | Declared operation | Restrictions |
|---|---|---|
| `+ - * / %` | Binary arithmetic | Function or single-result method |
| `-` | Unary negation | Distinguished from subtraction by effective arity |
| `< <= > >=` | Binary comparison | Function/predicate returning `bool` |
| `==` | Binary relation | Function/predicate returning `bool`; require `"override"` |
| `!=` | `!(selected equality call)` | No separate declaration |
| `+= -= *= /= %=` | Corresponding binary operator followed by assignment | No separate declaration |

Neither order duality nor consistency among separately declared comparisons is a
language law. A library may implement `greater(b)` as `b.less(this)` and prove the
desired relationship. This explicit delegation retains ordinary argument order,
preconditions, dispatch, and left-operand ownership. Automatic reversal would
require a second lookup rule on the right operand and careful evaluation-order
handling; it is not necessary for the requested feature.

Deriving `!=` is useful because it needs only Boolean negation, not an equality
law, and keeps a familiar pair of symbols coherent. Select `==` once, then negate
its result; do not independently look up `!=` or expand into intrinsic equality.
An equality declaration without `"override"` is diagnosed because logical
intrinsic equality would always take precedence. For other operators a fallback
declaration may legitimately be useful through a trait view even when shadowed
by an intrinsic at a concrete type, so do not reject it merely for that reason.

Logical connectives, implication, membership, disjointness, indexing, shifts,
bitwise operations, cardinality, type tests/casts, and prefix equality `==#[k]`
remain intrinsic. This feature adds no literal, indexing, conversion, or
short-circuit operator overloading.

### 2.4 Why an attribute rather than dedicated syntax

**Attribute advantages:** no declaration grammar or token changes; the existing
member has a stable callable name for proofs, exports, documentation, explicit
calls, and disambiguation; ordinary function/method distinctions remain visible.
`Dafny.atg` already parses attributes in `FunctionDecl` and `MethodDecl`, and
`ClassMemberDecl`/`TypeMembers` already handle the requested owners.

**Attribute disadvantages:** string-valued metadata is less prominent than an
`operator` declaration, typos require dedicated diagnostics, and old compilers
ignore the metadata. The last behavior is required in disabled mode, not a reason
to change it. Feature-aware IDE hovers and declaration checks make enabled use
clear.

**Dedicated `operator *(...)` syntax:** more immediately recognizable, but it
needs a declaration production, a decision about callable names and visibility,
and potentially a new reserved word. Coco/R reserves alphabetic literal strings
in the grammar regardless of an option guarding their later use; `Dafny.atg`
documents this near its token declarations. A contextual implementation is
possible, but gives no necessary capability beyond the attribute. Prefer the
attribute for this syntax-first increment.

## 3. Uses, precedence, chains, and method contexts

### 3.1 Existing expression grammar

Retain the existing precedence and associativity of `RelationalExpression`,
`ShiftTerm`, `Term`/`AddOp`, `Factor`/`MulOp`, and `UnaryExpression` in
`Source/DafnyCore/Dafny.atg`. Thus:

```dafny
a * b + c <= d
```

has the same parse tree shape as today, and for the rational declarations below
resolves to:

```dafny
a.times(b).plus(c).le(d)
```

Unary `-a` resolves as a unary slot; do not first rewrite it to `0 - a`. Preserve
the existing special handling of negative numeric literals on the intrinsic
path. `AST/Expressions/LiteralExpr.cs`, `NegationExpression`, and both inference
completion visitors currently participate in that handling.

### 3.2 Chaining is conjunction, not calculation

Preserve today's chain grammar, including its ascending/descending restrictions,
at most one `!=`, and rules for prefix equality and disjointness. Resolve each
ordinary eligible edge separately:

```dafny
a < b <= c   // a.less(b) && b.le(c), if these slots are selected
```

Keep the existing short-circuit conjunction semantics. The second edge's call
precondition may use the fact established by the first edge. There is no new
guarantee to evaluate the middle expression only once: the reference expansion
is the same conjunction of adjacent comparisons as the current chain expansion.
Do not reorder operands or infer a relation between the endpoints. Methods are
not eligible comparison operators, so chains introduce no method effects.

`AST/Expressions/Operators/ChainingExpression.cs::ComputeDesugaring` constructs
its own `BinaryExpr` nodes. Enabled source-chain edges must use the new source
operator factory; wrapping only operators constructed directly in `Dafny.atg`
would miss them. The conjunctions and disjoint-chain helper unions are still
generated intrinsics. Preserve the source/generated distinction in clones.

### 3.3 Ordinary method-call restrictions remain

For a unique selected arithmetic method, these are permitted only where an
explicit single-result method call is currently permitted:

```dafny
z := a * b;
var z := a * b;
a *= b;
```

These are rejected when `*` is a method:

```dafny
z := a * b + c;
assert a * b == c;
predicate p() { a * b == c }
```

There is no automatic lifting of effects out of an expression tree. A function
with a by-method implementation supports the full expression syntax using the
existing verification/compilation rules. Method-backed comparisons are excluded
because chains, derived `!=`, and ordinary Boolean use would otherwise require
additional state/evaluation rules. Select the operator slot before checking its
call kind; do not choose a different slot because one context permits methods
and another does not.

In the legacy resolver, `AST/Statements/Assignment/AssignStatement.cs::Resolve`
currently recognizes method RHSs specifically through `ApplySuffix` and
`MethodCallInformation`. The refreshed equivalents are in
`Resolver/PreType/PreTypeResolve.Statements.cs`. Extend this existing route for a
whole-RHS operator wrapper; do not construct a `FunctionCallExpr` naming a method.

## 4. Naming the intrinsic operator

### 4.1 Syntax and meaning

Use an option-gated contextual form:

```dafny
builtin.==(a, b)
builtin.!=(a, b)
builtin.+(a, b)
builtin.-(a)
builtin.-(a, b)
builtin.<(a, b)
builtin.<=(a, b)
```

The same form covers the remaining supported arithmetic/comparison symbols. It
selects the **existing intrinsic interpretation for the supplied operand types**.
It is not module or member lookup, cannot be shadowed, and is not a first-class
operator value. A lambda can name it as a value when needed. `builtin.f` and an
identifier named `builtin` retain their ordinary meaning. Only the exact
previously invalid `builtin . operator (` form is special when enabled.

The outer operation is forced intrinsic; arguments resolve normally. The escape
does not unwrap an opaque type, waive equality-support restrictions, reveal a
body, check a different heap, or remove a newtype/subset obligation. For arithmetic
on an accessible underlying numeric type, write the existing conversions:

```dafny
(a as int) + (b as int)
builtin.+(a as int, b as int)
```

`builtin.+(a, b)` with `a,b:N` retains the existing intrinsic operation on `N`,
including its result typing and restrictions; it does not promise an `int`
result. Class null checks can use `builtin.==(c, null)`. An instance user equality
has an ordinary non-null receiver obligation. To define a null-tolerant user
relation, declare a static equality on `C` whose operands are `C?` and implement
its null behavior explicitly with the escape.

### 4.2 Alternatives

| Alternative | For | Against |
|---|---|---|
| `===` / `!==` | Short and familiar identity notation | Solves only equality; new tokens can change disabled lexing; needs new precedence/chaining decisions |
| `builtin.==(a,b)` | One uniform facility, ordinary argument grouping, uses existing tokens | Verbose; requires a clearly documented contextual parser hook |
| `a ==@builtin b` | Preserves infix appearance | Interacts with `@` labels, precedence, chaining, and error recovery; harder to extend uniformly |
| Casts alone | Already available for accessible newtype representations | Cannot select intrinsic class/datatype equality or preserve opaque interfaces by themselves |
| Expression attribute | Potentially reuses attribute vocabulary | Dafny has no general attribute-on-arbitrary-expression form; placement and scope need another language design |

The qualified-looking contextual form wins on coverage and compatibility. It is
introduced before any override support, so there is never a feature stage that
can hide the only spelling of an intrinsic operation.

## 5. Resolution, ownership, inheritance, and type views

### 5.1 Operator slots

Create a shared validated descriptor for each annotated callable:

```text
OperatorKey       = (symbol, unary-or-binary)
OperatorDecl      = (key, priority, declaring member, original override slot,
                    instance/static, function/method)
OperatorBinding   = (descriptor, selected member, receiver view,
                    enclosing-type substitution, source origin)
```

Build effective instance slots from the ordinary resolved member table, after
member signatures and inheritance are available. Reuse
`ModuleResolver.RegisterInheritedMembers`, `AddToInheritedMembers`, and
`ProcessInheritedTraitMembers`; a scan only of the declaration's own member list
is insufficient. Deduplicate an identical slot inherited through a diamond and
substitute inherited generic arguments exactly as named member calls do.

An ordinary override inherits the parent's operator symbol, arity, and priority
even if it does not repeat the attribute. An explicit repeated attribute must
agree. Two unrelated slots with the same key are an ambiguity, even if they have
different names, argument types, or one is a function and one a method. An
unrelated new declaration does not hide a slot just by choosing the same symbol.
Existing restrictions on overriding defined or static members remain.

Static operators are ordinary helpers, not abstract operations that implementers
must override. For symbolic lookup, index a static slot only at its declaring
nominal owner. Its named member may remain accessible through ordinary static
inheritance, but symbolic inheritance must not silently rebind its first formal
from a base type to a newtype or an implementing type. A trait's static operator
can be used when the left operand has that trait view. Instance operators are
the mechanism for proof-carrying trait interfaces.

Operator metadata is part of the callable's visible signature, not its body.
An opaque/provided callable can expose it without revealing implementation.
Ordinary exports still control whether the type and member are nameable. Filter
by the current export/visibility view before reporting ambiguity. Do not scan
hidden declarations or unwrap hidden representations. Cache raw descriptors;
cache instantiated lookup only with its type substitution, visibility scope,
and feature setting. Preserve selected bindings in resolved refinement/import
clones and serialized artifacts, just as for named calls.

### 5.2 Selection rule

For enabled source `a op b` (or unary `op a`):

1. Resolve operand expressions without prematurely imposing intrinsic numeric,
   equality, or rank constraints. Determine the nominal owner from the static
   left operand, or the unary operand. Expand transparent aliases/subsets,
   preserve newtype identity, and map a class's non-null view to its declaration.
2. Obtain the unique visible slot, if any, at that view. A type parameter obtains
   instance slots from its explicit instantiated trait bounds; see 5.4.
3. If that slot has `"override"`, select it. Then perform ordinary call checking.
   A bad argument, wrong result context, missing precondition, inaccessible
   receiver, ghost use, or forbidden method context is an error, never a reason
   to fall back to an intrinsic.
4. Otherwise, if the existing intrinsic operation is type-applicable, take the
   existing intrinsic resolution path.
5. Otherwise select the fallback slot, if present, and perform ordinary call
   checking. If absent, report the missing operator.
6. Record the binding and resolved call before all analyses that consume semantic
   operator kinds or call edges. Never redo selection during code generation or
   generic specialization.

Normalize `!=` to the equality key for this decision, then negate the chosen
Boolean result. Do not define `>`/`>=` by swapping operands. No module imports,
right-operand members, solver proofs, expected result types, or arbitrary implicit
conversions participate in candidate discovery. Ordinary subset inclusion and
declared trait upcasts are ordinary call compatibility, not a new conversion
overload system. Numeric literals keep existing Dafny inference; `0 + a` does not
invent a conversion from `0` to a user numeric type.

Intrinsic applicability is a type question, not a test of whether its verification
obligations happen to be provable. Division by zero does not trigger fallback.
Logical equality's existence is also distinct from support for compiled equality:
a codatatype or non-reference trait does not select a fallback equality merely
because its intrinsic equality cannot be compiled. Require the explicit equality
override uniformly in ghost and compiled contexts.

Deferred inference must not guess a nominal owner. Resolve pending operator
constraints to a fixed point using ordinary independent type evidence. Primitive
expressions and numeric-literal defaults can use the existing intrinsic solver
when no nominal choice is pending. If a nominal receiver remains underdetermined,
ask for an operand type annotation or explicit named call; do not search right-hand
operator declarations or choose by solver iteration order. An untyped left-hand
`null` needs sufficient ordinary type evidence or a cast such as `null as C?` to
select a nullable static user slot. `builtin.==(null, c)` is always an unambiguous
request for the existing null comparison. Tests must exercise late type evidence
and ensure a committed intrinsic is not silently rebound to a user slot.

### 5.3 Newtypes and subsets

`AST/TypeDeclarations/NewtypeDecl.cs` is a member-bearing nominal declaration.
`SubsetTypeDecl.cs` is a type synonym with a constraint, not a new member owner.
Therefore a visible subset `S = x:T | p(x)` uses `T`'s operator view; a subset of
`N` stops at nominal newtype `N`. An operator result keeps the declared result
type. It does not automatically become `S` or acquire `p`.

General newtypes keep their nominal head during selection. Do not call
`NormalizeToAncestorType` or `UrAncestor` before finding the owner. Inherited
instance members use the existing general-newtype member machinery and retain
their original substituted argument/result types. There is no automatic lifting
of a base result into the newtype. An explicit conversion back to a constrained
newtype must prove its constraint; assignment to a subset result does likewise.

Respect `Types.cs::NormalizeExpand` and `NewtypeDecl.RhsWithArgument` visibility
behavior. The escape cannot expose an unavailable underlying type. Ordinary
`--general-newtypes` and `--general-traits` support limits continue to apply;
the operator option never silently enables another option or completes an
unsupported backend feature. In particular general newtypes currently require
the refreshed resolver, and `--general-traits=full` still advertises incomplete
support. Tests must distinguish those pre-existing restrictions from a missing
operator feature.

### 5.4 Trait bounds: an explicit prerequisite, not existing functionality

The grammar already accepts `T extends Ordered<T>` and repeated `extends` bounds.
However, both current member lookups expect a `TopLevelDeclWithMembers`;
`TypeParameter` is not one. Existing tests explicitly require a cast before
`x.f` when `x:T extends Trait`. The legacy resolver also rejects several declared
type-parameter-to-trait and concrete datatype-to-trait views that the refreshed
resolver accepts. Merely attaching a trait `Function` to a call with a raw `T`
receiver can crash `Types.cs::AsParentType`.

The trait milestone must therefore implement the following small, explicit
piece of resolution support, not claim to reuse functionality that is absent:

1. Enumerate instantiated declared trait bounds and inherited instance slots.
2. Deduplicate the same slot and reject unrelated competing slots. Do not let
   bound order select the winner.
3. Create an ordinary bound-typed receiver view before normal member/call
   resolution, retaining the original expression for all well-formedness checks.
4. With the feature enabled, teach the legacy assignability/conversion/comparability
   path the **declared** trait-view relationships needed for that expansion:
   concrete datatype-to-instantiated-parent and type-parameter-to-instantiated-bound.
   No arbitrary downcast or unchecked type assertion is introduced.
5. Permit the corresponding explicit named-call expansion with that view in the
   same enabled mode, so the equivalence is reviewable. Preserve all legacy
   behavior when the option is off.
6. Reuse existing type-bound membership facts, verification conversions, parent
   substitutions, and backend upcasts. Test both resolver results against the
   explicitly cast named-call form before declaring trait support complete.

The source locations and existing tests are listed in section 11. This prerequisite
is a high-risk part of the implementation, and deserves its own PR. It is still
ordinary existing trait-call semantics; it adds no algebraic or operator axiom.

An unbounded `T(==)` continues to select intrinsic equality, even when later
instantiated with `Rat`. A parameter bounded by a trait with an equality override
can select that trait slot. These choices are fixed when the generic body is
resolved. Implementation-specific operators never appear retroactively through
specialization. Dispatch of a selected instance slot remains ordinary virtual
trait dispatch; static type selection does not mean devirtualizing its body.

### 5.5 Diagnostics

Use new stable diagnostic identifiers, in enabled paths only. Examples:

```text
operator '*' is not defined for left operand type 'Rat'
operator '+' has ambiguous inherited slots: A.plus and B.add
operator '==' requires {:operator "==", "override"}; intrinsic equality otherwise wins
operator '<=' must return bool
operator '-' expects zero instance operands (unary) or one (binary)
operator '*' is a method and cannot be used inside an expression; use a named call first
operator receiver type is not determined; annotate the left operand or use a named call
operator override of Ordered.le must preserve symbol '<=' and priority 'override'
```

Wrong-argument and contract failures should otherwise reuse ordinary call
diagnostics at the operator token, with the selected member's declaration as
related information. `--show-hints`/hover may explain that a fallback declaration
lost to intrinsic newtype arithmetic or datatype rank. Do not silently select an
unintended operator to avoid an error. When the option is off, retain all existing
diagnostic IDs, text, locations, ordering, and exit codes.

### 5.6 Refinement and source clones must preserve the original choice

Freezing choices only in ordinary resolved AST clones is insufficient.
`Rewriters/RefinementTransformer.cs::RefinementCloner` uses `base(false, false)`:
it deliberately copies source while discarding normal resolved fields. Scope and
export clones also reconstruct source views. An inherited expression must not
choose a new operator just because a refining module adds an override or makes
an intrinsic operation newly applicable.

For example, a base module can prove a lemma with postcondition `c == c` for its
class `C`, using intrinsic reflexivity. A refining module may add a user equality
on `C` that always returns false. Re-resolving the inherited postcondition as that
new call would change a previously proved lemma into a lemma ensuring false,
while inherited verification conditions can be skipped. The risk exists without
any source-level abstract-type operator declarations.

Give every resolved source operator an immutable **selection record**, distinct
from `ResolvedExpression`: intrinsic-only, or a selected member/slot with its
static/instance form and necessary receiver/type-view information. Transport this
record through semantic source clones even when `CloneResolvedFields` is false.
During refinement:

1. Copy records for all inherited operator occurrences, including intrinsic ones.
2. After the refining module's signatures/inheritance are available, map selected
   members through the checked declaration correspondence, including
   `MemberDecl.RefinementBase` links established by `MergeClass`.
3. Resolve an inherited intrinsic through the intrinsic-only path. Resolve an
   inherited member selection as a normal call to its corresponding member,
   with all ordinary receiver/argument/type checks. Never repeat priority lookup.
4. Reject a selection that cannot be transported; do not replace it with another
   available member or intrinsic. Preserve the symbol/arity/priority of refined
   member signatures as for inherited trait slots.
5. Resolve newly authored expressions in the refining module normally; they can
   use a newly added override even though inherited expressions cannot change.

This does not require a preliminary whole-program resolution pass.
`Resolver/ProgramResolver.cs` resolves dependency-ordered modules, and
`RefinementTransformer.PreResolve` receives an already resolved base. The
selection records therefore exist before its source is cloned. Keep the records
separate from mutable solver types and transport substitutions using the normal
refinement mappings. The record is a replay recipe, not a saved resolved AST:
do not retain old local-variable/type-parameter pointers or enable wholesale
`CloneResolvedFields` where an abstract/signature clone deliberately needs
rebinding. Freeze the slot and view, while allowing its corresponding refining
member and ordinary instance dispatch to supply the implementation. Use general
clone/member correspondence, not cardinality-specific provenance as an operator
mapping API.

Store records per edge in `ChainingExpression`, whose clone currently recomputes
its desugaring, and on a compound statement's underlying operation. Records only
on transient generated edge/call nodes would be lost. Apply the same discipline
to `ScopeCloner` and export-consistency clones. An exported contract whose pinned
callee or receiver view is unavailable must fail the ordinary consistency check,
not acquire a different interpretation through fallback lookup. Preserve the
origin-owner/member mapping through generated `AbstractTypeDecl` views.

The current `Backends/Library/LibraryBackend.cs` serializes
`Program.AfterParsingClone`, retaining original module/refinement source. With
matching mode, reloading can reconstruct choices by resolving the base first and
then transporting them again. Test this exact round trip. Any serialization path
that flattens inherited source must instead preserve the selection records or
emit an explicit named call/intrinsic escape; printing bare infix syntax without
its original provenance is insufficient.

The publication-time #236 change in `RefinementTransformer` merges an abstract
type's members into a refining synonym's target. That is another checked member
correspondence across which the same metadata/selection rules apply; it does not
authorize new subset/alias-owned operators or a change to an inherited meaning.

## 6. Equality and the boundary of intrinsic reasoning

### 6.1 What a user `==` means

For an explicit override, `a == b` means exactly `a.eq(b)` or its selected static
call. It may be non-reflexive, non-transitive, heap-dependent, partial through a
precondition, or unsuitable for hashing. None of those properties is a language
error beyond the existing callable restrictions. The definition, contract, or
explicitly called library lemmas determine what can be proved.

Intrinsic equality remains substitutive. A user `==` supplies **no automatic
substitution**, no quotient values, and no replacement of representation identity.
Equivalence alone does not imply congruence for every function: parity equality
does not preserve the integer identity function as intrinsic equality, and
mathematical rational equality does not preserve numerator/denominator accessors.
An opaque representation does not remove this distinction.

Allow equality notation for all eligible nominal types, rather than classes only.
Classes-only is familiar for value-versus-reference comparison but supplies no
extra soundness: class predicates can be nontransitive and mutable too, while
datatypes are the principal numeric-library use case. Disallowing user equality
entirely preserves a simpler reading of `==`, but unnecessarily rejects the
requested notation once intrinsic escape and honest call lowering are present.
This recommendation depends on the AST and intrinsic-context rules below, not on
proving an unspecified global congruence condition.

### 6.2 Intrinsic mechanisms and nested source expressions

Do not say that every operator occurring syntactically under `old`, a frame, or
a trigger becomes intrinsic. The right distinction is between the mechanism's
own generated operations and ordinary source expressions nested in it.

| Construct | Operations that remain intrinsic | Source expressions inside it |
|---|---|---|
| Pattern matching | Constructor/discriminator tests, literal-pattern comparisons, exhaustiveness, generated discrimination equalities | Scrutinees, guards, branch expressions resolve normally |
| Sets, multisets, maps, sequences | Element/key identity, membership, multiplicity, extensional collection equality, map lookup/update uniqueness, runtime hashing | Element expressions, predicates, key/value computations resolve normally |
| `T(==)` | Existing compiled-equality characteristic; no new equality-support inference | An explicitly trait-bounded operator call is a separate callable capability |
| `fresh` / `allocated` | Allocation state and reference identity | Expressions producing their arguments use normal calls |
| `old` / `old@L` | Existing heap selection, not an equality operation | `old(a == b)` contains the selected `eq` call in the old heap, with ordinary reads/preconditions |
| `unchanged` | Generated field-value equalities and frame membership | Expressions denoting the frame resolve normally |
| `reads` / `modifies` | Object/field identity and frame-set membership | Frame-producing expressions resolve normally |
| `decreases` and automatic termination machinery | The existing well-founded orders on measure values, including datatype rank | An explicitly written operator within a measure computes a value; it does not redefine the order used to compare measures |
| Triggers | Generated intrinsic terms retain their semantics | An explicit operator call becomes the same function application as the named form; do not retain fake equality/arithmetic trigger kinds |
| Generated/compiled equality | Datatype equality, collection equality, generic equality, null/identity tests, hashing | A selected user override compiles as its ordinary callable |
| `calc` connectors | Default/explicit step relations and their built-in composition rules | Ordinary expressions in lines and hints may contain user calls |
| Prefix equality and coinduction | `==#[k]`, codatatype equality machinery, generated induction/coinduction conditions | A user equality function is analyzed as a function, without coinductive-equality privileges |
| Generated proof statements | Equalities introduced by assignments, `forall`, matching, refinement, frame checks, and compiler rewrites | Already resolved user subexpressions retain their selected calls |

"Intrinsic equality" means the type's existing Dafny equality, not a new universal
shallow field comparison. Classes use identity; datatypes, collections, and
codatatypes have their existing structural/extensional/coinductive treatments
and existing compilability restrictions. `Verifier/Expressions/TranslateBinaryExpr.cs`
and `AST/Types/UserDefinedType.cs::SupportsEquality` contain important distinctions.

### 6.3 Why `calc` needs an explicit rule

`AST/Statements/CalcStmt.cs::BinaryCalcOp.ResultOp` encodes fixed composition
rules. `Verifier/Statements/BoogieGenerator.TrStatement.cs::TrCalcStmt` proves
adjacent steps and then assumes the endpoint relation. It checks the line
expressions, not the arbitrary preconditions of a newly introduced operator call.
Replacing a connector by a user relation would therefore import unproved
transitivity and can also skip a precondition.

For example, a relation that means "the two integer representations differ by at
most one" holds for 0/1 and 1/2 but not 0/2. It cannot use the existing `calc ==`
rule. Keep connectors intrinsic in this feature, including default equality;
report the ordinary intrinsic type error if a connector is unavailable, such as
`calc <=` on a rational datatype. Explain the intrinsic connector in hover/docs.
Users can call the trait's transitivity lemma, or calculate over Boolean
propositions containing ordinary user comparisons. A future calculation syntax
that takes explicit law evidence is a separate feature.

## 7. Compound assignment

The accepted forms are `lhs += e;`, `-=`, `*=`, `/=`, and `%=`. They have no
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
- an immutable selection record that survives refinement/export source clones,
  independently of whether ordinary resolved fields are copied;
- an ordinary `ResolvedExpression`, or the existing method-RHS information while
  resolving a permitted statement context.

`AST/Expressions/ConcreteSyntaxExpression.cs` and `Expression.Resolved` already
support this distinction. After successful expression resolution, the wrapper
contains an intrinsic expression, `FunctionCallExpr`, or Boolean negation of an
equality call. A statement-only method use instead becomes ordinary resolved
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

### 8.2 Disabled parsing must remain byte-identical, including errors

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

For every accepted enabled program, erase source wrappers and expand compounds
and derived `!=`. The resulting program uses existing primitive operators and
ordinary calls. For functions, the call has the same selected member, receiver,
arguments, instantiated types, visibility, ghostness, heap context, and source
well-formedness obligations as the explicitly written call. For methods, the
expansion has only currently permitted method statements. For compounds, the
explicit temporaries fix the storage location and evaluation order.

The verifier must see this expansion before call-graph construction, bounds
discovery, ghost checking, co-call/extreme-predicate analysis, decreases inference,
trigger collection, and translation. In particular:

- `BoundsDiscovery.cs` must never see user equality as `EqCommon`, since it uses
  intrinsic equality to construct an exact bounded pool.
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

## 10. Worked library example: Ordered and rational values

The example below uses an ordinary self-typed trait. `self` is a ghost function
whose intrinsic-identity contract connects its `T` result to the receiver; it is
not a language-provided self type. This avoids pretending that static trait
helpers are abstract instance obligations. The trait states equality equivalence,
order congruence, reflexivity, antisymmetry, and transitivity as library lemmas.
No law is recognized by the operator implementation.

`Rat` uses noncanonical positive-denominator representations. Consequently the
order is a preorder on representations and a partial order modulo its declared
equivalence; antisymmetry concludes `eq`, **not structural equality**. The extra
congruence lemmas make the order well-defined on those equivalence classes. They
do not make numerator/denominator accessors congruent, or turn ordinary sets/maps
into quotient collections. A library needing structural antisymmetry must use a
canonical representation and prove its canonicality.

All declarations are in the same module. Existing trait support requires
`--general-traits=datatype`. The explicit-call baseline was checked using the
refreshed resolver; the legacy trait-view prerequisite in 5.4 must land before
claiming equivalent legacy support. The following is **proposed enabled syntax**;
the checked baseline and its limits are described immediately afterward.

```dafny
type Pos = x: int | 0 < x witness 1

trait Ordered<T extends Ordered<T>> {
  ghost function self(): T
    ensures builtin.==(this, self() as Ordered<T>)

  predicate {:operator "==", "override"} eq(other: T)
  predicate {:operator "<=", "override"} le(other: T)

  lemma eq_reflexive()
    ensures this == self()
  lemma eq_symmetric(other: T)
    requires this == other
    ensures other == self()
  lemma eq_transitive(other: T, last: T)
    requires this == other && other == last
    ensures this == last
  lemma le_congruent(other: T, last: T)
    requires other == last
    ensures (this <= other) == (this <= last)
  lemma le_congruent_left(other: T, last: T)
    requires this == other
    ensures (this <= last) == (other <= last)

  lemma reflexive()
    ensures this <= self()
  lemma antisymmetric(other: T)
    requires this <= other && other <= self()
    ensures this == other
  lemma transitive(other: T, last: T)
    requires this <= other && other <= last
    ensures this <= last
}

datatype Rat extends Ordered<Rat> = Rat(n: int, d: Pos) {
  ghost function self(): Rat
    ensures builtin.==(this, self() as Ordered<Rat>)
  { this }

  // eq and le inherit their operator metadata from Ordered<Rat>.
  predicate eq(other: Rat) { n * other.d == other.n * d }
  predicate le(other: Rat) { n * other.d <= other.n * d }

  function {:operator "*"} times(other: Rat): Rat {
    Rat(n * other.n, d * other.d)
  }
  function {:operator "+"} plus(other: Rat): Rat {
    Rat(n * other.d + other.n * d, d * other.d)
  }

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
  print a * b + c <= d, " ", a == Rat(2, 4), " ",
        builtin.==(a, Rat(2, 4)), "\n";
}
```

The intended output is `true true false`. The last two outputs deliberately
distinguish rational equality from representation equality. Operator bodies
above contain ordinary integer arithmetic/equality; those remain intrinsic.
The trait's Boolean equality between two propositions is also intrinsic.

**Validation performed for this design:** the fully explicit-call expansion of
this example, including all eight law lemmas, was checked with public fork release
[`v4.11.0+fcb2042d.review.9a4451d7`](https://github.com/erniecohen/dafny/releases/tag/v4.11.0%2Bfcb2042d.review.9a4451d7),
Z3 5.1.0, `--general-traits=datatype --type-system-refresh`, with additional axioms
off: **25 verified, 0 errors**. Python compilation/execution succeeded and printed
`true true false`. A separate client that calls `reflexive` and `eq_reflexive` and
then executes `assert false` reported its one expected verification error. The
same explicit-call example under the legacy resolver reported nine existing
trait-view/comparability resolution errors. These are functional checks of the
ordinary-call baseline, not tests of unimplemented operator syntax or a build of
the audited source commit.

The exact checked baseline is included as
[194-ordered-rat-explicit.dfy](examples/194-ordered-rat-explicit.dfy), and its
negative client as
[194-ordered-rat-vacuity.dfy](examples/194-ordered-rat-vacuity.dfy). They are design
examples using existing syntax, not an operator implementation or new integration
suite entries. To reproduce with the cited release, set `Z3_5_1_0` to the Z3
5.1.0 executable and run:

```sh
dafny verify docs/dev/design/examples/194-ordered-rat-explicit.dfy --general-traits=datatype --type-system-refresh --solver-path="$Z3_5_1_0"
dafny run docs/dev/design/examples/194-ordered-rat-explicit.dfy --target:py --general-traits=datatype --type-system-refresh --solver-path="$Z3_5_1_0"
dafny verify docs/dev/design/examples/194-ordered-rat-vacuity.dfy --general-traits=datatype --type-system-refresh --solver-path="$Z3_5_1_0"
```

The last command is expected to exit 4 at `assert false`. In the expansion, a
selected instance operator becomes its named call, with an explicit
`(x as Ordered<T>)` receiver for a type-parameter operand. The two `builtin.==`
identity contracts and final intrinsic comparison become ordinary equality in
the baseline; ordinary integer/Boolean operations stay unchanged. Section 12
requires keeping this reference program alongside the future operator regression.
The proposed declarations/lookup add no new proof step to the law bodies.

Additional usage examples for the eventual test suite are:

```dafny
predicate ordered<T extends Ordered<T>>(a: T, b: T) { a <= b }
// Expands to (a as Ordered<T>).le(b).

type Positive = r: Rat | 0 < r.n witness Rat(1, 1)
// Positive uses Rat's operators. plus still returns Rat;
// assigning it back to Positive must prove positivity.

lemma false_control(a: Rat) {
  a.reflexive();
  a.eq_reflexive();
  assert false;  // must fail
}
```

## 11. Concrete implementation map

### 11.1 Option, artifacts, and source compatibility

Add `CommonOptionBag.UserDefinedOperators` as `Option<bool>` with default false,
and expose it in `DafnyCommands.ParserOptions` so parsing, verification,
compilation, formatting, IDE projects, and `dfyconfig.toml` agree. Project syntax:

```toml
[options]
user-defined-operators = true
```

Proposed help text:

> Enable operator declarations on user-defined types, intrinsic operator calls
> such as builtin.==(a,b), and compound assignments. Operators resolve to ordinary
> member calls. Built-ins take precedence unless the declaration explicitly uses
> the override mode. Disabled by default.

Do not add this option to the module `{:options}` allowlist. A single source
compilation has one setting; source modules cannot locally change the meaning of
an imported callable's operator metadata. Preserve command-line-over-project
precedence, including explicit `--user-defined-operators:false`.

Unlike a cosmetic parser setting, this option affects the interpretation of
serialized library source. `DooFile.cs` serializes the source/pre-resolution form
and resolves it again when loading it. Merely registering the option as `Cli`, or
checking only "library true implies client true", is insufficient.

For the first implementation, require **matching producer/consumer settings**
for `.doo` imports and already-translated source modules. Register a global exact
compatibility check, with missing metadata interpreted as false. Add corresponding
recording/checking in `Options/TranslationRecord.cs` for `.dtr` modules; its current
translation-option scope is separate from the global registry, so this needs an
explicit integration rather than assuming registration covers both formats.
Record the new value when true; omit this new false-valued field on the disabled
path to preserve existing artifact bytes. Existing metadata and
option checks remain untouched.

The artifact gate compares serialized program/interpretation metadata; the
fork's mandatory product-version fields and incidental ZIP-container metadata
are accounted for separately. It does not require identical whole archives from
two differently versioned compiler builds. This qualification does not relax the
required scanner/diagnostic/Boogie compatibility checks.

For `.doo`, reject mode mismatches during manifest validation, before reparsing
the library's source. For precompiled source reuse, make the `.dtr` check at the
reuse boundary: `Backends/ExecutableBackend.cs::ProcessTranslationRecords` currently
reads records only during compilation, after resolution, and record coverage is
optional. Checking `TranslationRecord.Validate` alone cannot protect a module
absent from every record. When this feature is enabled, require matching mode
metadata for **every nonempty reused precompiled module**, and reject missing
records/module coverage before emitting calls to that code. Audit currently
excluded default-module cases: either record an unambiguous module identity or
reject that enabled reuse until coverage can be established. Do not claim this
late check prevents source parsing or checks a verification-only invocation.

Plain trusted `--library` source, and a disabled-mode link with no translation
record, retain their existing caller responsibility for matching an assumed
interface to the actual implementation. The strict OFF policy does not permit
adding a blanket missing-record error to those old invocations. Feature-using
libraries must distribute their `.doo` manifest and/or required `.dtr` metadata;
omitting that metadata is outside the automatically checked reuse route. Supplied
records must check both mismatch directions, even for a disabled consumer. This
distinction is necessary to avoid claiming a guarantee the current raw-source
library interface cannot enforce.

An enabled application using a prebuilt library, including the standard library,
initially needs one produced with the same setting. That rebuild requirement is
a conservative cost of the initial design. A later per-module interpretation
scheme could relax it, but must preserve validated operator metadata and resolved
meanings; it is not part of this feature. Tests cover both mismatch directions,
absent values, absent records/modules, exported attributes, and `.doo`/`.dtr`
round trips. Mode-off loading of existing mode-off libraries retains the previous
diagnostics and behavior, subject to the existing product-version check.

`{:operator}` metadata must remain available to source/serialization printers.
On the enabled path, treat it as front-end metadata rather than emitting an SMT
operator instruction. Review `Verifier/BoogieGenerator.ExpressionTranslator.cs::TrAttributes`
and its callers for whether a feature-gated omission is needed. Compare infix
and named-call programs with identical declarations/attributes. In disabled mode,
unknown attributes must traverse exactly their old path, including printed output.

### 11.2 Parser, AST, and shared declaration work

| File/location | Required change |
|---|---|
| `Source/DafnyCore/Options/CommonOptionBag.cs` | Declare/register the option and document its compatibility policy |
| `Source/DafnyCore/Options/DafnyCommands.cs::ParserOptions` | Make the option available consistently to relevant commands |
| `Source/DafnyCore/DooFile.cs`; `Options/OptionRegistry.cs`; `Options/OptionCompatibility.cs`; `Options/TranslationRecord.cs` | Preserve producer interpretation and reject mode mismatches; test disabled artifact compatibility |
| `Source/DafnyCore/Backends/ExecutableBackend.cs::ProcessTranslationRecords`; `Backends/DafnyExecutableBackend.cs` | Check supplied record modes and require complete reused-module coverage when enabled, before compiled-code reuse; retain the documented raw-library trust boundary |
| `Source/DafnyCore/Dafny.atg` | Guarded semantic hooks in `NameSegment`/`AssignStatement`; enabled source operator construction in existing expression actions; no new terminals/grammar alternatives |
| `Source/DafnyCore/AST/Grammar/ParserNonGeneratedPart.cs` | Contextual escape lookahead/parser and compound lookahead/parser, using existing tokens and expression parsing |
| `Source/DafnyCore/DafnyCore.csproj::RunCoco` | Regenerate and compare generated parser/scanner; normally no build-target behavior change |
| Proposed `Source/DafnyCore/AST/Expressions/Operators/OperatorSyntaxExpr.cs` | Source wrapper, cloning, source operands, resolution/binding metadata, formatting/navigation hooks |
| `Source/DafnyCore/AST/Expressions/Operators/ChainingExpression.cs` | Deliberately wrap enabled source comparison edges; preserve mode/provenance through cloning |
| `Source/DafnyCore/AST/Expressions/LiteralExpr.cs::NegationExpression` | Coordinate unary operator selection with existing intrinsic negative-literal behavior |
| Proposed `Source/DafnyCore/AST/Statements/Assignment/CompoundAssignStmt.cs` | Source statement plus ordinary resolved capture/call/store statements |
| Proposed `Source/DafnyCore/Resolver/OperatorResolution.cs` | Shared descriptor validation, instance-slot inheritance, visibility filtering, static ownership, priority rules, and diagnostics |
| `Source/DafnyCore/AST/Members/MemberDecl.cs` and callable/owner clone paths | Store validated descriptor metadata or equivalent shared resolution data; preserve inherited-slot identity and reset resolution-local caches correctly |
| `Source/DafnyCore/Resolver/ModuleResolver.cs` | Invoke validation after member signatures/inheritance; finalize operator calls before the existing semantic-analysis pipeline |
| `Source/DafnyCore/AST/Cloner.cs`; `AST/Substituter.cs` | Preserve wrapper syntax where needed; substitute the ordinary resolved call without rediscovering a slot |
| `Source/DafnyCore/Rewriters/RefinementTransformer.cs`; `AST/Modules/ScopeCloner.cs`; export-consistency cloning in `Resolver/ModuleResolver.cs` | Preserve intrinsic/member selection records even in source-only clones, map refined slots, keep opaque-view identity, and reject untransportable or invisible selections |
| `Source/DafnyCore/Backends/Library/LibraryBackend.cs` | Retain original module provenance in serialized source and test selection reconstruction through `.doo` round trips |

The proposed file names can follow local conventions, but the separation between
shared policy and resolver-specific constraints must remain. Do not maintain two
different definitions of operator applicability or inheritance. Existing
`ConcreteSyntaxExpression` substitution may intentionally keep only a parenthesized
resolved expression in internal proof clones; that is acceptable semantically.
Source/export clones still need appropriate syntax and metadata for their purpose.

### 11.3 Legacy resolver

In `Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs`:

1. Handle `OperatorSyntaxExpr` before `BinaryExpr` intrinsic constraints. The
   current `Plussable`, `Minusable`, `Mullable`, `RankOrderable`, `Equatable`, and
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

### 11.4 Refreshed resolver

In `Resolver/PreType/PreTypeResolve.Expressions.cs`, handle source wrappers before
`ResolveBinaryExpr` adds family confirmations/default advice. Use
`PreTypeConstraints.cs`'s guarded-constraint infrastructure and receiver
approximation to delay unresolved owners. Share descriptors and selection policy
with the legacy resolver; use the refreshed resolver's normal member/call
resolution to construct the selected application.

Before `UnderspecificationDetector` assigns intrinsic opcodes, each wrapper must
have a chosen expansion or an enabled-mode diagnostic. Update/check
`PreTypeToType.cs` and `TypeRefinementVisitor.cs` so the wrapper and expansion
have consistent result types, including subtype refinements. A result expectation
constrains the selected call; it does not select a different operator. Extend
`PreTypeResolve.Statements.cs::ResolveAssignmentRhs`/`ResolveUpdateStmt` for method
RHS wrappers and compound expansions. Ordinary general-newtype receiver handling
and parent substitutions must remain intact.

### 11.5 Semantic consumers, backends, and editor

The following are an audit checklist of places where the new semantic invariant
must hold, not permission to add a second operator semantics in each consumer:

| Existing path | Required invariant/check |
|---|---|
| `Resolver/CallGraphBuilder.cs`; `Resolver/ModularTermination.cs` | Calls are present early, including trait calls; no lost edges or synthesized termination exemptions |
| `Resolver/BoundsDiscovery/BoundsDiscovery.cs` | User relation never yields intrinsic exact/numeric bounded pools |
| `Resolver/CoCallResolution.cs`; `CollectFriendlyCallsInSpecVisitor.cs`; `Abstemious.cs` | User `==` gets only ordinary-call treatment |
| `AST/ExtremeLemmaSpecificationSubstituter.cs`; `ExtremeLemmaBodyCloner.cs` | No conversion of user equality to coinductive/prefix equality |
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
operator-specific equality hook, the lowering boundary has probably been placed
too late.

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

A concise normative manual statement should say:

> With user-defined operators enabled, an operator use selects a member using the
> static operand type. An explicit operator override has priority; otherwise an
> applicable built-in operator has priority. A selected user operator has exactly
> the checks and meaning of the corresponding named call. User equality does not
> change intrinsic equality, collection identity, equality-support characteristics,
> termination orders, or calculation connectors. The builtin operator form selects
> the existing intrinsic meaning explicitly.

Each later product PR needs the appropriate `docs/dev/news/194.feat` fragment
(or staged descriptive fragment following repository practice), regression and
expectation files, `.github/review/base` and `.github/review/regressions` updates,
and the gate evidence required by `AGENTS.md`/`REVIEW.md`. Do not update expected
verdicts speculatively. Follow the prescribed fail/artifact/reasoned-baseline
procedure if a new test changes a gate inventory. Merge product PRs one at a time
and reconcile the base according to current repository rules. This design PR
does not claim the feature has shipped and needs no product-version change.

## 12. Validation plan and PR sequence

### 12.1 Tests with observable acceptance criteria

Create `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-194.dfy`
as the driver and separate `Inputs/github-issue-194-*` fixtures where expected
failures or option combinations differ. Keep explicit-call reference programs
alongside operator programs. Use the repository's existing per-resolver and
per-compiler harnesses where supported. Add focused AST/parser tests in
`Source/DafnyCore.Test`, `.doo` cases in `DooFileTest.cs`, and editor tests in
`Source/DafnyLanguageServer.Test/Lookup/` and `Refactoring/RenameTest.cs`.

| Area | Required cases and expected result |
|---|---|
| Disabled parser | Valid legacy programs, malformed programs, identifiers `operator`/`builtin`, unknown/malformed operator-looking attributes, `builtin.==`, `+=` and whitespace variants: exact previous tokens, diagnostics, exit codes, printed/resolved output, and Boogie |
| Enabled declarations | All eligible owners; static/instance unary/binary arity; enclosing generic types; invalid symbols, mode/arity/result/default/member-generics; comparisons-as-methods rejected |
| Priority | Fallback rational `+`; numeric-newtype fallback/override `+`; datatype fallback/override `<`; equality requires override; override errors never trigger fallback |
| Types and inference | RHS/result mismatch, mixed operand type, unresolved left type, null in either position with/without annotation, expected result type, nested uses, late type constraints; deterministic result across traversals |
| Precedence and chains | Unary versus binary `-`, mixed arithmetic/comparison, parenthesized forms, ascending/descending chain restrictions; each edge selects independently and second-edge preconditions use short-circuit facts |
| Intrinsic escape | Every supported symbol/arity, malformed calls, nested user calls in operands, casts, identity/null comparison, scope collisions with ordinary `builtin` identifiers |
| Traits | Declared and inherited slots, concrete implementation with omitted annotation, diamond deduplication, unrelated-slot ambiguity, conflicting repeated annotation, opaque/provided exports, missing named member export |
| Bounded types | Direct bound, inherited bound, generic substitution, multiple bounds in permuted order; explicit-call expansion and generated receiver view agree under both resolvers after the parity PR |
| Generic coherence | `T(==)` equality stays intrinsic at an overriding instantiation; bound-based equality remains the chosen trait call; resolved/refinement/library clones never rediscover operators |
| Refinement source clones | A refinement adds a false equality override after an inherited reflexivity lemma: inherited `==` stays intrinsic and `assert false` fails; user-call selections also stay calls when a representation/view changes; new refinement syntax can use the new override; chain/compound selections, opaque exports, synonym-target refinement, and `.doo` round trips preserve these distinctions |
| Newtypes/subsets | Visible/opaque nominal heads, general-newtype inherited instance members, inherited static-name versus symbolic scope, no result lifting; negative subset/newtype result conversion fails |
| Calls and effects | Failing requires, reads, ghostness, receiver checks, wrong result count, nested method rejection, whole-RHS method success, by-method execution; compare to named-call errors |
| Compound assignment | Local, out variable, field, multidimensional array; RHS/address observations evaluated once in specified order; aliased/mutating method; null/bounds/uninitialized/frame/subset failures; invalid LHSs rejected |
| Equality safety | Always-true and non-reflexive `eq`; `a == a` not simplified; user eq does not imply intrinsic equality or accessor substitution; codatatype user eq gets no special coinduction or `T(==)` capability |
| Structural mechanisms | Pattern matching, duplicate map keys, set membership/cardinality, multiset multiplicity, generic compiled equality/hash, `fresh`/`unchanged`/frames remain intrinsic despite user eq |
| `calc` | Nontransitive user eq cannot prove endpoint relation through `calc`; custom comparison preconditions cannot be skipped; primitive connectors on unsupported types fail; Boolean lines containing calls work |
| Bounds and decreases | Parity equality in a comprehension yields no fake exact bound; overloaded `<` yields no fake numeric bounds or automatic decrease; generated rank comparisons remain primitive |
| Heap/proof contexts | `old`, `old@L`, quantifier triggers, comprehensions, lambdas, witnesses, type constraints, refinement; selected calls retain all original receiver/argument obligations |
| Recursive contracts | Operator equality in its own contract cannot bootstrap the unproved contract; retain #166 protections for lets/sequence initializers and #39 receiver checks |
| Termination | Function/method and trait recursion operator/named-call pairs give identical results with modular termination off/on; cross-module rejection/obligations follow the current #211 policy |
| Tooling | Source print/format/parse round trip; diagnostics at symbol; hover/definition/references; renaming `times` changes names but leaves all `*` tokens intact |
| Artifacts | `.doo` matching modes succeed and mismatches reject before reparsing; supplied `.dtr` modes match before compiled-code reuse; enabled reused modules require full record coverage; old absent flag means false; defaults stay compatible; exposed metadata survives serialization |
| Vacuity | After exercising operators, trait laws, chain/compound paths, and intrinsic escape in inhabited contexts, `assert false` still fails; never count a prior resolver failure as a successful vacuity control |

Useful existing fixtures to extend/compare include
`dafny0/BoundedPolymorphismResolution.dfy`, `BoundedPolymorphismVerification.dfy`,
`BoundedPolymorphismCompilation.dfy`, `GeneralNewtypeMemberResolution.dfy`,
`GeneralNewtypeMemberCompile.dfy`, `GeneralNewtypeMemberVerify.dfy`, and the
`traits/` general-trait/export tests. Their current restrictions are evidence to
account for, not tests to rewrite indiscriminately to get a green run.

### 12.2 Compatibility and proof-cost gates

For each product milestone, compare the baseline and candidate with the feature
off, using the same source filenames, solver/build configuration, and deterministic
settings described in `REVIEW.md`. Compare:

1. Scanner/parser tables and a diagnostic corpus including rejected programs.
2. Parsed/resolved source output and literal Boogie output, controlling known
   timestamps/paths outside the language change without normalizing away a semantic
   difference. Also compare generated-name allocation where applicable.
3. The full verifier suite and standard-library verdicts, and resource counts
   under the fork's required comparison profiles. Use its existing deterministic
   literal hashes and restart policy; do not reinterpret known process noise as
   a license for an unmeasured regression.
4. Existing default library import, IDE, formatting, and artifact round trips.

For enabled operator-versus-named-call pairs, require equal verification outcomes,
runtime results, call graph edges, and normal call checks. Compare normalized
resolved AST, Boogie, trigger sets, and resource counts for the rational acceptance
case. If a source origin changes a proof identifier/order, explain that difference
and eliminate unnecessary AST/Boogie changes before accepting proof-cost drift.
The design does not promise unsupported backends or exact timing equivalence.

The shared resolver policy can index declarations in linear time in the number
of visible/effective members; each selected nominal slot has constant-time lookup
after ordinary type/view preparation. Bound lookup is proportional to the explicit
bound slots before deduplication. There is no global imported-function scan and
no exponential candidate backtracking. Deferred type obligations use a worklist
and deterministic progress/error conditions. These are algorithmic expectations,
not measured performance results. With the option off the new index/worklist is
not built at all.

### 12.3 Milestones, each independently reviewable

The order deliberately puts the intrinsic escape before overrides and separates
legacy trait parity from operator mechanics. No product milestone begins until
the owner reviews this design and its priority qualification.

| PR | Content | Completion gate |
|---|---|---|
| D0 (this PR) | Design, alternatives, critical review, checked explicit-call example | Owner decision on the specified policies; no implementation |
| M1 | Option/help/project plumbing; validated declaration descriptors and duplicate diagnostics; artifact mode protection | Declaration/serialization/OFF gates; no silent claim that operator uses work yet |
| M2 | Concrete syntax wrappers, source printing/cloning, contextual intrinsic escape | Intrinsic-only parse/resolve/verify/compile pairs, both resolvers; scanner/error tables unchanged OFF |
| M3 | Shared lookup and both resolver paths for ordinary function arithmetic/comparison, unary and chains; concrete instance and directly owned static slots; source-clone selection transport; safe symbolic-reference/rename handling | Rational arithmetic against named calls; failing-call and refinement/export tests; no operator opcode reaches semantic passes; member rename never edits operator tokens |
| M4 | Instance-trait metadata inheritance and ordinary explicit trait-receiver dispatch | Override/diamond/export/refinement tests; exact call graph/termination behavior |
| M5 | Legacy declared-trait-view/comparability parity; bound-aware operator lookup in both resolvers | Checked Ordered/Rat expansion supported under both resolvers; generic/bound ambiguity and runtime tests |
| M6 | Compound statements and single-result arithmetic method RHSs | Capture/evaluation/frame/alias tests; negative nested-effect cases; normal method verification |
| M7 | Explicit override priority on colliding intrinsics, user `==` and derived `!=` | Intrinsic-context safety matrix, inherited-reflexivity negative, `calc`/bounds/coinduction/self-contract negatives, vacuity and named-call equivalence |
| M8 | Final editor navigation/reference/rename integration, full acceptance matrix and manual examples | Verified/compiled infix Ordered/Rat program; library mode tests; complete OFF neutrality evidence |

Source-preserving AST/formatting support belongs with M2, and basic diagnostic
origins belong with every milestone. Nontextual reference metadata and rename
filtering must accompany the first operator-backed call in M3, because ordinary
`FunctionCallExpr` already exposes references; until that is safe, suppress its
symbolic reference rather than allow corrupting rename edits. M8 completes IDE
behavior and does not defer that safety requirement. Every stage M1–M6 must reject
enabled `"override"` declarations as an unsupported capability until M7 gives
them their specified meaning; do not accept an attempted override and silently
interpret its uses as built-ins. M5 checks the unannotated explicit-call Ordered/Rat
baseline and the supported fallback trait operators, not the final overriding
infix example. M5 may be split into a bounded
trait-view prerequisite PR and an operator-bound lookup PR if review size warrants
it, but both are required for the promised final behavior. No stage enables
general-newtype/general-trait backend support that the underlying language lacks.

## 13. Critical review and decision record

### 13.1 Severity-ranked issues

Here **P0** means a possible proof of false or verification/runtime disagreement,
**P1** a required-language/compatibility defect, and **P2** a usability or
maintainability defect. These are design/implementation risks, not a claim that an
unwritten patch already contains them.

| Rank | Issue | Disposition and required evidence |
|---|---|---|
| P0 | User operators left as primitive `BinaryExpr` opcodes inherit equality/arithmetic/bounds/coinduction rules | Resolve to honest call nodes before semantic passes; exact-bound, nonreflexive-eq, datatype-rank and codatatype negatives |
| P0 | Source-only refinement/export clones reinterpret already verified operators after discarding resolved fields | Transport separate intrinsic/member selection records through checked member correspondence, including chain edges and compound operations; inherited-reflexivity false control and opaque-view round trips |
| P0 | `calc` grants transitivity and omits arbitrary operator-call preconditions | Connectors remain intrinsic; nontransitive-relation and precondition controls |
| P0 | Hidden operator calls evade frames, self-contract rules, or termination (#211/#166/#39 interactions) | Reuse normal call resolution/graphs/checks; operator/named-call negative pairs; no new unchecked assumptions |
| P0 | Reparsed/precompiled libraries change operator meaning across modes | Exact `.doo` mode checks and complete enabled `.dtr` coverage at the reuse boundary; both mismatch directions tested; raw trusted-source caller responsibility stated explicitly |
| P1 | Absolute built-in-first makes requested overrides unreachable, including datatype `<` and newtype arithmetic | Explicit `"override"` qualification approved as part of this design; tests cover all three collision families |
| P1 | New tokens or Coco grammar sets alter disabled parsing/errors | Existing-token semantic hooks; generated-table comparison and invalid-input OFF corpus |
| P1 | Both-resolver promise overlooks missing legacy trait views and bound member lookup | M5 is a real prerequisite with explicit cast/comparability parity; current checked example is refreshed-only |
| P1 | General compound expansion reevaluates destination or reads its old value after a mutating call | Explicit capture-before-call sequence and alias/mutation runtime tests |
| P1 | Ghost/runtime, generic specialization, or inference order changes slot selection | Static-view binding frozen at resolution; no fallback on proof/compilability failures; generic/literal tests |
| P1 | Mathematical rational equality is mistaken for structural equality or global congruence | Example proves equivalence/order laws only; intrinsic identity escape and representation-inequality tests |
| P1 | Export/newtype handling leaks hidden operators or implicitly lifts values/results | Visible signature index and scope-aware normalization; no inferred newtype closure |
| P2 | Operator tokens participate in textual symbol rename | Nontextual reference kind; rename regression |
| P2 | Static symbolic lookup differs from ordinary inherited static name access | Explicit owner rule and documentation; use instance slots for polymorphic requirements |
| P2 | One-slot policy and method restrictions exclude convenient overloads | Clear diagnostic and named-call/by-method alternatives; avoid a larger unreviewed inference/effect system |
| P2 | Matching library modes requires rebuilding libraries for opt-in use | Document the conservative initial cost; defer relaxation to a separately validated per-module design |

### 13.2 Decision alternatives, argued both ways

**Declaration syntax.** Dedicated syntax is more discoverable and can encode
arity/kind directly; attributes are less elegant and require string validation.
Attributes win here because they preserve ordinary named members, introduce no
declaration ambiguity or reserved word, and make disabled compatibility much
easier to demonstrate. Reserved names such as `op_Addition` would avoid an
attribute but constrain users' naming and still need mapping/validation; they
offer no compelling advantage.

**Built-in escape.** `===` is compact but handles only equality and complicates
lexing. Casts are the clearest way to expose numeric representation but cannot
express general intrinsic identity. An infix annotation keeps visual shape but
complicates grammar/heap-label interpretation. `builtin.<op>(...)` wins because
it is uniform, unshadowable, uses existing tokens, and states exactly which outer
operation is forced. Its verbosity is acceptable for an escape rather than the
ordinary notation.

**Override policy.** Strict built-in-first is simple and meets one sentence of
the brief literally, but prevents other explicit requirements. Declaration-first
for every symbol is simpler than two modes and makes every annotation effective,
but silently replaces intrinsic rank/arithmetic whenever a trait is inherited.
An explicit priority marker wins as the narrow qualification that preserves
built-in-first by default and makes replacement deliberate. The cost is a second
declaration mode and view-dependent fallback behavior; document it and freeze
bindings rather than hiding it.

**User equality: classes only, all eligible types, or none.** Classes-only makes
the identity distinction familiar but removes value-type numeric libraries while
not preventing any dangerous relation definition. None keeps all equality syntax
substitutive but rejects a requested use that ordinary predicates can already
express soundly. All eligible nominal types wins under ordinary-call semantics.
The escape and the intrinsic-context rules are mandatory. Proving equivalence
and congruence for every possible function is neither a prerequisite for notation
nor an implementable promise for open mutable APIs; quotient types are separate
language work.

**Static versus instance.** Instance-only is enough for arithmetic trait
interfaces and reuses dynamic dispatch; it makes nullable comparisons and some
mixed-type helpers awkward. Static-only is easy to select and avoids a non-null
receiver requirement, but abstract static functions are not current trait
implementation obligations. Supporting both, with a single deterministic slot
and a declared static owner, wins. It adds no typeclass dictionary or static
dispatch abstraction. Requiring static operators to be inherited symbolically
would need more owner/first-operand rules, particularly for newtypes; named static
access remains available without that complication.

**Functions versus methods.** Function-only provides the cleanest algebraic
expression feature and by-method already serves efficient executable functions.
The brief also asks for ordinary methods. Allowing methods at existing whole-RHS
positions meets that request without inventing effectful expressions. Allowing
arbitrary nested method calls would need evaluation sequencing, heap snapshots,
specification rules, and temporary lifetime decisions well beyond syntax sugar.
Comparison methods are excluded to keep derived negation and chains ordinary
expressions.

**AST representation.** Direct call replacement makes the semantic path simple
but needs another store of source form for formatting/navigation. An overloaded
`BinaryExpr` is initially less code but asks every existing opcode consumer to
remember an exception, with severe soundness consequences if one is missed. A
`ConcreteSyntaxExpression` wrapper plus ordinary resolved call wins: it follows
an existing design pattern and preserves the strongest downstream invariant.

**Derived comparisons.** Automatic `>`/`>=` reversal reduces annotations but
changes lookup ownership and needs careful operand evaluation order, especially
with heterogeneous types. Separate declarations win for a minimal predictable
feature; library wrappers are short. Deriving `!=` from the same selected `==`
needs no equality law and keeps left ownership/evaluation order unchanged, so it
is worth doing.

**`calc` treatment.** Rejecting all calculation connectors on types with an
override would reduce surprise but unnecessarily reject correct intrinsic proofs.
Automatically using user connectors would be unsound. Keeping connectors
intrinsic is consistent with retaining other generated proof mechanisms and
preserves existing calculations; clear documentation/hover and explicit lemma
calls handle the distinction. General law-aware calculations need their own
design.

### 13.3 Review conclusion

**Recommend approval of this design, including its explicit qualification of
built-in-first, before starting implementation.** The central architecture is
sound as a reduction to existing call semantics. The proof-trait pattern is
concrete: its explicit rational instance has verified and executed, including
equality/order law obligations and a failing false control.

The implementation is larger than a parser-only patch. The irreducible work is
correct early call lowering in both resolvers, strict disabled-mode isolation,
legacy trait-view parity, and preservation of library interpretation. Those are
bounded front-end tasks, not a reason to add semantic laws. The ranked P0/P1 items
are implementation acceptance gates; this design review does not certify a
future patch or the unfinished #211 termination work.

## 14. Primary source references

The source tree audited for all paths above is
[`review/4.11.0` at `62bae981952337d312cbb3d43c39f9813cca6084`](https://github.com/erniecohen/dafny/tree/62bae981952337d312cbb3d43c39f9813cca6084).
Useful direct entry points:

- [Fork instructions](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/AGENTS.md)
  and [review/version/gate rules](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/REVIEW.md).
- [Parser grammar](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Dafny.atg).
- [Legacy resolution](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs)
  and [refreshed resolution](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs).
- [Concrete-syntax wrapper](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/AST/Expressions/ConcreteSyntaxExpression.cs)
  and [calculation AST](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/AST/Statements/CalcStmt.cs).
- [Calculation translation](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs)
  and [bounds discovery](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Resolver/BoundsDiscovery/BoundsDiscovery.cs).
- [Trait-bound resolution fixtures](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/IntegrationTests/TestFiles/LitTests/LitTest/dafny0/BoundedPolymorphismResolution.dfy).
- [Library serialization and loading](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/DooFile.cs),
  [library backend](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Backends/Library/LibraryBackend.cs),
  and [refinement source cloning](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Rewriters/RefinementTransformer.cs).
- [Opt-in precedent #220](https://github.com/erniecohen/dafny/pull/220)
  and [opt-in/neutrality precedent #223](https://github.com/erniecohen/dafny/pull/223).
- [Trait termination issue #211](https://github.com/erniecohen/dafny/issues/211)
  and [audited option status](https://github.com/erniecohen/dafny/blob/62bae981952337d312cbb3d43c39f9813cca6084/Source/DafnyCore/Options/CommonOptionBag.cs).
