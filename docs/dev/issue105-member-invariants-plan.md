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
| When checked, objects | One obligation per constructor, on the values it assigned.  It is emitted at `new;` (explicit or implicit).  Because those values cannot change, this is the same obligation as "at constructor exit", not an extra one. |
| When checked, datatypes | At every constructor application and every update expression.  An intermediate value that breaks the invariant is rejected. |
| Inhabitance | A datatype with an invariant is possibly empty unless it declares a `witness`.  Reference types are unaffected. |
| Quantifying over the type | Not in the type's own invariant or in what the invariant reaches, unless the quantifier is bounded by a collection.  One condition, for classes, traits and datatypes alike. |
| Repeated obligations | None.  Each conjunct is proved once per construction site; inherited conjuncts are not re-proved in a refinement, and an ancestor reached by two paths counts once (section 4.4). |
| `extends`, refinement | Invariants accumulate by conjunction.  There is no syntax that weakens, so "only strengthened" holds by construction; the obligations are at construction sites. |
| Encoding | Per-type axioms only.  No prelude change.  A program without invariants gets the same Boogie as today. |
| Option | `--member-invariants`, default off, requires `--type-system-refresh`. |

The plan differs from the upstream proposal in three places, on purpose:

1. **The object obligation is stated at `new;`, not as a constructor
   postcondition.**  This is a difference of placement only.  The invariant
   sees only values fixed before `new;`, so "holds at `new;`" and "holds at
   exit" are one proposition, proved once (section 3.4).
2. **Datatypes have no implicit raw type.**  The proposal's
   `this.(x := x+1).(y := y+1)` is rejected; `this.(x := x+1, y := y+1)` is
   accepted (section 3.5).
3. **An invariant may not refer to the set of values of its own type**
   (section 3.3).  Such an invariant defines the type's extension in terms of
   itself, and the definition may have no solution.

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

### 3.3 An invariant may not refer to the set of values of its own type

The meaning of an invariant is one sentence: the values of the type are exactly
those built from components that satisfy it.  If the invariant itself mentions
the set of values of the type, by quantifying over it, that sentence is a
recursive definition of the set.  This is the same for a class, a trait and a
datatype.  A quantifier over a class type ranges over every reference of the
type, allocated or not (section 3.4), so no state is involved.

A recursive definition is harmless when it has a solution.

```dafny
class Never {
  const x: int
  invariant forall c: Never :: c.x != x
}
```

The invariant is false at `c == this`, so it is `invariant false`.  The empty
type is a solution, and the only one.  No constructor passes the check, the
class cannot be constructed, and nothing is unsound.

**A correction to the first version of this plan.**  It used this example to
argue that a restriction is needed, and claimed that accepting it proves
`false`.  That was wrong.  It proves `false` only under a *naive* check, one
that leaves the object being constructed out of the quantifier's range.  The
example is a requirement on the check (section 3.4).  It is not evidence for a
restriction.

The real hazard is a definition with *no* solution:

```dafny
datatype Liar = Liar(f: int) {
  invariant f == 0 ==> !(exists e: Liar :: e.f == 0)
}
```

`Liar(0)` is a value of the type exactly if no value of the type has `f == 0`,
that is, exactly if `Liar(0)` is not a value of the type.  This is not an empty
type.  There is no set that fits the definition.  For a datatype the definition
is emitted as the `$Is` equivalence of section 5.1, so the axioms are
inconsistent and `false` follows wherever they are in scope, with no
constructor application in sight.

For a class the same invariant is the same ill-formed definition.  The encoding
shows it differently: the emitted axiom for a class is only the implication
"every value of the type satisfies the invariant", which the empty set always
satisfies, and the other half is the assumption at `new;` that a reference with
the checked components exists.  So for a class the unsoundness, if any, arrives
through `new;` rather than through an axiom.  One condition for both kinds of
type is still right: the semantics is the same sentence, and the plan should
not rest on an argument that a clever enough check rescues every ill-formed
definition for classes.

#### Two conditions that guarantee a solution

- **(a) Ban the reference.**  The invariant of `T`, and everything it reaches,
  may not refer to the extension of `T` or of a type that depends on `T`.
  Membership then depends only on the components, which exist before the value
  does.  The definition has exactly one solution.
- **(b) Allow positive occurrences only.**  If every reference to the extension
  is monotone (an `exists e: T` in a positive position, or a `forall e: T` in a
  negative one), the defining operator is monotone and has fixed points.

**Recommendation: (a).**

