# Member invariants: a semantic plan

This is a plan, not an implementation, for
[issue 105](https://github.com/erniecohen/dafny/issues/105): `invariant`
declarations as members of traits, classes and datatypes.  The upstream proposal
it imports is <https://github.com/dafny-lang/dafny/issues/3872>.

Nothing here is built.  The examples are in the proposed syntax and do not parse
today.  Code references are to this repository at the commit the plan was written
on; they were read, not run.

## 1. The design in brief

One principle decides everything else:

> **A type denotes a set of values, and an invariant is part of the definition of
> that set.  The invariant is a predicate over the type's *components*, and the
> type's values are exactly those built from components that satisfy it.**

The *components* of a type are the things a value is built from:

- for a datatype constructor, its arguments;
- for a class, the instance `const` fields that have no initializer, its own and
  inherited, which a constructor assigns before `new;`;
- for a trait, its own and inherited instance `const` fields that have no
  initializer.

The consequences, each argued below:

| Question | Decision |
|---|---|
| Where | `class`, `trait`, inductive `datatype`.  Not `newtype`, subset types, abstract types, codatatypes, iterators. |
| What it may read | Components only, and nothing in the heap.  `this` may appear only as the receiver of a component selection. |
| When assumed | Everywhere a value has the type.  There is no "raw" type: every expression of type `D`, `C`, `C?` (non-null) or `T` satisfies the invariant. |
| When checked, objects | Once, at `new;` (explicit or implicit), against the values the constructor assigned.  Never again: the invariant cannot change. |
| When checked, datatypes | At every constructor application and every update expression.  An intermediate value that breaks the invariant is rejected. |
| Inhabitance | A datatype with an invariant is possibly empty unless it declares a `witness`.  Reference types are unaffected. |
| Quantifying over the type | Allowed everywhere except in the invariant's own dependency cone, where a quantifier over the type must be bounded by a collection. |
| `extends`, refinement | Invariants accumulate by conjunction.  There is no syntax that weakens, so "only strengthened" holds by construction; the obligations are at construction sites. |
| Encoding | Per-type axioms only.  No prelude change.  A program without invariants gets the same Boogie as today. |
| Option | `--member-invariants`, default off, requires `--type-system-refresh`. |

The plan differs from the upstream proposal in three places, on purpose:

1. **Objects are checked at `new;`, not as a constructor postcondition.**  After
   `new;` the object is usable as a value of its type, in the rest of the
   constructor body as much as anywhere, so that is where it must hold.
2. **Datatypes have no implicit raw type.**  The proposal's
   `this.(x := x+1).(y := y+1)` is rejected; `this.(x := x+1, y := y+1)` is
   accepted (section 3.5).
3. **The invariant must be independent of the extension of its own type**
   (section 3.3).  The proposal does not mention this.  Without it the feature
   proves `false`.

## 2. Syntax and placement

### 2.1 Grammar

A new member declaration, in `ClassMemberDecl` of `Source/DafnyCore/Dafny.atg`:

```
InvariantDecl = "invariant" { Attribute } Expression(allowLemma: false, allowLambda: true)
WitnessDecl   = [ "ghost" ] "witness" Expression        // datatypes only, section 3.6
```

`invariant` and `witness` are already keywords, and neither can start a member
today, so the grammar stays LL(1).  The loop-specification production
`InvariantClause` is untouched.

- No modifiers.  An invariant is implicitly ghost and instance; `static`,
  `ghost`, `opaque` and `abstract` are errors.
- No `reads`, `requires`, `decreases` or name.  There is deliberately no way to
  write a reads clause: the frame is empty by definition.
- A type may declare several invariants.  They are conjoined in textual order,
  parents' before the type's own.  Each is checked for well-formedness assuming
  the earlier ones, as successive `requires` clauses are.
- Member order does not otherwise matter.  Every other member of the type is
  checked assuming all of the type's invariants.

### 2.2 Where it may appear

| Declaration | Allowed | Reason |
|---|---|---|
| `class` | yes | Components are the initializer-less instance consts. |
| `trait` (reference or general) | yes | Same, and every extending type inherits it. |
| inductive `datatype` | yes | Components are constructor arguments. |
| `codatatype` | no, for now | The value set is a greatest fixpoint; the argument in section 5 is for least fixpoints.  Revisit separately. |
| `newtype`, subset type | no | They already have a constraint clause and a witness clause.  Two mechanisms for one job is not a design. |
| abstract type (`type T { ... }`) | no | It has no components, so an invariant could say nothing.  An abstract type may be *refined* by a type that has invariants (section 4.3). |
| iterator, tuple, array, default class | no | No user components, or no user-controlled construction. |

A module-level `invariant` is an error, like a module-level field.

## 3. Meaning

### 3.1 The invariant is a predicate over components

Inside an invariant, `this` is not yet a value of the type: the invariant is what
decides whether it is one.  So `this` may appear only as the receiver of a
component selection:

- in a datatype: `this.d` for a destructor `d`, and `this.K?` for a
  discriminator (explicit `this` or implicit);
- in a class or trait: `this.f` for an instance `const` field `f` with no
  initializer, its own or inherited.

Everything else that uses `this` is an error: passing it as an argument,
comparing it, calling an instance function or method on it, reading a mutable
field, and reading a `const` that has an initializer.  The last is excluded
because an initializer is checked assuming the invariant, so reading it from the
invariant would be circular.

This is not a new kind of rule.  The first division of a constructor body
already has it: before `new;`, "`this` can only be used to assign to its
fields", and a `const` whose initializer depends on `this` cannot be read
(`Source/DafnyCore/Resolver/CheckDividedConstructorInitVisitor.cs`).  An
invariant is evaluated on an object in exactly that state.

The components themselves are ordinary typed values.  A component of type `E`
satisfies `E`'s invariant.  A component of the type being defined, such as the
tail of a list, satisfies the invariant being defined: it is a smaller value,
and the definition is by recursion on structure.  So invariants compose
inductively without any helper predicate over the whole value:

```dafny
datatype SortedList = Nil | Cons(head: int, tail: SortedList) {
  invariant Cons? && tail.Cons? ==> head <= tail.head
}
```

The semantics is then one line.  For a datatype `D` with constructors `K`:

```
D  =  least set such that  K(a) ∈ D  ⟺  each aᵢ ∈ Tᵢ  ∧  Inv_K(a)
```

and for a class `C` with components `f`:

```
objects of C  =  { o | dtype(o) = C,  each o.fᵢ ∈ Tᵢ,  Inv_C(o.f) ∧ Inv_T(o.f) for each ancestor T }
```

### 3.2 Reads restrictions

An invariant depends on no mutable state.

- It reads no mutable field and no array element.
- It uses no `old`, `fresh`, `allocated`, `unchanged`, and calls no two-state
  function.
- Every function it calls must have no `reads` clause at all.  The check is
  syntactic, not a proof that the frame is empty at this call: the invariant is
  translated to a Boogie function with no heap argument, and it must stay that
  way.
- It may follow `const` fields to any depth (`this.left.size`), and may read the
  immutable parts of mutable objects (`this.buffer.Length`).

This is the restriction that makes object invariants unproblematic.  Dafny has
avoided object invariants because of the question of when they must hold.  For
an invariant with an empty frame the question has a one-word answer: always.
No method can break it, so no method needs to re-establish it, and it is the
same in every heap, so two-state reasoning has nothing to say about it.

### 3.3 Extension independence: the condition the feature needs

Checking an invariant once and assuming it forever is sound only if whether a
value satisfies the invariant does not depend on which *other* values of the
type exist.  Otherwise the definition of the type refers to itself
non-monotonically, and the check can be passed vacuously:

```dafny
class Liar {
  const x: int
  invariant forall c: Liar :: c.x != x       // rejected by this plan
  constructor (x: int) { this.x := x; }
}
method M() { var c := new Liar(0); assert false; }
```

If this were accepted, the verifier would reason at `new;` as follows.  Every
existing `Liar` object `c` satisfies its invariant, which, instantiated with
`c` itself, says `c.x != c.x`.  So there are no `Liar` objects, the quantifier
is vacuously true, and the check passes.  The new object then becomes a `Liar`,
the axiom applies to it, and everything after the `new` is provable.  The same
paradox can be routed through a second type, a supertype, a type parameter, an
overridden function or a stored function value:

```dafny
trait P { const k: int }
class C { const n: int   invariant forall p: P :: p.k != n }   // rejected
class E extends P { const c: C   invariant k == c.n  ... }
```

So the invariant of `X` must be definable without reference to the extension of
`X`.  Define:

- **The cone of `X`'s invariant**: the invariant expressions of `X`, and the
  bodies *and specifications* of every function, constant initializer, subset
  type and newtype constraint reachable from them through calls and uses.
- **`X`-dependent types**: `X`; every type whose declaration mentions an
  `X`-dependent type in a component, parent, constraint, type argument or
  invariant cone; every trait and `object`, because a trait is open and may
  later acquire an `X`-dependent subtype; and every type parameter, because it
  may be instantiated with one.

**Rule E.**  Within the cone of `X`'s invariant:

1. A bound variable (of a quantifier, a comprehension, or `:|`) whose type
   mentions an `X`-dependent type must have a *collection bound*: the resolver
   must find that it ranges over the elements of a set, sequence, multiset or
   map.  `forall k <- kids :: k.size < size` is fine; `forall k: C :: ...` is
   not.
2. No type test `e is U` for an `X`-dependent `U`.  (`as` is a checked
   conversion and is fine.)
3. No dynamically bound call: no call of an instance function on a receiver of
   trait type, and no application of a function value other than a lambda
   expression or a named function applied on the spot.
4. A call of a generic function may pass an `X`-dependent type argument only for
   a type parameter the callee does not quantify over without a bound.  This
   needs a per-function, per-type-parameter flag, computed bottom-up.  The first
   stage may approximate it by rejecting every such type argument.

A function *parameter* of type `X` is not a dependency.  `Inv` may call
`f(tail)`, and `f` may assume the invariant of its argument: the argument is a
component, already a member, and smaller.

Clause 3 is the same problem as
[issue 211](https://github.com/erniecohen/dafny/issues/211): an override in a
later module can close a cycle the earlier module cannot see.  Here the cycle is
"override quantifies over `X`, `X`'s invariant calls the overridden function".
The plan forbids dynamic binding in the cone until that issue has a sound
discipline, and then reuses it: a quantifier over `X` becomes an edge to
`Inv(X)` in whatever order
[issue 211](https://github.com/erniecohen/dafny/issues/211),
[issue 266](https://github.com/erniecohen/dafny/issues/266) and
[issue 245](https://github.com/erniecohen/dafny/issues/245) settle on.

### 3.4 Objects: assumed everywhere, checked at `new;`

**Assumed.**  Every non-null value of a class or trait type satisfies the
invariants of that type and of its ancestors.  This holds for `C` and for
non-null values of `C?`; there is no type of raw `C` objects.  It holds for
parameters, results, bound variables, heap reads, and for `this` in every
member except in the first division of a constructor and in the invariant
itself.  It holds for references that are not allocated in the current heap
too: `forall c: C :: ...` ranges over objects that satisfy the invariant.

A consequence worth having: `o is C` remains a run-time test of the dynamic
type alone, and still yields the invariant.  No object of dynamic type `C` ever
fails it.

**Checked.**  At `new;`, the point where a constructor publishes `this`.  An
omitted `new;` is at the end of the body, which is the upstream proposal's
"constructor exit".  The check is on the values assigned in the first division.
A component left unassigned is arbitrary, so the invariant must hold for every
value of its type.

- Every constructor of the class is checked, including constructors inherited
  through refinement.
- A class that has or inherits an invariant must declare a constructor.  This
  extends the existing rule for classes "with fields without known
  initializers" (`Source/DafnyCore/Resolver/ModuleResolver.cs`).
- A constructor with no body is, as today, an assumption.  It now also assumes
  the invariant.  `dafny audit` must say so.
- After `new;` nothing is checked.  The components cannot be assigned again:
  the resolver already allows a `const` to be assigned only in the first
  division.

Mutable state is deliberately out of reach.  A class that wants an invariant
over mutable fields keeps using a `Valid()` predicate with a `reads` clause.
Nothing in this plan blocks a later, richer discipline for those.

### 3.5 Datatypes: values, and no intermediate values

**Assumed.**  Every value of type `D` satisfies `D`'s invariant.

**Checked.**  A constructor application `K(e)` has a well-formedness
obligation, `Inv_K(e)`, exactly as a function call has a precondition.  It is
checked wherever the expression occurs: in compiled code, ghost code,
specifications and function bodies.  A pattern is not a construction and has no
obligation.

An update expression is a construction.  `d.(x := e, y := e')` builds one new
value, with one obligation, about the value with both fields replaced.

**No raw type.**  The upstream proposal suggests accepting

```dafny
const inc: T := this.(x := x+1).(y := y+1)
```

by giving the middle expression "an implicit datatype defined as `T` without
the invariant".  This plan rejects that, for three reasons.

1. *It has no principled boundary.*  If the receiver of an update may be raw,
   may a `let`-bound variable?  A function argument?  A raw value stored in a
   `seq<T>`?  Each "no" is an arbitrary line; each "yes" needs the raw type to
   be a real type, with a name, in signatures.
2. *A real raw type takes back what the feature gives.*  The complaint about
   the subset-type workaround is that there are two types, `T_` and `T`, and
   the relation between them must be published.  A raw `T` is `T_` again.
3. *The language already has the right construct.*  A simultaneous update
   builds the new value in one step, and the commenter on the upstream proposal
   pointed this out.  A construction that truly needs staging uses local
   variables for the parts, or a separate datatype for the unfinished value.

So `this.(x := x+1).(y := y+1)` is an error at the inner update, and the
message should suggest the simultaneous form.

**The type system is untouched.**  Under `--type-system-refresh`, a pre-type is
a type without subset-type constraints, and the verifier enforces the
difference (`docs/dev/TypeSystemRefresh.md`).  A datatype with an invariant is
*its own* pre-type.  There is no second type to infer, no conversion to insert
and no bound-variable type to widen.  The invariant is a precondition of the
constructors and a fact about the type, and nothing else.

### 3.6 Inhabitance and witnesses

**Reference types.**  Nothing changes.  A non-null reference type is already
never auto-initialized: a variable of the type must be assigned before use, and
an object exists only if a constructor returned it.  A class whose invariant is
unsatisfiable is merely a class no verified constructor can build.

**Datatypes.**  Today a datatype is inhabited if it has a grounding
constructor, a syntactic fact.  An invariant can empty it:

```dafny
datatype Empty = Mk(n: nat) { invariant n < 0 }
```

So a datatype that has an invariant is **possibly empty** unless it declares a
witness, in the manner of subset types:

```dafny
datatype Interval = Interval(lo: int, hi: int) {
  invariant lo <= hi
  witness Interval(0, 0)        // nonempty and auto-initializable: satisfies (0)
}
datatype Cert = Cert(ghost p: Proof) {
  invariant Checks(p)
  ghost witness Cert(Axiom)     // nonempty, not compilable: satisfies (00) only
}
```

- The witness expression is a closed expression of the type.  Checking it is
  just checking that expression: its constructor applications carry the
  obligations.
- With no witness, the type behaves as a subset type with `witness *` does.
  Variables need definite assignment, and the type cannot instantiate a `(0)`
  or `(00)` type parameter.  All of that machinery exists and is driven by
  `Type.GetAutoInit` (`Source/DafnyCore/AST/Types/Types.cs`).  The work is to
  make `GetAutoInit` answer for such datatypes, and to audit every place that
  asks "is this a `SubsetTypeDecl`" where it should ask `GetAutoInit`.
- Emptiness propagates.  A datatype whose only grounding constructor has a
  possibly-empty component is possibly empty, as it is today for components of
  possibly-empty subset types.
- At most one witness per type in a module.  A refinement may supply or replace
  it (section 4.2).

### 3.7 What replaces the subset-type workaround

The workaround in the issue is

```dafny
type T = t: T_ | t.i() witness *
datatype T_ = ... { predicate i() ... }
```

| | Workaround | Member invariant |
|---|---|---|
| Types | two, with a published relation | one |
| `requires i()` / `ensures i()` in members | on every member | none |
| Checked | at each `T_`-to-`T` conversion, by the verifier | at construction |
| Quantify over `T` in a member of `T_` | rejected: "recursive constraint dependency" | accepted, outside the cone |
| Refine or extend | `T_` can be, `T` cannot | the one type can be |
| Strengthen in a refinement | a predicate with an ever-stronger postcondition | add an `invariant` |
| Intermediate invalid values | have type `T_` | not values of the type |

**Quantifying over the type.**  Today the resolver puts the subset type and its
constraint in one call-graph component with everything the constraint reaches
(`CheckForCyclesAmongRedirectingTypes` in
`Source/DafnyCore/Resolver/ModuleResolver.cs`), and reports any cycle.  The
members of `T_` mention `T`, and the constraint of `T` calls a member of `T_`,
so the cycle is easy to close.

With member invariants the dependency is narrower and exact.  The only thing
that depends on the *extension* of `D` is an unbounded binder or a type test
over a `D`-dependent type.  The only thing whose meaning must not depend on
that extension is the cone of `D`'s invariant.  So:

- A member of `D` that is not in the cone may quantify over `D` freely.
- A member in the cone may quantify over `D` when the quantifier is bounded by
  a collection.
- An unbounded quantifier over `D` in the cone is the liar of section 3.3 and
  is rejected, with a message that names the path from the invariant.

A function that merely takes or returns `D` is not in a cycle with anything.

## 4. Inheritance and refinement

### 4.1 `extends`

A class, trait or datatype that extends a trait `T` inherits `T`'s invariants,
instantiated at the parent type arguments.  Its invariant is the conjunction of
all its ancestors' invariants and its own.

- **Only strengthened, by construction.**  There is no syntax to drop, replace
  or weaken an inherited invariant.  The conjunction implies each conjunct, so
  there is nothing to check about the invariants themselves.
- **Checked soundly at construction.**  A value of a trait type is always a
  value of some class or datatype.  That type's construction check asserts the
  whole conjunction, ancestors first.  So everything that reaches type `T`
  satisfies `T`'s invariant, which is what the axiom for `T` says.
- **Well-formedness.**  A type's own invariants are checked for well-formedness
  assuming its ancestors'.
- **Visibility.**  To extend a trait, a module must see its invariant.  The
  invariant is exported exactly when the type is revealed.  A type that is only
  provided shows neither its components nor its invariant.
- **Across modules.**  A class in one module may extend a trait with an
  invariant in another.  The invariant is in the earlier module, and by Rule E
  its cone cannot reach an override, so the later class cannot change its
  meaning.  This feature therefore adds no new instance of the hole described
  in [issue 211](https://github.com/erniecohen/dafny/issues/211).

### 4.2 Refinement

In `module M1 refines M0`, a refining `trait`, `class` or `datatype`
declaration may add `invariant` members.  The refined type's invariant is the
inherited conjunction followed by the new conjuncts.  Again there is no syntax
to weaken.

Soundness rests on one fact about refinement in Dafny: **`M1.T` is a different
type from `M0.T`, and `M1` is resolved and verified as a whole, inherited
bodies included.**  So:

- every inherited constructor of a class is re-checked at its `new;` against
  the stronger invariant;
- every inherited constructor application and update of a datatype is
  re-checked;
- every inherited invariant is still there, so everything `M0` proved from it
  still holds in `M1`;
- Rule E is re-checked in `M1`, where a refinement may have given a body to a
  function in the cone.

If an inherited body no longer verifies, the refinement is wrong, and the error
says so.  Nothing is assumed.

This must be pinned by a test before stage 3 relies on it: a refinement that
adds an invariant an inherited constructor does not establish must fail at the
inherited `new;`, reported in `M1`.

Two points follow from the same fact.

- **Datatypes.**  Strengthening a datatype's invariant strengthens the
  obligation at every construction site, which resembles adding a `requires`,
  something refinement otherwise forbids.  It is sound here only because the
  sites are re-verified.  A module with an abstract import `import A : M0`
  builds `A.T` values against `M0`'s invariant; it is itself re-verified when
  refined with `import A = M1`.
- **Witnesses.**  A stronger invariant can invalidate the witness.  The
  inherited `witness` is re-checked in `M1`.  The refining declaration may
  declare a new one, which replaces it.

### 4.3 Abstract types

An abstract type cannot declare an invariant, but may be refined by a datatype
or class that has one.  The existing check that the refining type has the
declared characteristics does the necessary work.  A `type T(0)` cannot be
refined by a datatype with an invariant and no compilable witness.

## 5. Soundness

### 5.1 What is emitted

**No prelude change.**  `DafnyPrelude.bpl` is untouched.  Everything is per
type, and only for types that declare or inherit an invariant.  With the option
off, or with no invariants in the program, the Boogie output is the same as
today.

**For a datatype** `D<G>` with constructor `K(a₁: T₁, …, aₙ: Tₙ)`:

```
function D.K#Inv(G, a₁, …, aₙ): bool;

// definition: the translated conjunction, parents' invariants first
axiom (forall G, a ::  { D.K#Inv(G, a) }
  $Is(a₁, T₁) ∧ … ∧ $Is(aₙ, Tₙ)  ==>  (D.K#Inv(G, a)  <==>  ⟦Inv_K⟧(G, a)));

// the existing $Is axiom, with one more conjunct
axiom (forall G, a ::  { $Is(#D.K(a), Tclass.D(G)) }
  $Is(#D.K(a), Tclass.D(G))  <==>  $Is(a₁, T₁) ∧ … ∧ $Is(aₙ, Tₙ) ∧ D.K#Inv(G, a));
```

and one obligation: at every `DatatypeValue` expression, after the arguments
are checked, `assert ⟦Inv_K⟧(G, args)`, one assertion per conjunct so the error
names the invariant that failed.

`Inv_K` is the invariant specialised to constructor `K`: discriminators become
constants, and destructors of `K` become the arguments.  A destructor of
another constructor is a well-formedness error in that branch, as usual.

**For a class or trait** `C<G>` with components `f₁ … fₙ`:

```
function C#Inv(G, f₁, …, fₙ): bool;          // over component values, not over a reference

axiom (forall G, f ::  { C#Inv(G, f) }
  $Is(f₁, T₁) ∧ …  ==>  (C#Inv(G, f)  <==>  ⟦Inv_C⟧(G, f)));

// a consequence axiom; the definition of $Is for the class is left alone
axiom (forall G, o: ref ::  { $Is(o, Tclass.C?(G)) }
  o != null ∧ $Is(o, Tclass.C?(G))  ==>  C#Inv(G, C.f₁(G, o), …, C.fₙ(G, o)));
```

and one obligation.  In the translation of `new;`
(`DividedBlockStmt` in
`Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs`), the
first division already runs on local *surrogates* of the fields, and `new;`
then assumes `$Is(this, C?)` and that each field equals its surrogate.
**Before** those assumptions, assert `T#Inv(surrogates)` for each ancestor `T`
and then `C#Inv(surrogates)`.

The assertion does not mention `this`.  That is the point of making the
invariant a predicate over components: the check is about values that exist
before the object does, and the axiom about objects of the type cannot be used
to discharge it.

Ancestors need no axiom of their own for a class.  `$Is(o, C?)` already implies
`$Is(o, T?)`, and `T`'s consequence axiom applies.

### 5.2 Why it is consistent

Fix a program that satisfies Rule E, and order its types so that every type
comes after the types its invariant cone depends on in extension.  Rule E says
such an order exists.  Build a model by stages along it.

- **Datatypes.**  `D` is the least fixpoint of
  `K(a) ∈ D ⟺ a ∈ T ∧ Inv_K(a)`.  The operator is monotone.  Membership of the
  arguments in `D` is a positive occurrence.  By Rule E, `Inv_K(a)` has the
  same truth value whatever `D` is taken to be: it looks at `D` only through
  its arguments, or through quantifiers bounded by collections inside them,
  which are already fixed.  Datatype values are well-founded, since Dafny's
  rank axioms already require it, so the least fixpoint is the set the `$Is`
  equivalence describes.  The definition axiom for `D.K#Inv` is an ordinary
  definition of a new function symbol, guarded by the argument types.
- **Classes.**  Take the references of dynamic type `C` to be: for each tuple
  of component values in their types that satisfies the conjoined invariant,
  infinitely many references carrying those values, built by the same
  induction.  A component may itself be a `C` object; it comes from an earlier
  stage.  The consequence axiom holds by construction.  Extension independence
  is what makes "satisfies the invariant" a property of the tuple alone, and
  so makes the construction well-defined.
- **Traits.**  The references of trait type `T` are those of the classes that
  extend it.  Each class's invariant includes `T`'s conjuncts, so `T`'s
  consequence axiom holds.
- **Unsatisfiable invariants.**  If no tuple satisfies the invariant, the model
  has no reference of that dynamic type, and the datatype is the empty set.
  Both are models.  Nothing else in the emitted Boogie says such a value
  exists; see the hazards below.

**The check is not vacuous.**  At `new;` the model contains a reference with
the constructor's component values exactly when those values satisfy the
invariant.  The assertion is over the surrogates.  It is provable only if it
holds in every model, including the one just built, where it is true exactly
when the tuple satisfies the invariant.  After a successful check, the
assumption that `this` is a fresh `C` reference with those values is satisfied
in that model.  This is the step that fails for the liar: with a self-referring
invariant the model's `C` is not determined by the tuples, and the argument
does not start.

The axioms assert no new fact about existing programs.  The obligations only
add checks.  The one assumption that carries weight is the existing one, that
`new` can always find a fresh reference of the right type.  It now has a side
condition, and the check at `new;` discharges it.

Per `AGENTS.md`, these axioms need vacuity controls: tests in which
`assert false` must still fail in contexts that trigger them.  Section 6.4
lists them.  The axioms are part of an opt-in feature with its own option, and
they are emitted only for types with invariants, so they do not join
`--additional-axioms`.

### 5.3 Hazards

**Vacuous types.**  A type that is believed inhabited when it is not makes
every program point where a value of it is in scope unreachable in the model,
so anything verifies there.  The channels, and what closes each:

| Channel | Closed by |
|---|---|
| Local or out-parameter of a datatype with an invariant, used before assignment | Possibly-empty characteristic (section 3.6); definite-assignment rules that already exist |
| Class field of such a type | Existing rule: the class must have a constructor that assigns it |
| Type argument for `(0)` or `(00)` | Characteristic check |
| Enclosing datatype believed inhabited through a component | `GetAutoInit` propagation |
| Bodiless function or method returning the type | An assumption today, for any possibly-empty type; unchanged, and it should appear in `dafny audit` |
| `new C` on a class with no constructor | Rejected: the class must declare one |
| Extern or bodiless constructors, extern datatype values | Assumptions; reported by `dafny audit` |
| Any place the translator branches on `SubsetTypeDecl` to decide emptiness | An audit, as a stage-2 task with its own tests |

A parameter of a possibly-empty type is not a hazard.  A method that takes an
`Empty` is never called.

**Mutable objects whose invariants read fields.**  Prevented by the reads
restriction, not managed.  An invariant reads only components, which are
assigned once before `new;`.  The specific risks:

- A `const` assigned after `new;`: already an error.
- A component that is a reference to a mutable object (`const buf: array<int>`):
  the invariant can speak of the reference and its immutable parts
  (`buf.Length`), never its contents.
- `this` escaping before the check: already impossible in the first division.
- A constructor that calls a method on `this` after `new;` while it "is still
  setting things up": the object already satisfies its invariant, and the
  mutable state the method sees is not the invariant's business.
- An invariant in a trait, about a `const` a class assigns: the class's `new;`
  checks it.  Consts cannot be overridden.

**Two-state reasoning.**  An invariant has no heap argument, so `old(inv)` is
`inv`.  Frame axioms, `unchanged`, `modifies` and heap succession never mention
it.  The single place where order of events matters is inside `new;`: the
assertion must precede the assumption that `this` has the type.  A regression
test must pin that order.  If the two were swapped, the test's `assert false`
after a bad constructor would verify.

The invariant holds for unallocated references of the type as well.  This is
deliberate.  A function has no way to know its argument is allocated, and the
invariant must be usable in functions.  It is sound because the model fixes the
components of all references at once, in the way `const` fields are already
modelled.

**Extension dependence.**  Section 3.3.  This is the hazard that is new with
this feature, and Rule E is the part of the design that most needs review.  It
is a sufficient condition and is conservative.  Each relaxation, starting with
dynamic dispatch, needs its own argument.

**Trigger cost.**  The class consequence axiom triggers on `$Is(o, Tclass.C?)`,
which is a common term.  It appears only for classes with invariants, but its
cost must be measured in resource units on the first real examples.  If it is
too eager, the fallback is to trigger on the component functions `C.fᵢ(G, o)`.

## 6. Examples

### 6.1 Accepted

```dafny
// A datatype invariant, used without restating it.
datatype Interval = Interval(lo: int, hi: int) {
  invariant lo <= hi
  witness Interval(0, 0)

  function Width(): nat { hi - lo }                    // nat: uses the invariant
  function Shift(d: int): Interval { this.(lo := lo + d, hi := hi + d) }   // one construction
  predicate Tightest()
  { forall j: Interval :: j.lo <= lo && hi <= j.hi ==> j == this }   // quantifies over the type: not in the cone
}

method Use(i: Interval) {
  assert i.lo <= i.hi;                                 // from the type
  var j := Interval(i.lo, i.hi + 1);                   // checked here
}
```

```dafny
// A recursive invariant: local, composed by induction.
datatype Sorted = Nil | Cons(head: int, tail: Sorted) {
  invariant Cons? && tail.Cons? ==> head <= tail.head
  witness Nil

  function Push(v: int): Sorted                 // the Cons is checked here; 'this' has the invariant by its type
    requires Nil? || v <= head
  { Cons(v, this) }
}
```

```dafny
// A class: checked once, at new.
class Buffer {
  const data: array<int>
  const cap: nat
  var len: nat                                         // mutable: not the invariant's business

  invariant data.Length == cap && 0 < cap              // immutable facts only

  constructor (n: nat)
    requires 0 < n
  {
    data := new int[n];
    cap := n;
    len := 0;
    new;                                               // invariant checked here
    Reset();                                           // 'this' is a full Buffer
  }
  method Reset() modifies this { len := 0; }
  method Get(i: nat) returns (x: int)
    requires i < cap
  { x := data[i]; }                                    // i < data.Length, from the invariant
}
```

```dafny
// Inheritance and refinement: only ever stronger.
module M0 {
  trait Account {
    const limit: int
    invariant 0 <= limit
  }
}
module M1 refines M0 {
  trait Account ... {
    invariant limit <= 1_000_000                       // added conjunct
  }
  class Savings extends Account {
    const rate: nat
    invariant rate <= 100
    constructor (l: int, r: nat)
      requires 0 <= l <= 1_000_000 && r <= 100
    { limit := l; rate := r; }                         // implicit new; checks all three
  }
  method Check(a: Account) { assert 0 <= a.limit <= 1_000_000; }
}
```

```dafny
// A bounded quantifier over the type itself, inside the invariant.
datatype Tree = Node(size: nat, kids: seq<Tree>) {
  invariant forall k <- kids :: k.size < size
  witness Node(0, [])
}
```

### 6.2 Rejected by the resolver

```dafny
datatype D = D(x: nat, y: nat) {
  invariant Ok()               // error: 'this' may be used in an invariant only to select a component
  predicate Ok() { x == y }
}

class C {
  var v: int
  const k: int
  const twice: int := 2 * k
  invariant v >= 0             // error: an invariant may not read a mutable field
  invariant twice >= 0         // error: 'twice' has an initializer; it is not a component
}

class Liar {
  const x: int
  invariant forall c: Liar :: c.x != x      // error: unbounded quantifier over a type that depends on
}                                           //        'Liar' in the invariant of 'Liar'

trait Shape { predicate Fits(n: int) }
class Box {
  const s: Shape
  const n: int
  invariant s.Fits(n)          // error: dynamically bound call in an invariant (see issue 211)
}

newtype Even = x: int | x % 2 == 0 {
  invariant x >= 0             // error: a newtype states its constraint in its declaration
}

class NoCtor {
  const k: int
  invariant k > 0              // error: a class with an invariant must declare a constructor
}
```

### 6.3 Rejected by the verifier

```dafny
datatype D = D(x: nat, y: nat) {
  invariant x == y
  witness D(0, 1)                              // error: invariant might not hold for the constructed value
  function Inc(): D { this.(x := x + 1).(y := y + 1) }
                                               // error at the inner update; use this.(x := x+1, y := y+1)
}

class Pos {
  const k: int
  invariant k > 0
  constructor (k: int) { this.k := k; }       // error at the implicit 'new;': invariant might not hold
}

method M() {
  var e: Empty;                                // Empty from section 3.6: no witness
  Consume(e);                                  // error: variable 'e' is used before it is assigned
}
```

### 6.4 Vacuity controls

Each of `V1` to `V4` must report an error on its `assert false`:

```dafny
method V1(i: Interval) { assert false; }                       // $Is axiom for a datatype with an invariant
method V2() { var p := new Pos(1); assert false; }             // class consequence axiom, after a good new
method V3(a: Account) { assert false; }                        // trait axiom, with inheritance
method V4() { var t := Node(1, [Node(0, [])]); assert false; } // recursive invariant

class Bad { const k: int  invariant k > 0
  constructor () { k := 0; new; } }                            // must report an error at 'new;'
```

`Bad` pins the order of the check and the assumption inside `new;`.  With the
two swapped, it would verify with no error at all.

`V5` is the liar of section 3.3 with Rule E disabled by a test-only switch.  Its
`assert false` must verify.  It records why the rule exists, and that the
vacuity tests can tell the difference.

## 7. Implementation sketch

### 7.1 Files

**Option.**  `Source/DafnyCore/Options/CommonOptionBag.cs`: `--member-invariants`,
default false, usable from `dfyconfig.toml`.  It is an error without
`--type-system-refresh`.  `Source/DafnyCore/Options/DafnyCommands.cs`: add to
the resolver options.

**Parser.**  `Source/DafnyCore/Dafny.atg`:

- `ClassMemberDecl`: a new alternative for `InvariantDecl`, and one for
  `WitnessDecl`.
- With the option off, the alternatives are disabled by a semantic predicate,
  so the diagnostic for `trait T { invariant true }` stays the one recorded in
  the issue.
- `TypeMembers` reaches datatypes, newtypes and abstract types through the same
  production; the resolver rejects the misplaced ones with a specific message.

**AST.**  `Source/DafnyCore/AST/Members/`: a new `InvariantDecl : MemberDecl`
that is an `ICallable`, so it is a call-graph node and a code context, and a
`WitnessDecl`.  On `TopLevelDeclWithMembers`: `Invariants`, and a method that
returns all invariants, inherited first, with substitutions.  On `DatatypeDecl`:
the witness and its kind.  `Cloner`, `Printer`, `Substituter`, and the
serialization used for `.doo` files need the new nodes.

**Resolver.**

- `Source/DafnyCore/Resolver/PreType/`: resolve the invariant as a ghost
  expression of type `bool`, with `this` in scope under a new resolution
  context flag, `InInvariant`, alongside `InFirstPhaseConstructor` in
  `Source/DafnyCore/Resolver/ResolutionContext.cs`.
- A new pass modelled on
  `Source/DafnyCore/Resolver/CheckDividedConstructorInitVisitor.cs`: the "`this`
  only as a component receiver" rule, and the syntactic reads restrictions.
- `Source/DafnyCore/Resolver/CallGraphBuilder.cs` and
  `Source/DafnyCore/Resolver/ModuleResolver.cs`: Rule E.  Add an edge from each
  callable to `Inv(X)` for every unbounded binder or type test over an
  `X`-dependent type.  Report any cycle through `Inv(X)`, reusing
  `ReportCallGraphCycleError`.  Reject dynamic calls and open types in the cone
  directly.  Compute the per-type-parameter "quantifies without a bound" flag
  on functions.
- `Source/DafnyCore/AST/Types/Types.cs`: `GetAutoInit` for datatypes with
  invariants.
- `Source/DafnyCore/Resolver/ModuleResolver.cs`: "a class with an invariant
  must declare a constructor".
- `Source/DafnyCore/Rewriters/RefinementTransformer.cs`: `MergeClass` already
  merges members of classes, traits and datatypes; let it accept added
  invariants and a replaced witness.

**Verifier.**

- `Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs`: declare
  `D.K#Inv`, its definition axiom, and the extra conjunct in the constructor
  `$Is` axiom.
- `Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs`
  (`AddClassMembers`): declare `C#Inv` and the consequence axiom, next to the
  `implements$` and field axioms.
- `Source/DafnyCore/Verifier/BoogieGenerator.ExpressionWellformed.cs`: the
  obligation at `DatatypeValue`, and so at updates, which desugar to
  constructor applications.
- `Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs`
  (`DividedBlockStmt`): the assertion over surrogates before
  `SelectAllocateObject`.
- A well-formedness procedure for each invariant and each witness, in the
  manner of `AddWellformednessCheck` for subset types in
  `Source/DafnyCore/Verifier/BoogieGenerator.Types.cs`.
- `Source/DafnyCore/Verifier/ProofObligationDescription.cs`: two descriptions,
  "invariant might not hold for the constructed value" and "invariant might not
  hold at `new;`".

**Compilers.**  Nothing.  Invariants are ghost and erased.  A compilable
witness feeds the existing default-value machinery, as a subset type's does.

**Audit.**  `Source/DafnyCore/Auditor/`: a bodiless constructor of a class with
an invariant assumes the invariant.

**What every change needs** (`AGENTS.md`): `docs/dev/news/105.feat`; tests in
`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-105*.dfy`;
the Reference Manual (`docs/DafnyRef/`) sections on types, classes, datatypes
and refinement; `--help` text for the option.

### 7.2 Stages

Every stage is behind `--member-invariants`.  Every stage shows, on the suite
and the standard library, that verdicts and resource counts are unchanged with
the option off.

| Stage | Content | Exit test |
|---|---|---|
| 0 | Option, grammar, AST, printer, cloner.  The resolver rejects every invariant with "not yet supported". | Round-trip printing; the diagnostic with the option off is unchanged. |
| 1 | **Datatypes, non-generic, no parents.**  Component rule, reads rule, Rule E in its conservative form (clause 4 rejects every dependent type argument), `D.K#Inv`, the `$Is` conjunct, construction obligations, witnesses and `GetAutoInit`. | Sections 6.1 (datatype examples), 6.2, 6.3, and controls V1, V4, V5. |
| 2 | **Emptiness audit.**  Every consumer of "is this type inhabited" for datatypes.  Generic datatypes. | A test for each row of the vacuous-types table. |
| 3 | **Classes.**  `C#Inv`, the consequence axiom, the check at `new;`, the constructor requirement, audit reporting.  Measure the trigger cost. | Control V2 and the `Bad` test; the `Buffer` example. |
| 4 | **Traits and `extends`.**  Inherited invariants, the conjunction at `new;`, export visibility.  General traits extended by datatypes. | Control V3; a cross-module extension test. |
| 5 | **Refinement.**  Added invariants, replaced witnesses, the re-verification test of section 4.2. | The `M0`/`M1` example; a refinement that must fail at an inherited `new;`. |
| 6 | **Relaxations**, each with its own argument: the exact clause 4; dynamic dispatch in the cone, after [issue 211](https://github.com/erniecohen/dafny/issues/211); codatatypes. | Per relaxation. |

## 8. Open questions for the owner

Each has a recommendation.  The plan above assumes the recommendation.

1. **Datatype intermediates: no raw type?**
   *Recommend yes.*  Reject `this.(x := x+1).(y := y+1)` and accept the
   simultaneous update.  The alternative is a named raw type, which is the
   workaround's `T_` under another name.  This contradicts the upstream
   proposal's stated preference, so it is the first thing to confirm.

2. **Objects checked at `new;` rather than at constructor exit?**
   *Recommend `new;`.*  It is where the object becomes a value of its type.
   Exit is the special case of an omitted `new;`.

3. **`this` restricted to component selection?**
   *Recommend yes for now.*  It gives the one-line semantics and makes the
   check at `new;` free of `this`.  The cost is that `invariant Valid()` cannot
   be written; the body of `Valid` must be inlined or moved to a static
   function over the components.  Two relaxations to consider later: functions
   declared callable on an unfinished receiver, whose bodies may not assume the
   invariant; and reading initialized consts whose initializers are
   well-formed without the invariant.

4. **Rule E as stated, with dynamic dispatch and stored function values banned
   from the cone?**
   *Recommend yes.*  The bans cost real expressiveness.  A class holding a
   trait-typed component cannot state an invariant through that component's
   functions.  The alternative is unsound, and the principled relaxation is the
   same mechanism
   [issue 211](https://github.com/erniecohen/dafny/issues/211) needs.

5. **A datatype with an invariant and no `witness`: possibly empty, or an
   error?**
   *Recommend possibly empty.*  It is always sound and never guesses.  A subset
   type without a witness clause instead makes the verifier search for one.
   That behaviour could be mirrored for uniformity, but a silent search is the
   less explicit choice.

6. **Witness as a member (`witness e`) rather than a clause on the type header?**
   *Recommend member.*  A refinement can then replace it with the syntax it
   already uses to add members.

7. **Must a class with an invariant declare a constructor?**
   *Recommend yes.*  The alternative is a check at every `new C` site that the
   invariant holds for arbitrary components, which is non-modular and almost
   never true.

8. **Newtypes and abstract types.**
   *Recommend no invariants on either.*  A newtype keeps its constraint clause.
   If strengthening a newtype's constraint in a refinement is wanted, that is a
   separate, smaller proposal.

9. **Strengthening a datatype invariant in a refinement.**
   *Recommend allow.*  It is the issue's motivating case, and it is sound
   because the refining module is re-verified.  The owner should confirm the
   re-verification premise is one the fork intends to keep.  A future
   optimization that skips inherited bodies would have to exclude construction
   sites.

10. **Codatatypes.**
    *Recommend defer.*  Checking at each constructor application is probably
    right, but the model argument is a greatest fixpoint, and "smaller
    component" is no longer available.

11. **Option name and off-behaviour.**
    *Recommend `--member-invariants`*, with the parser diagnostic unchanged
    when the option is off.  A friendlier "enable `--member-invariants`"
    message would change an existing diagnostic, which the rules allow for an
    error-message fix; it can be a separate change.

12. **Named invariants** (`invariant Sorted: ...`) for error messages, `reveal`
    and opacity.
    *Recommend defer.*  Attributes already attach, and `{:error "..."}` covers
    messages.

## 9. Settled enough to implement first

Stages 0 and 1: **invariants on non-generic inductive datatypes with no
parents.**

- The semantics is a least fixpoint with a three-line statement.
- The encoding is one function, one definition axiom and one conjunct per
  constructor.
- The obligation sits where a function precondition would.
- Witnesses reuse the possibly-empty machinery.
- Rule E is needed in full from the first day, in its conservative form.  It is
  the part to review hardest, and datatypes are the smallest setting in which
  to review it.
- None of it touches the heap, `new;`, inheritance or refinement.

It depends on the owner's answers to questions 1, 3, 4 and 5 only.  Classes
(stage 3) add the check at `new;` and one axiom.  They depend on questions 2
and 7, and on a cost measurement.  Traits and refinement come after that, and
add no new semantic idea: only the conjunction.