- The quantifier an invariant would want is `forall e: T :: ...` in a positive
  position, and that one is *anti*-monotone.  Condition (b) admits "some other
  value of the type exists", which few invariants want to say.
- Under (b) there are several solutions, and one must be chosen.  The least one
  suits a datatype, read inductively.  It makes
  `invariant exists e: T :: e.f == f` an empty type, so the construction check
  must stop a value from serving as its own witness.  For a class the check at
  `new;` wants every tuple that passes to be in the type, which points to the
  greatest one.  The choice changes what the check must be, and the axioms do
  not say which solution is meant.
- (b) needs polarity tracked through every function the invariant reaches.
  Dafny does this for extreme predicates, but only inside one body.
- Neither condition is necessary.  Both reject `Never` above, which is sound.
  Nothing useful is lost by that, and (b) can be added later for existentials
  if a use appears.

#### The condition, precisely

Let `T` be a class, trait or datatype with an invariant.  Define:

- **The cone of `T`'s invariant**: the invariant expressions of `T`, its own
  and inherited, and the bodies *and specifications* of every function,
  constant initializer, subset type constraint and newtype constraint
  reachable from them through calls and uses.
- **`T`-dependent types**: `T` (and `T?`); every trait `T` extends and every
  type that extends `T`, since their extensions are defined with `T`'s;
  `object`, if `T` is a reference type; and every type built from one of these
  as a datatype component, a type argument, the base of a subset type or
  newtype, or the argument or result of an arrow.

**The extension condition.**  Within the cone of `T`'s invariant:

1. A bound variable (of a quantifier, a comprehension, or `:|`) whose type is
   `T`-dependent must have a *collection bound*: the resolver must find that it
   ranges over the elements of a set, sequence, multiset or map.
   `forall k <- kids :: k.size < size` is fine; `forall k: T :: ...` is not.
2. A call of a generic function may pass a `T`-dependent type argument only for
   a type parameter the callee does not quantify over without a bound.  This
   needs a per-function, per-type-parameter flag, computed bottom-up.  The
   first stage may approximate it by rejecting every such type argument.
3. No application of a function value other than a lambda expression or a
   named function applied on the spot.  A stored function is a body the cone
   cannot see.
4. A dynamically bound call puts every override in the cone, including
   overrides in modules not yet written.  It is accepted only where no
   override can violate clause 1; see below.  Otherwise it is rejected.

Remarks.

- A *parameter* of type `T` is not a reference to the extension.  The invariant
  may call `f(tail)`, and `f` may assume the invariant of its argument: the
  argument is a component, already a value of the type.
- For the same reason a type test `e is U` is allowed.  It examines one value
  that already exists.  (If `U` is a subset type, its constraint is in the
  cone.)
- The cone is closed under refinement by re-checking: a refinement that gives a
  body to a function in the cone is checked in the refining module.

#### What Dafny already enforces

The resolver has, for its own reasons, a restriction on every function
definition: a bound variable whose type may involve references must have a
bound, or the function is rejected as depending "on the set of allocated
references" (`Source/DafnyCore/Resolver/BoundsDiscovery/BoundsDiscovery.cs`,
using `Type.MayInvolveReferences`).  Classes, traits, and datatypes with a
reference component all count as involving references.

That restriction is not the reason for the extension condition, but it
discharges most of it.  When `T` may involve references, every `T`-dependent
type does too, so:

- clause 1 already holds in every function in the program, hence in every
  function of the cone and in every override.  Only the invariant expression
  itself needs checking, and resolving it as a function body does that;
- clause 2 is vacuous, since a type parameter that a function quantifies over
  must be declared `(!new)`;
- clause 4 accepts every dynamically bound call.  A class may state an
  invariant through a function of a trait-typed component.

So the cone analysis has real work only for a datatype with no reference
components, and there clause 4 rejects dynamically bound calls until there is
a way to bound the overrides.

Two cautions.

- This rests on the existing restriction covering every function body,
  *including lambda expressions written in methods*.  A stage 3 test must pin
  that.  If a lambda in a method may quantify over a class type without a
  bound, clause 3 is what stops it reaching an invariant, and clause 4 must
  reject dynamic calls too, since a future override could apply such a value.
- If the existing restriction is ever relaxed, the extension condition does
  not relax with it.  The cone analysis then applies to classes, and dynamic
  dispatch becomes the hard case.

### 3.4 Objects: assumed everywhere, checked at `new;`

**Assumed.**  Every non-null value of a class or trait type satisfies the
invariants of that type and of its ancestors.  This holds for `C` and for
non-null values of `C?`; there is no type of raw `C` objects.  It holds for
parameters, results, bound variables, heap reads, and for `this` in every
member except in the first division of a constructor and in the invariant
itself.  It does not depend on allocation.  The values of a class type are all
the references of that type, allocated or not, and each has the invariant.  A
function's parameter is not known to be allocated in any particular heap, and
has the invariant all the same.

A consequence worth having: `o is C` remains a run-time test of the dynamic
type alone, and still yields the invariant.  No object of dynamic type `C` ever
fails it.

**Checked.**  Once per constructor.  The obligation is: the values this
constructor gives the components satisfy the invariant.

There is no choice between checking "at `new;`" and "at constructor exit".
The invariant sees only components.  Components are assigned before `new;` and
cannot be assigned after.  So the invariant has the same truth value at `new;`
and at every later point, exit included, and the two checks are one
proposition.  The plan emits it once, at `new;`.  It is not asserted again at
exit, it is not a postcondition that each `new C(...)` re-examines, and nothing
is checked at any later use of the object.  An omitted `new;` is at the end of
the body, so for such a constructor the two places coincide as well.

The reason to place the single assertion at `new;` rather than at exit is what
may be *assumed*, not what is proved: from `new;` on, `this` is a value of its
type, and the second division and everything it calls may rely on the
invariant.

What is checked is that the invariant holds of the new object *as a member of
its type*.  Under the extension condition of section 3.3 the invariant cannot
mention the extension of the type, so it can speak of the new object only
through its components, and the check on the assigned values is that check.
A design that admitted quantifiers over the type would have to put the new
object in their range; leaving it out is the naive check of section 3.3, and
it is unsound.

A component left unassigned is arbitrary, so the invariant must hold for every
value of its type.

- Every constructor of the class is checked.  A constructor inherited through
  refinement is checked only against the conjuncts the refinement adds
  (section 4.4).
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
that refers to the *extension* of a datatype `D` is an unbounded binder or a
type test over a `D`-dependent type.  The only thing that must not refer to it
is the cone of `D`'s invariant (the extension condition, section 3.3).  So, for
a datatype with no reference components:

- A member of `D` that is not in the cone may quantify over `D` freely.
- A member in the cone may quantify over `D` when the quantifier is bounded by
  a collection.
- An unbounded quantifier over `D` in the cone makes the type's definition
  recursive in its own extension and is rejected, with a message that names
  the path from the invariant.

A function that merely takes or returns `D` is not in a cycle with anything.

For a class, a trait, or a datatype with reference components, nothing changes
from today.  A function cannot quantify over such a type without a bound, with
or without invariants; that is Dafny's existing restriction.  A method, a
lemma or a two-state function can, and may do so whether or not the type has
an invariant.

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
  whole conjunction, ancestors first, each ancestor once (section 4.4).  So
  everything that reaches type `T`
  satisfies `T`'s invariant, which is what the axiom for `T` says.
- **Well-formedness.**  A type's own invariants are checked for well-formedness
  assuming its ancestors'.
- **Visibility.**  To extend a trait, a module must see its invariant.  The
  invariant is exported exactly when the type is revealed.  A type that is only
  provided shows neither its components nor its invariant.
- **Across modules.**  A class in one module may extend a trait with an
  invariant in another.  The invariant may call a function that the later
  class overrides.  That is harmless here: the override is a function, and no
  function can refer to the extension of a reference type (section 3.3,
  clause 4).  Whether the override *terminates* is the question of
  [issue 211](https://github.com/erniecohen/dafny/issues/211), unchanged by
  this feature: an invariant is not callable, so it adds no edge to any call
  cycle.

### 4.2 Refinement

In `module M1 refines M0`, a refining `trait`, `class` or `datatype`
declaration may add `invariant` members.  The refined type's invariant is the
inherited conjunction followed by the new conjuncts.  Again there is no syntax
to weaken.

Soundness rests on two facts about refinement in Dafny.  `M1.T` is a different
type from `M0.T`.  And `M1` is translated as a whole, inherited bodies
included, but an obligation that was inherited unchanged is not proved again:
the translator turns it into an assumption unless the refinement changed the
body it sits in (`IsInherited` and `MustReverify`; inherited postconditions
are guarded by `$_reverifyPost` in the same way).  Member invariants follow
that pattern, conjunct by conjunct:

- at every inherited constructor's `new;`, and at every inherited constructor
  application and update of a datatype, the conjuncts **added in `M1`** are
  asserted.  They are new obligations, with their origin in `M1`;
- at those same sites the **inherited** conjuncts are assumed, not re-proved.
  `M0` proved them for this site and this conjunct, and neither has changed.
  A refinement can only make abstract things concrete (give a body to a
  function, a definition to an abstract type), and `M0`'s proof held for every
  such choice;
- if the refinement changes the body that contains the site, the site is
  re-verified in full, as any changed body is;
- every inherited invariant is still there, so everything `M0` proved from it
  still holds in `M1`;
- the extension condition is re-checked in `M1`, where a refinement may have
  given a body to a function in the cone.

If an inherited constructor does not establish an added conjunct, the
refinement is wrong, and the error says so, at the inherited `new;`, reported
in `M1`.  A test must pin this before stage 3 relies on it, together with its
converse: an inherited constructor whose inherited conjunct was expensive to
prove costs nothing in `M1`.

Two points follow from the same fact.

- **Datatypes.**  Strengthening a datatype's invariant strengthens the
  obligation at every construction site, which resembles adding a `requires`,
  something refinement otherwise forbids.  It is sound here only because the
  added conjuncts are checked at every inherited site.  A module with an
  abstract import `import A : M0` builds `A.T` values against `M0`'s
  invariant; when it is refined with `import A = M1`, its construction sites
  are inherited sites and get the added conjuncts as obligations.
- **Witnesses.**  A stronger invariant can invalidate the witness.  The
  inherited `witness` is checked in `M1` against the added conjuncts only.
  The refining declaration may
  declare a new one, which replaces it.

### 4.3 Abstract types

An abstract type cannot declare an invariant, but may be refined by a datatype
or class that has one.  The existing check that the refining type has the
declared characteristics does the necessary work.  A `type T(0)` cannot be
refined by a datatype with an invariant and no compilable witness.

### 4.4 Every obligation is proved once

An invariant creates proof work in exactly one kind of place: where a value is
made.  An obligation is identified by a pair, *(construction site, conjunct
declaration)*, and each pair is proved once in the program.  Everything else
is an assumption, which costs no proof.

- **Objects.**  One assertion per conjunct per constructor, at `new;`.  Not
  again at exit (section 3.4), not at `new C(...)`, not at method boundaries,
  not on use.
- **Datatypes.**  One assertion per conjunct per constructor application.
  Nothing when a value is matched, passed, returned or stored.  For an update
  `d.(f := e)`, only the conjuncts that mention an updated component are
  asserted; the others hold because `d` has the type and its other components
  are unchanged.  A conjunct that is trivially true for the constructor at
  hand (guarded by another constructor's discriminator) is not emitted.
- **Ancestors, once each.**  A type's invariant is a *set* of conjuncts, each
  keyed by the type that declares it.  The set is collected over the set of
  ancestors, not over the paths to them.  If `C extends A, B` and both extend
  `T`, a constructor of `C` proves `T`'s conjuncts once.  What cannot be
  shared is across classes: each class that extends `T` proves `T`'s conjuncts
  for its own constructors, because a trait constructs nothing and the values
  differ.
- **Refinement.**  Inherited conjuncts at inherited, unchanged sites are not
  re-proved (section 4.2).  Only added conjuncts and new or changed sites
  carry obligations.  The inherited witness is checked against added conjuncts
  only.
- **Well-formedness.**  Each conjunct is checked for well-formedness once, in
  the type that declares it, assuming the ancestors' conjuncts.  Not in
  extending types, not in refinements that leave it alone, not at construction
  sites.
- **Generics.**  One obligation per site, for arbitrary type arguments, never
  one per instantiation.

In the encoding (section 5.1) this means each declaring type contributes one
predicate for its *own* conjuncts, and the invariant of a type is the
conjunction of the own-predicates of its distinct ancestors and itself.  The
assertion at a site is split per conjunct, so that an error names the conjunct
and so that the refinement rule can assume some and assert others.

Two things that look like repetition and are not: the verifier may *use* the
invariant of the same value many times, which is an axiom instance, not a
proof; and two constructors of one class each prove the invariant, because
they assign different values.

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
**Before** those assumptions, assert the own conjuncts of each distinct
ancestor `T`, once, and then those of `C`, all applied to the surrogates
(section 4.4).

The assertion does not mention `this`.  That is the point of making the
invariant a predicate over components: the check is about values that exist
before the object does, and the axiom about objects of the type cannot be used
to discharge it.

Ancestors need no axiom of their own for a class.  `$Is(o, C?)` already implies
`$Is(o, T?)`, and `T`'s consequence axiom applies.

### 5.2 Why it is consistent

Fix a program that meets the reads restrictions and the extension condition.  Build a model
in two steps.

**Step 1: datatypes with no reference components.**  Order them so that each
comes after every type whose extension its invariant cone refers to.  The
extension condition says a type's cone does not refer to its own extension or
to that of a type that depends on it, so such an order exists.  Take the types in that order.  For
`D`, the extensions the cone refers to are already fixed, so `Inv_K(a)` is a
fixed predicate of the arguments.  Define `D` by recursion on the structure of
values: `K(a) ∈ D ⟺ a ∈ T ∧ Inv_K(a)`.  Datatype values are well-founded, which
Dafny's rank axioms already require, so the recursion has exactly one solution,
and it satisfies the `$Is` equivalence.  The definition axiom for `D.K#Inv` is
an ordinary definition of a new function symbol, guarded by the argument types.

**Step 2: classes, traits and the datatypes that contain references.**  By the
extension condition, which for these types the existing restriction on
functions enforces throughout the program, no invariant, here or in step 1,
refers to their extensions.  Each invariant is a fixed predicate of component
values.

- *Classes.*  Take the references of dynamic type `C` to be: for each tuple of
  component values in their types that satisfies the conjoined invariant,
  infinitely many references carrying those values.  A component may itself be
  a `C` object.  Build by stages, each stage using the references of the
  earlier ones as components.  The consequence axiom holds by construction.
- *Traits.*  The references of trait type `T` are those of the classes that
  extend it.  Each class's invariant includes `T`'s conjuncts, so `T`'s
  consequence axiom holds.
- *Datatypes with reference components.*  As in step 1, by recursion on
  structure, interleaved with the stages for classes.

**Unsatisfiable invariants.**  If no tuple satisfies the invariant, the model
has no reference of that dynamic type, or the datatype is the empty set.  Both
are models, and both are sound: `invariant false` gives a type with no values,
not an inconsistency.  Nothing else in the emitted Boogie says such a value
exists; see the hazards below.

**The check at `new;` is the right check.**  The model contains a reference
with the constructor's component values exactly when those values satisfy the
invariant.  The assertion over the surrogates is provable only if it holds in
every model, including this one, where it is true exactly when the tuple
satisfies the invariant.  After a successful check, the assumption that `this`
is a fresh `C` reference with those values is satisfied in the model.  The
argument uses the fact that the invariant is a predicate of the tuple.  That
is why the check may be made on the tuple and still be the check "with the new
object included" that section 3.4 requires.

**What the extension condition buys.**  Without it, step 1 can fail outright:
for `Liar` of section 3.3 there is no set satisfying the equivalence, so there
is no model at all.  And step 2 would need, for each class, an argument that
the check at `new;` admits only tuples that some solution contains.

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

**Reference to the type's own extension.**  Section 3.3.  It makes the meaning
of the type a recursive definition, which may have no solution.  The extension
condition excludes it, for classes, traits and datatypes alike.  It is the one
new semantic restriction in this plan and the part that most needs review.  It
is sufficient, not necessary, and its clauses 2 to 4 are conservative.

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

```dafny
// An object invariant through a trait-typed component: accepted by clause 4 of section 3.3.
trait Shape { predicate Fits(n: int) }               // no reads clause
class Box {
  const s: Shape
  const n: int
  invariant s.Fits(n)
  constructor (s: Shape, n: int) requires s.Fits(n) { this.s := s; this.n := n; }
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

class Max {
  const x: int
  invariant forall c: Max :: c.x <= x       // error: the invariant of 'Max' refers to the set of values of 'Max'
}

datatype Liar = Liar(f: int) {
  invariant f == 0 ==> !(exists e: Liar :: e.f == 0)
}                                           // error: the invariant of 'Liar' refers to the set of values of 'Liar'

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

`V5` is the datatype `Liar` of section 6.2 with the extension condition disabled by a
test-only switch, and a method `method V5() { assert false; }` that mentions
the type.  Its `assert false` must verify.  It records why the condition
exists, and that the vacuity tests can tell the difference.

`V6` is `class F { const x: int  invariant false  constructor () { new; } }`.
It must report an error at `new;`, and a method that takes an `F` must be
accepted.  It records that an unsatisfiable object invariant is sound: the
class simply cannot be constructed.

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
- `Source/DafnyCore/Resolver/BoundsDiscovery/BoundsDiscovery.cs`: resolve an
  invariant under the restriction that function bodies already have.  For a
  type that may involve references this discharges clause 1 of the extension
  condition.
- `Source/DafnyCore/Resolver/CallGraphBuilder.cs` and
  `Source/DafnyCore/Resolver/ModuleResolver.cs`: the extension condition's
  cone analysis, which has work to do for reference-free datatypes.  Add an
  edge from each callable to `Inv(T)` for every unbounded binder over a
  `T`-dependent type.  Report any cycle through `Inv(T)`, reusing
  `ReportCallGraphCycleError`.  Reject applications of stored function values,
  and dynamically bound calls that clause 4 does not accept, directly.  Compute the per-type-parameter "quantifies without a
  bound" flag on functions.
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
| 1 | **Datatypes, non-generic, no parents.**  Component rule, reads rule, the extension condition in its conservative form (clause 2 rejects every dependent type argument), `D.K#Inv`, the `$Is` conjunct, construction obligations, witnesses and `GetAutoInit`. | Sections 6.1 (datatype examples), 6.2, 6.3, and controls V1, V4, V5. |
| 2 | **Emptiness audit.**  Every consumer of "is this type inhabited" for datatypes.  Generic datatypes. | A test for each row of the vacuous-types table. |
| 3 | **Classes.**  `C#Inv`, the consequence axiom, the check at `new;`, the constructor requirement, audit reporting.  The test for lambda bodies (section 3.3).  Measure the trigger cost. | Controls V2 and V6, the `Bad` test; the `Buffer`, `Box` and `Max` examples. |
| 4 | **Traits and `extends`.**  Inherited invariants, the conjunction at `new;`, export visibility.  General traits extended by datatypes. | Control V3; a cross-module extension test. |
| 5 | **Refinement.**  Added invariants, replaced witnesses, the tests of section 4.2 (added conjuncts checked at inherited sites, inherited conjuncts not re-proved). | The `M0`/`M1` example; a refinement that must fail at an inherited `new;`. |
| 6 | **Relaxations**, each with its own argument: the exact clause 2; stored function values and dynamic dispatch in a reference-free datatype's cone; positive occurrences under a fixed-point reading; codatatypes. | Per relaxation. |

## 8. Open questions for the owner

Each has a recommendation.  The plan above assumes the recommendation.

1. **Datatype intermediates: no raw type?**
   *Recommend yes.*  Reject `this.(x := x+1).(y := y+1)` and accept the
   simultaneous update.  The alternative is a named raw type, which is the
   workaround's `T_` under another name.  This contradicts the upstream
   proposal's stated preference, so it is the first thing to confirm.

2. **The single constructor obligation: emitted at `new;` or at exit?**
   *Recommend `new;`.*  It is one obligation either way (section 3.4), so this
   is not a question of proof work.  At `new;` the second division may assume
   the invariant; at exit it could not.

3. **`this` restricted to component selection?**
   *Recommend yes for now.*  It gives the one-line semantics and makes the
   check at `new;` free of `this`.  The cost is that `invariant Valid()` cannot
   be written; the body of `Valid` must be inlined or moved to a static
   function over the components.  Two relaxations to consider later: functions
   declared callable on an unfinished receiver, whose bodies may not assume the
   invariant; and reading initialized consts whose initializers are
   well-formed without the invariant.

4. **Invariants that refer to the type's own extension: ban, or allow positive
   occurrences only?**
   *Recommend ban (the extension condition).*  It is one condition for classes,
   traits and datatypes.  Positive occurrences admit existentials only, not
   the `forall` an invariant would want, and leave a choice of fixed point that
   changes what the construction check must be.  Within the ban, clause 3 (no
   stored function values in the cone) is conservative and unsupported by a
   counterexample; the owner may prefer to drop it.

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
   *Recommend allow.*  It is the issue's motivating case.  It is sound because
   every inherited construction site carries the added conjuncts as new
   obligations, while the inherited conjuncts are assumed there (sections 4.2
   and 4.4).  The owner should confirm that the translator's skipping of
   inherited obligations is a premise the fork intends to keep, since the plan
   now leans on it in both directions.

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
- The extension condition is needed from the first day, in its conservative
  form.  It is the part to review hardest.
- None of it touches the heap, `new;`, inheritance or refinement.

It depends on the owner's answers to questions 1, 3, 4 and 5 only.  Classes
(stage 3) add the check at `new;` and one axiom.  They depend on questions 2
and 7, and on a cost measurement.  Traits and refinement come after that, and
add no new semantic idea: only the conjunction.
