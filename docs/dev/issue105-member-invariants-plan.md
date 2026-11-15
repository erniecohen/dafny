# Member invariants: a semantic plan

This is a plan, not an implementation, for
[issue 105](https://github.com/erniecohen/dafny/issues/105): `invariant`
declarations as members of types.  The upstream proposal it imports is
<https://github.com/dafny-lang/dafny/issues/3872>.

Nothing here is built.  The examples are in the proposed syntax and do not parse
today.  Code references are to this repository at the commit the plan was written
on; they were read, not run.

## 1. The design in brief

One principle decides everything else:

> **A type denotes a set of values, and an invariant is part of the definition of
> that set.  Every type `T` with an invariant has a *raw type*, `raw T`: the
> same declaration without the invariant.  The values of `T` are exactly the
> raw values that satisfy the invariant.**

So `T` stands to `raw T` as a subset type stands to its base type, and under
`--type-system-refresh` `raw T` is the pre-type of `T`.  The difference from
writing the subset type by hand is that there is one declaration: the raw type
is derived from it, and follows it through `extends` and refinement.

The consequences, each argued below:

| Question | Decision |
|---|---|
| Where | Wherever members can be declared: `class`, `trait`, `datatype`, `codatatype`, `newtype`, abstract type.  Not subset types, which have no members. |
| The raw type | Derived and nameable: `raw T`.  Constructors and updates produce raw values.  Using a raw value where a `T` is expected checks the invariant, as a subset-type conversion does. |
| What an invariant may use | Any member with empty reads, applied to `this` or to anything else.  Inside an invariant `this` has type `raw T`. |
| When assumed | Everywhere a value has type `T`. |
| When checked, value types | Where a raw value is used at type `T`. |
| When checked, objects | One obligation per constructor, emitted at `new;` (explicit or implicit).  Because the state it reads cannot change, this is the same obligation as "at constructor exit", not an extra one. |
| Newtypes | The `x: B \| P(x)` constraint *is* the invariant.  The old syntax is kept as sugar during migration. |
| Codatatypes | Included.  The type is a greatest fixed point.  A co-recursive function is checked prefix-wise, as a greatest lemma is: the result of a co-recursive call counts as a `T` only as the component it is placed in, never in the proof of an invariant.  The raw type is raw at every depth (section 3.8). |
| Inhabitance | A value type with an invariant is possibly empty unless it declares a `witness`.  Its raw type is as inhabited as the type is today.  Reference types are unaffected. |
| Quantifying over the type | Not in the type's own invariant or in what the invariant reaches, unless the quantifier is bounded by a collection.  One condition for every kind of type. |
| Repeated obligations | None.  Each conjunct is proved once per construction site; inherited conjuncts are not re-proved in a refinement, and an ancestor reached by two paths counts once (section 4.4). |
| `extends`, refinement | Invariants accumulate by conjunction.  There is no syntax that weakens, so "only strengthened" holds by construction.  The raw type follows automatically. |
| Encoding | Per-type axioms only.  No prelude change.  A program without invariants gets the same Boogie as today. |
| Option | `--member-invariants`, default off, requires `--type-system-refresh`. |

The plan differs from the upstream proposal in these places, on purpose:

1. **The object obligation is stated at `new;`, not as a constructor
   postcondition.**  This is a difference of placement only: the two are one
   proposition, proved once (section 3.4).
2. **The raw type is explicit.**  The proposal suggests an *implicit* datatype
   "defined as `T` without the invariant" for the middle of
   `this.(x := x+1).(y := y+1)`.  Here that type has a name, `raw T`, and one
   rule: constructors and updates produce it.  The chained update is accepted
   (section 3.5).
3. **An invariant may not refer to the set of values of its own type**
   (section 3.3).  Such an invariant defines the type's extension in terms of
   itself, and the definition may have no solution.
4. **More places.**  Newtypes, abstract types and codatatypes are included.
   Codatatypes are checked prefix-wise, because a co-recursive call has no
   termination argument to justify assuming that its result is in the type
   (section 3.8).

## 2. Syntax and placement

### 2.1 Grammar

Two new member declarations, in `ClassMemberDecl` of
`Source/DafnyCore/Dafny.atg`, and one new type form:

```
InvariantDecl = "invariant" { Attribute } Expression(allowLemma: false, allowLambda: true)
WitnessDecl   = [ "ghost" ] "witness" Expression        // value types only, section 3.6
Type          = ... | "raw" NamedType                   // section 3.5
```

`invariant` and `witness` are already keywords, and neither can start a member
today, so the grammar stays LL(1).  The loop-specification production
`InvariantClause` is untouched.  `raw` is contextual: it is the raw-type
operator only where a type is expected and a type name follows, so `raw`
remains usable as an identifier.

- No modifiers.  An invariant is implicitly ghost and instance; `static`,
  `ghost`, `opaque` and `abstract` are errors.
- No `reads`, `requires`, `decreases` or name.  There is deliberately no way to
  write a reads clause: the frame is empty by definition.
- A type may declare several invariants.  They are conjoined in textual order,
  parents' before the type's own.  Each is checked for well-formedness assuming
  the earlier ones, as successive `requires` clauses are.
- Member order does not otherwise matter.

### 2.2 Where it may appear

Wherever members can be declared.

| Declaration | Allowed | Note |
|---|---|---|
| `class` | yes | Checked at `new;` (section 3.4). |
| `trait` (reference or general) | yes | Every extending type inherits it. |
| `datatype` | yes | Section 3.5. |
| `codatatype` | yes | Checked prefix-wise; the raw type is raw at every depth (section 3.8). |
| `newtype` | yes | Its constraint is an invariant; `newtype N = x: B \| P(x)` is sugar (section 3.5). |
| abstract type (`type T { ... }`) | yes | The invariant is over its member functions, and every refinement must honour it (section 4.3). |
| subset type, type synonym | no | They cannot declare members.  A subset type keeps its constraint clause. |
| iterator, tuple, array, default class | no | No member declarations, or no user-controlled construction. |

A module-level `invariant` is an error, like a module-level field.

## 3. Meaning

### 3.1 The invariant is a predicate on raw values

Inside an invariant, `this` is not yet known to be a value of the type: the
invariant is what decides whether it is one.  So `this` has type `raw T`
there, and the invariant is a predicate on raw values:

```
T  =  { v ∈ raw T  |  Inv_T(v) }
```

The invariant may use any member that has empty reads: destructors and
discriminators, `const` fields with or without an initializer, and member
functions.

```dafny
datatype D = D(x: nat, y: nat) {
  predicate Ok() { x <= y }
  invariant Ok()
}
```

One condition makes this sound.  `Ok` is applied to a raw `this`, so `Ok` may
not itself assume the invariant; if it could, the invariant would be defined
in terms of a function that takes the invariant for granted.

**Raw members.**  A member function or constant is a *raw member* of `T` if an
invariant of `T` applies it to `this` or to another value of type `raw T`, or
another raw member of `T` does.

- In a raw member, `this` has type `raw T`.  Its body and specification are
  checked without the invariant.
- A raw member is a member of `raw T` as well as of `T`.  Every other member
  has a receiver of type `T` and is checked assuming the invariant.
- An override of a raw member is raw.  A member inherited from a trait can be
  applied to `this` in an invariant only if it is raw in the trait, since the
  trait's other members, and their overrides, were checked for finished
  receivers.
- Raw-ness is inferred, not declared.  The error for a raw member that needs
  the invariant is reported in the member and names the invariant that uses
  it.  Open question 3 is whether to have an explicit modifier as well.

A raw member may do anything with `this` that is allowed for a value of type
`raw T`.  Using `this` where a `T` is required is a conversion, with the
invariant as its obligation (section 3.5), and in a raw member that obligation
generally cannot be proved.

**Components are finished.**  The components of a raw value are ordinary typed
values.  A component declared with type `E` satisfies `E`'s invariant.  A
component declared with the type being defined, such as the tail of a list,
satisfies the invariant being defined: for an inductive type it is a smaller
value, and the definition is by recursion on structure.  So invariants compose
without a helper predicate over the whole value:

```dafny
datatype SortedList = Nil | Cons(head: int, tail: SortedList) {
  invariant Cons? && tail.Cons? ==> head <= tail.head
}
```

A raw value is raw only at the top.  `raw SortedList` is the set of `Nil` and
`Cons(h, t)` with `t` a `SortedList`.  A declaration that wants a raw
component says so: `Cons(head: int, tail: raw SortedList)`.

A codatatype is the exception.  Its values are not built from smaller ones, so
its raw type is raw at every depth (section 3.8).

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
the raw values that satisfy it.  If the invariant itself mentions the set of
values of the type, by quantifying over it or by testing membership in it,
that sentence is a recursive definition of the set.  This is the same for
every kind of type.  A quantifier over a class type ranges over every reference of the
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

For a class the same invariant is the same ill-formed definition, and now it
fails the same way.  With raw types, a class type is defined from its raw type
by an equivalence too (section 5.1), so a class `Liar` gives inconsistent
axioms exactly as the datatype does.

Quantifying is not the only way to refer to the set.  A membership test does
it one value at a time:

```dafny
datatype Liar2 = Liar2(f: int) {
  predicate No() { !(this is Liar2) }     // 'this' is raw here, so the test is not trivial
  invariant No()
}
```

A value is in the type exactly if it is not.  The raw type is what makes this
expressible: without it, every operand of `is Liar2` was already a `Liar2`.

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

Let `T` be a type with an invariant.  Define:

- **The cone of `T`'s invariant**: the invariant expressions of `T`, its own
  and inherited, and the bodies *and specifications* of every function,
  constant initializer, subset type constraint and newtype constraint
  reachable from them through calls and uses.
- **`T`-dependent types**: `T` (and `T?`); every trait `T` extends and every
  type that extends `T`, since their extensions are defined with `T`'s;
  `object`, if `T` is a reference type; and every type built from one of these
  as a datatype component, a type argument, the base of a subset type or
  newtype, or the argument or result of an arrow; and `raw U` for each such
  `U`.  (Counting `raw U` is conservative.  A raw type depends on the
  invariant only through its components.)

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
5. No test `e is U`, and no conversion of `e` to `U`, explicit or implicit,
   where `U` is `T`-dependent and the static type of `e` is a raw type.  The
   test evaluates the invariant.  The conversion only has it as a proof
   obligation, and is probably harmless; it is excluded until someone shows
   that.  One consequence: a raw member cannot pass `this` as an argument
   where a `T` is expected.

Remarks.

- A *parameter* of type `T` is not a reference to the extension.  The invariant
  may call `f(tail)`, and `f` may assume the invariant of its argument: the
  argument is a component, already a value of the type.
- A type test `e is U` on an operand whose static type is not raw is allowed.
  The operand is a finished value, usually a component, and for it the test
  examines only the variant or the dynamic type.  (If `U` is a subset type,
  its constraint is in the cone.)  The second version of this plan allowed
  every type test.  That was right while there were no raw values, and clause
  5 is the correction.
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

So for clauses 1 to 4 the cone analysis has real work only for a value type
with no reference components, and there clause 4 rejects dynamically bound
calls until there is a way to bound the overrides.  Clause 5 is new work for
every kind of type.

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
non-null values of `C?`.  It holds for parameters, results, bound variables,
heap reads, and for `this` in every member except in the first division of a
constructor, in the invariant, and in raw members.  It does not depend on
allocation.  The values of a class type are all the references of that type,
allocated or not, and each has the invariant.  A function's parameter is not
known to be allocated in any particular heap, and has the invariant all the
same.

A consequence worth having: for `o` of a class or trait type, `o is C` remains
a run-time test of the dynamic type alone, and still yields the invariant.

**Checked.**  Once per constructor.  The obligation is: the object this
constructor builds satisfies the invariant.

There is no choice between checking "at `new;`" and "at constructor exit".
The invariant reads only immutable state: `const` fields, and functions with
no reads.  A `const` is assigned before `new;` or by its initializer, and
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

What `new;` does, in order:

1. The object comes into existence as a value of `raw C`, with the `const`
   values the first division assigned.  A `const` left unassigned is
   arbitrary.
2. The invariant is asserted of it, one assertion per conjunct.
3. `this` is from then on a `C`.

Step 2 evaluates the invariant on the object itself, as a member of `raw C`.
Under the extension condition of section 3.3 the invariant cannot quantify
over `C`, so there is no range for the new object to be left out of.  That is
the naive check of section 3.3, and this design cannot express it.

**A proof with the instance in hand.**  Sometimes the invariant needs a
separate proof, and the proof needs the object.  For that, `new` may carry a
proof block, as `assert` can:

```dafny
constructor (k: nat)
{
  this.k := k;
  new by { EstablishesValid(this); }     // here 'this' has type 'raw C'
  ...
}
```

The block runs between steps 1 and 2.  It is ghost, and in it `this` has type
`raw C`.  It may call lemmas and functions that take a `raw C`.  This is an
addition to what the owner has accepted for `new;`, and it is open question 2.

**The raw type of a class.**  `raw C` is a nameable type, with these rules:

- Its values are the non-null references whose dynamic type is `C`.  `C` is a
  subtype of `raw C`.
- `raw C` is a subtype of `raw P` for each parent trait `P`, and of nothing
  else.  In particular it is not a subtype of `object` or of `P`.  If it were,
  `o is C` could not yield the invariant.
- A test or conversion from a raw reference type to its type, `r is C` or
  `r as C`, evaluates a ghost predicate.  `is` is ghost.  `as`, and the
  implicit conversion, carry a proof obligation and compile to nothing.
- In a verified program every reference that exists at run time satisfies its
  invariant from the moment its constructor reaches `new;`.  A `raw C`
  variable never holds an object that fails the invariant; it holds an object
  the verifier has not been told about.  The model has references that fail
  it.  No constructor ever allocates one.

Other rules:

- Every constructor of the class is checked.  A constructor inherited through
  refinement is checked only against the conjuncts the refinement adds
  (section 4.4).
- A class that has or inherits an invariant must declare a constructor.  This
  extends the existing rule for classes "with fields without known
  initializers" (`Source/DafnyCore/Resolver/ModuleResolver.cs`).
- A constructor with no body is, as today, an assumption.  It now also assumes
  the invariant.  `dafny audit` must say so.
- After `new;` nothing is checked.  A `const` cannot be assigned again: the
  resolver already allows that only in the first division.

Mutable state is deliberately out of reach.  A class that wants an invariant
over mutable fields keeps using a `Valid()` predicate with a `reads` clause.
Nothing in this plan blocks a later, richer discipline for those.

### 3.5 Value types and the raw type

This section covers datatypes and newtypes.  Codatatypes add section 3.8, and
abstract types are in section 4.3.

**The raw type exists so that an unfinished value is a value.**  A proof that
an invariant is established often has to talk about the thing that is about to
satisfy it.  If the only values are finished ones, that proof has no instance
to work with.  The first version of this plan had no raw type and rejected the
chained update for that reason; that argument is withdrawn.

```dafny
datatype D = D(x: nat, y: nat) {
  invariant x == y
  function Inc(): D { this.(x := x + 1).(y := y + 1) }   // the middle value is a 'raw D'
}
```

and the separate proof has its instance (the full example is in section 6.1):

```dafny
var r := Rebuild(t, k);      // r: raw Tree
InsertOrdered(r, t, k);      // a lemma about r, which is not yet a Tree
u := r;                      // checked here, with the lemma's conclusion in hand
```

**Syntax: `raw T`.**  A prefix operator on a named type: `raw D`,
`raw M.D<int>`, `seq<raw D>`.

- It reads as what it is, in signatures and in error messages.
- It is derived, not declared.  There is nothing to publish and nothing to
  keep in step.
- It is not a member name, so it cannot clash with a member or a nested
  declaration, as `T.Raw` could.
- It does not take a suffix, so it composes with `?`: `raw C?`.
- It is contextual, so it takes no identifier away from existing programs.

`raw` applies to any type that can declare an invariant.  Where the type has
none, `raw T` has the same values as `T` and the conversion has no obligation.
That matters for an abstract type, which may gain an invariant in a
refinement.  `raw` does not apply to a type parameter.

**The rules.**

- `T` is a subtype of `raw T`.  They have the same pre-type, so they are
  comparable with `==`.
- A constructor application `K(e)` has type `raw D`.  So does an update
  `d.(f := e)`, whether `d` is a `D` or a `raw D`.
- Destructors, discriminators, `match`, update and the raw members work on
  `raw D`.  Every other member needs a `D`.
- **Conversion.**  A raw value may be used wherever a `T` is expected: an
  assignment to a variable or field of type `T`, an argument, a result, a
  constructor argument.  The verifier then checks the invariant.  This is the
  rule a subset type has, for example where an `int` is used as a `nat`.
  `e as T` is the explicit form, and `e is T` is the ghost test.
- Collections follow the subset-type rule too: a `seq<raw D>` is accepted as a
  `seq<D>` with the obligation that every element satisfies the invariant.
- Type inference follows the subset-type rule: an unannotated
  `var r := K(e)` has type `raw D`, as `var n := 5` has type `int`.  A
  declared type, a parameter or a result type makes it a `D`.

So a value is checked once, where it first becomes a `D`, and never again
(section 4.4).  Where nothing is annotated, that is the point of use.

**The type system.**  Under `--type-system-refresh`, a pre-type is a type
without subset-type constraints, and the verifier enforces the difference
(`docs/dev/TypeSystemRefresh.md`).  `raw T` is the pre-type of `T`, given a
name.  Resolution infers pre-types as it does today.  The conversion check is
the one the verifier already makes for subset types.

**Newtypes.**  The owner's ruling is that the "such that" constraint goes away
in favour of the member invariant.

```dafny
newtype Even = int {
  invariant this as int % 2 == 0
  witness 0
}
```

- `raw N` is the newtype over the same base with no constraint.  `this` in the
  invariant has that type, and `this as B` gives the base value.
- **Sugar.**  `newtype N = x: B | P(x) witness w { members }` means
  `newtype N = B { invariant P(this as B)  witness w  members }`.  `witness *`
  means no `witness` member.  Both forms are accepted during migration, and
  they mean the same thing, so the change of syntax changes no verdict.
- A newtype's operators keep their present rule: the result of `a + b` on `N`
  must be an `N`, checked at the operation.  That rule is what lets a newtype
  have a native representation, and the plan does not change it.  A raw value
  arises as `this` in the invariant, or by `e as raw N`.
- A refinement may add conjuncts to a newtype, as to any other type.
- Detecting a native representation from the shape of the constraint keeps
  working through the sugar.  How it reads a general conjunction is a detail
  for stage 6.

**Subset types** are not changed.  They cannot declare members, so they cannot
declare an invariant.  A type with an invariant is, in meaning, the subset
type of its raw type with the invariant as constraint.  What is new is that
it is one declaration, it carries members, it can be recursive in itself, and
it can be extended and refined.

### 3.6 Inhabitance and witnesses

**Reference types.**  Nothing changes.  A non-null reference type is already
never auto-initialized: a variable of the type must be assigned before use, and
an object exists only if a constructor returned it.  A class whose invariant is
unsatisfiable is merely a class no verified constructor can finish.  The same
holds for `raw C`.

**Value types.**  Today a datatype is inhabited if it has a grounding
constructor, a syntactic fact.  An invariant can empty it:

```dafny
datatype Empty = Mk(n: nat) { invariant n < 0 }
```

`raw Empty` is inhabited, by the old syntactic rule.  `Empty` is not.  So a
value type that has an invariant is **possibly empty** unless it declares a
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

- The witness is a closed expression, used at the type.  Checking it is the
  ordinary conversion check.
- With no witness, the type behaves as a subset type with `witness *` does.
  Variables need definite assignment, and the type cannot instantiate a `(0)`
  or `(00)` type parameter.  All of that machinery exists and is driven by
  `Type.GetAutoInit` (`Source/DafnyCore/AST/Types/Types.cs`).  The work is to
  make `GetAutoInit` answer for such types, and to audit every place that asks
  "is this a `SubsetTypeDecl`" where it should ask `GetAutoInit`.
- Emptiness propagates.  A datatype whose only grounding constructor has a
  possibly-empty component is possibly empty, as it is today for components of
  possibly-empty subset types.  The grounding of `raw D` treats a component of
  type `D` as possibly empty unless `D` has a witness.
- A newtype written in the old syntax keeps the old meaning of its witness
  clause, including the default when the clause is absent (section 3.5).
- An abstract type states its characteristics in its header, as today.  A
  refinement must meet them, and now needs a witness to do so (section 4.3).
- At most one witness per type in a module.  A refinement may supply or replace
  it (section 4.2).

### 3.7 What replaces the subset-type workaround

The workaround in the issue is

```dafny
type T = t: T_ | t.i() witness *
datatype T_ = ... { predicate i() ... }
```

The design keeps the two types, because both are needed, and removes the
second declaration.

| | Workaround | Member invariant |
|---|---|---|
| Declarations | two, with a published relation | one; `raw T` is derived |
| `requires i()` / `ensures i()` in members | on every member | none: `this` is a `T` in every member that is not raw |
| Members | belong to `T_`; `T` has none of its own | belong to `T`; the raw members also to `raw T` |
| Checked | at each `T_`-to-`T` conversion | at each `raw T`-to-`T` conversion |
| Quantify over `T` in a member of `T_` | rejected: "recursive constraint dependency" | accepted, outside the cone |
| Refine or extend | `T_` can be, `T` cannot, and the pair must be kept in step by hand | the one type can be, and the raw type follows |
| Strengthen in a refinement | a predicate with an ever-stronger postcondition | add an `invariant` |
| Recursion | `T_`'s components can only be `T_`: a component of type `T` is rejected as a recursive constraint dependency.  An invariant on every node is a recursive predicate over the whole value | components are finished unless declared raw (for a codatatype, section 3.8) |
| Intermediate invalid values | have type `T_` | have type `raw T` |

**Quantifying over the type.**  Today the resolver puts the subset type and its
constraint in one call-graph component with everything the constraint reaches
(`CheckForCyclesAmongRedirectingTypes` in
`Source/DafnyCore/Resolver/ModuleResolver.cs`), and reports any cycle.  The
members of `T_` mention `T`, and the constraint of `T` calls a member of `T_`,
so the cycle is easy to close.

With member invariants the dependency is narrower and exact.  The only thing
that refers to the *extension* of a type `D` is an unbounded binder over a
`D`-dependent type, or a test of a raw value against one.  The only thing that
must not refer to it is the cone of `D`'s invariant (the extension condition,
section 3.3).  So, for a value type with no reference components:

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

### 3.8 Codatatypes

A codatatype may declare an invariant.  Sections 3.1 to 3.7 apply to it, with
two changes.  Both come from one fact: a value of a codatatype can be a
component of itself.

- A co-recursive function is checked prefix-wise, as a greatest lemma is.
- The raw type of a codatatype is raw at every depth, not only at the top.

**What the type means.**  For an inductive type, "the raw values whose
components are in their types and which satisfy the invariant" is a definition
by recursion on structure.  For a codatatype there is no structure to recurse
on, and the type is the *greatest* set that fits:

```
T  =  the greatest X such that every K(a) in X has  a in types[T := X]  and  Inv_K(a)
```

`types[T := X]` are the declared component types with `X` in place of `T`.
The invariant does not depend on `X`, by the extension condition and by the
rule on raw types below.  So the operator is monotone and the greatest fixed
point exists.  This reading is not a choice.  It is what a codatatype without
an invariant already means.

**The problem is a circular assumption.**

```dafny
codatatype S = Cons(head: int, tail: S) { invariant false }
function F(): S { Cons(1, F()) }
```

The check at `Cons(1, F())` must prove the invariant of the new node.  If it
may assume that `F()` is an `S`, it assumes `false`, because no `S` exists,
and it passes.  `S` is then both empty and inhabited.

An inductive type does not have the problem, and the reason is termination.
A recursive call must decrease a measure.  "The result of the recursive call
is in `T`" is an induction hypothesis: it is about a smaller argument, and the
smallest arguments get no help from it.  A co-recursive call decreases
nothing.  The assumption that its result is in `T` is the statement being
proved, about the value being defined.

What the invariant says does not matter.  `false` shows it, and so does any
invariant that follows from the tail's invariant: with "a zero occurs
somewhere", a zero at position `n` of `Ones()` is a zero at position `n + 1`
of `Cons(1, Ones())`.  So no restriction on the form of the invariant is the
fix.  The assumption is what has to change.

**Prefixes.**  The greatest fixed point is reached from above:

```
T#0      =  raw T
T#(k+1)  =  { K(a)  |  a in types[T := T#k]  and  Inv_K(a) }
T        =  the intersection of the T#k
```

`T#k` is the set of raw values whose top `k` layers satisfy the invariant.
Natural numbers are enough.  A component is in `types[T := X]` when *every*
`T`-typed value inside it is in `X`, so the intersection of the `T#k` is
closed under the operator.  Greatest predicates need ordinals because a body
may put the recursive call under an existential quantifier.  The component
types of a codatatype cannot.

**The rule.**  The functions of a co-recursive cluster are checked as a
greatest lemma is:

> For an arbitrary `k`, assume that the result of every co-recursive call is
> in `T#k`.  Prove that the value of each body is in `T#(k+1)`.

By induction on `k`, every result is in every `T#k`, and so in `T`.  Said
without `k`: the assumption that a co-recursive call returns a `T` must be
guarded.  It is used once, to give its type to the component in which the
call sits.  It is not available for proving an invariant.

**What the rule comes to.**  Take a node `Cons(e, G(x))` in a body.

- Its tail is in `T#k`.  That is the assumption.
- Its invariant must be proved for every `k`, including `k = 0`, where the
  assumption says only that the tail is in `raw T`.  The invariant is one
  predicate, not a family indexed by `k`.  So the case `k = 0` is the whole
  obligation:

  > The invariant of a node built over a co-recursive call is proved knowing
  > of the call's result only that it is in `raw T`.

- A node inside a node is one level up.  In `Cons(a, Cons(b, G(x)))` the inner
  node is in `T#(k+1)` once its invariant is proved, and the outer one is then
  in `T#(k+2)`.  The proof of the outer invariant may use the inner invariant,
  which has just been checked, and nothing about `G(x)`.  The level counts the
  layers that have been checked.

```dafny
codatatype Zs = Cons(head: int, tail: Zs) {
  function Nth(n: nat): int decreases n { if n == 0 then head else tail.Nth(n - 1) }
  invariant exists n: nat :: Nth(n) == 0            // every suffix has a zero
}
function Zeros(): Zs { Cons(0, Zeros()) }           // accepted: n = 0
function Alt(): Zs { Cons(1, Cons(0, Alt())) }      // accepted: n = 0 for the inner node, n = 1 for the outer
function Ones(): Zs { Cons(1, Ones()) }             // error: invariant might not hold
function F(): S { Cons(1, F()) }                    // error: invariant might not hold
```

`Ones` fails because a zero must be exhibited in `Cons(1, Ones())`, and
nothing is known of `Ones()` but its raw type.  `F` fails because `false` must
be proved.  Both type declarations are accepted.  `Zs` is the type of streams
with infinitely many zeros, and `S` is an empty type.

**The raw type of a codatatype is raw at every depth.**  `T#0` has to be a
type that exists before any invariant is known to hold.  The raw type of
section 3.1 is not that: its components are finished.  For an inductive type,
"the components are finished" is recursion on structure.  For a codatatype a
component may be the value itself, and "the components are finished" is the
assumption the rule forbids.  So, for a codatatype:

- `raw T` is the declaration with its invariant removed and every
  co-recursive component read as raw.  It is the type the codatatype would be
  without the invariant.  A co-recursive component is one whose type depends
  on a codatatype in the same recursive cluster of declarations.  An inductive
  datatype in such a cluster is read the same way.
- In the invariant and in raw members, `this.tail` has type `raw T`.  They are
  checked without the invariant at any depth.  A member applied to a receiver
  of type `raw T` is a raw member, as in section 3.1.
- A constructor accepts raw co-recursive components and yields a raw value.
  Using the value at type `T` proves membership: the invariant of the node,
  and that each co-recursive component is a `T`.  The second part is immediate
  for a component whose static type is `T`.

The rule is needed, not only tidy.  With the raw type of section 3.1:

```dafny
codatatype Zs = Cons(head: int, tail: Zs) {
  function Nth(n: nat): int decreases n { if n == 0 then head else tail.Nth(n - 1) }
  predicate Q() ensures Q() { exists n: nat :: Nth(n) == 0 }
  invariant Q()
}
function Ones(): Zs { Cons(1, Ones()) }
```

`Q` is a raw member.  If its `this` had a finished tail, the postcondition
would be provable: the tail has a zero, so `this` has one.  The check at
`Cons(1, Ones())` would then pass by the postcondition of `Q`, with nothing
assumed about `Ones()` directly.  The assumption that the tail is finished has
moved into `Q`.  With a tail of type `raw Zs` the postcondition of `Q` cannot
be proved, and the error is reported there.

The cost is that the invariant of a codatatype cannot call an ordinary member
on a co-recursive component, or lean on that component's invariant to be well
formed.  It can read as deep as it likes: `invariant head <= tail.head` is
allowed.

**Where `raw T` for a co-recursive call comes from.**  "The result of the
co-recursive call is in `raw T`" is itself a coinductive fact.  `raw T` is a
greatest fixed point too, and its components have types.  It may be assumed
because it is established first and separately, by the check Dafny makes
today, in which nothing is assumed of a co-recursive call.  So a node has two
kinds of obligation, and they see different things:

| Obligation at a node | May assume, of the result of a co-recursive call |
|---|---|
| Each component has its declared type (today) | Nothing |
| The node's invariant (new) | That it is in `raw T` |

The second row assumes what the first row concludes.  Neither assumes that the
result is in `T`.  The two must be kept apart in the verification condition.
If the raw type of a co-recursive call's result reached the obligations of the
first row, a field whose type is empty would discharge them, which is
https://github.com/erniecohen/dafny/issues/143 again.  Control `V7c` of
section 6.4 is the test.

**What Dafny does today, and whether reusing it is enough.**  The pinned fork
treats a self-call under a constructor in three ways, depending on what the
function promises about its result.

1. *The function has an `ensures` clause.*  The call is not co-recursive
   (`NoBecauseFunctionHasPostcondition` in
   `Source/DafnyCore/Resolver/CoCallResolution.cs`).  It is an ordinary
   recursive call and must terminate: "cannot prove termination ... (note that
   only functions without any ensures clause can be called co-recursively)".
   The source gives `ensures false` as the reason, which is the circular
   assumption above.
2. *The result type is a constrained newtype over a codatatype*, under
   `--extended-newtype-bases`.  The same demotion
   (`NoBecauseFunctionHasConstrainedReturnType`).
3. *The result type is a subset type over a codatatype.*  The call is not
   demoted.  It stays co-recursive and falls under the rule that fixed
   https://github.com/erniecohen/dafny/issues/143
   (https://github.com/erniecohen/dafny/pull/159).  A guarded co-recursive
   call is a *suspended value*.  The verifier does not assume `canCall` for
   it, so it has neither the call's type nor its definition.  When a suspended
   value is observed, by a destructor, a `match`, a `let`, or as an argument
   to an ordinary function, its membership is asserted and never assumed.
   The effect on a subset-type result is that the function is rejected
   whatever the constraint says:

   ```dafny
   codatatype St = Cons(head: int, tail: St)
   type Any = s: St | true witness *
   function Plain(): St { Cons(1, Plain()) }      // accepted
   function Triv(): Any { Cons(1, Triv()) }       // error: value does not satisfy the subset constraints of 'Any'
   ```

   The check of the result asks for the whole node to be in the base type.
   That includes the co-recursive call's result, and nothing can show it.

Also, the recursive form of the workaround cannot be written today.  A
datatype or codatatype with a component whose type is a subset type over
itself is rejected: "recursive constraint dependency involving a subset type".
So the only constraint a co-recursively built node carries today is the type
of a field, and issue 143 was about exactly that.

Is reusing these rules enough?

- *Demotion* (1 and 2) is sound and gives up too much.  A type with an
  invariant puts a postcondition on every function that returns it.  If each
  such function had to terminate, no value of the type could be built
  co-recursively, and a type like `Zs` would have no values that a program
  can produce.
- *The suspended-value rule* (3) is sound.  Applied unchanged to a type with
  an invariant, it would reject every co-recursive function whose result is
  `T`, for the reason it rejects `Triv`.  It never grants the one guarded use
  of the assumption: that the call's result is in `T` as the component it is
  placed in.
- The suspended-value rule is the right base.  It is the first row of the
  table above, already implemented and tested.  The plan adds the second row.
  Where a node over suspended values is used at type `T`, the verifier does
  not ask for the whole node to be in `T`.  It asks that the components that
  are not suspended have their finished types, and that the node's invariant
  holds given that the suspended calls are raw.
- The rule on raw types has no counterpart today and is needed in addition.
  Today a constraint cannot call a function over the type it constrains,
  because of the cycle check, so nothing can carry an assumption about a
  component into the check of a node.  A raw member can.

The same prefix reading would let a postcondition on a co-recursive function
be proved instead of forbidden.  That is a separate proposal.

**Looking into the result of a co-recursive call.**

```dafny
codatatype Sorted = Cons(head: int, tail: Sorted) { invariant head <= tail.head }
function Up(n: int): Sorted { Cons(n, Up(n + 1)) }
```

The obligation is `n <= Up(n + 1).head`.  It needs a fact about the *value*
of the co-recursive call, not about its type.  The defining equation of `Up`
supplies it, and using the equation is sound: a guarded definition has a
solution whatever the types say.  Today's rule withholds the equation
together with the type, because one antecedent guards both in the encoding.
Until they are separated, `Up` is rejected.  Separating them is listed with
the relaxations (section 7.2).  It is all that an invariant of bounded depth
needs.

**Proofs that are not one step.**  The check above is the proof that a
function body can carry: one step for each node.  Membership that needs a
coinductive argument of its own, with a stronger hypothesis or another lemma,
would be proved by a greatest lemma that concludes `r is T` for an
`r: raw T`, with `T#k` as the prefix predicate.  That is not designed here.
It is listed with the relaxations.

Everything else about a codatatype is as for a datatype: conversion checks
the invariant, and a witness makes the type nonempty.

## 4. Inheritance and refinement

### 4.1 `extends`

A type that extends a trait `P` inherits `P`'s invariants, instantiated at the
parent type arguments.  Its invariant is the conjunction of all its ancestors'
invariants and its own.

- **The raw type follows.**  `raw C` is a subtype of `raw P`, and `C` of `P`.
  Nothing is declared or published to make that so.
- **Raw members.**  An invariant of `C` may apply to `this` only those
  inherited members that are raw in `P` (section 3.1).

- **Only strengthened, by construction.**  There is no syntax to drop, replace
  or weaken an inherited invariant.  The conjunction implies each conjunct, so
  there is nothing to check about the invariants themselves.
- **Checked soundly at construction.**  A value of a trait type is always a
  value of some class or value type.  That type's check asserts the whole
  conjunction, ancestors first, each ancestor once (section 4.4).  So
  everything that reaches type `P` satisfies `P`'s invariant.
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

In `module M1 refines M0`, a refining declaration of any type that can have an
invariant may add `invariant` members.  The raw type is refined with it:
`M1`'s `raw T` is the raw type of `M1.T`, with nothing written.  The refined type's invariant is the
inherited conjunction followed by the new conjuncts.  Again there is no syntax
to weaken.

Soundness rests on two facts about refinement in Dafny.  `M1.T` is a different
type from `M0.T`.  And `M1` is translated as a whole, inherited bodies
included, but an obligation that was inherited unchanged is not proved again:
the translator turns it into an assumption unless the refinement changed the
body it sits in (`IsInherited` and `MustReverify`; inherited postconditions
are guarded by `$_reverifyPost` in the same way).  Member invariants follow
that pattern, conjunct by conjunct:

- at every inherited constructor's `new;`, and at every inherited conversion
  of a raw value to its type, the conjuncts **added in `M1`** are
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
  given a body to a function in the cone;
- an added invariant may apply to `this` a member that was not raw in `M0`.
  The member becomes raw in `M1` and is verified again there, without the
  invariant.  This is the one case where an added invariant causes an
  inherited body to be re-verified.  Where `M0.T` had no invariant at all,
  nothing in the member can have relied on one, but the check is still made.

If an inherited constructor does not establish an added conjunct, the
refinement is wrong, and the error says so, at the inherited `new;`, reported
in `M1`.  A test must pin this before stage 3 relies on it, together with its
converse: an inherited constructor whose inherited conjunct was expensive to
prove costs nothing in `M1`.

Two points follow from the same fact.

- **Value types.**  Strengthening a value type's invariant strengthens the
  obligation at every conversion site, which resembles adding a `requires`,
  something refinement otherwise forbids.  It is sound here only because the
  added conjuncts are checked at every inherited site.  A module with an
  abstract import `import A : M0` builds `A.T` values against `M0`'s
  invariant; when it is refined with `import A = M1`, its conversion sites
  are inherited sites and get the added conjuncts as obligations.
- **Witnesses.**  A stronger invariant can invalidate the witness.  The
  inherited `witness` is checked in `M1` against the added conjuncts only.
  The refining declaration may
  declare a new one, which replaces it.

### 4.3 Abstract types

An abstract type may declare members, so it may declare an invariant.  The
invariant can only be about its member functions:

```dafny
module Spec {
  type T {
    function Size(): int
    invariant Size() > 0
  }
  lemma Positive(t: T) ensures t.Size() > 0 { }      // from the type
}
module Impl refines Spec {
  datatype T = Mk(n: int) {
    function Size(): int { n }
    // inherited: invariant Size() > 0.   So T is { Mk(n) | n > 0 }.
  }
}
```

- **In the abstract module**, `raw T` is an opaque type, `T` is the part of it
  where the invariant holds, and `Size` is a raw member with no body.  Clients
  may assume the invariant of every `T`.  Nothing constructs a `T` there.
- **Every refinement honours it.**  The refining declaration inherits the
  invariant as a conjunct, by the rule of section 4.2.  It is not an
  obligation the refinement proves once.  It is part of what the refined type
  is, and it is checked wherever the refinement makes a value.
- **What may refine it.**  A declaration that can carry an invariant: a
  datatype, codatatype, newtype, class or trait, or another abstract type.  A
  type synonym or a subset type cannot, because it has no members.  Refining
  an abstract type that has an invariant with one of those is an error.
- **Raw members stay raw.**  `Size` is applied to a raw `this` in the abstract
  module, so the body the refinement gives it is checked without the
  invariant.
- **Characteristics.**  `type T(0) { invariant ... }` promises a compilable
  value.  The refinement must supply a `witness`, and the existing check that
  a refining type has the declared characteristics is where that is enforced.
  An abstract type with an invariant and no `(0)` or `(00)` is possibly empty.
- **The extension condition** is checked in the abstract module against what
  is visible there, the specifications of the bodiless functions, and again in
  each refinement, against the bodies.
- **A codatatype as the refinement** reads `raw` as section 3.8 does.  The
  bodies it gives to the abstract type's raw members are checked with raw
  co-recursive components.  Nothing in the abstract module depends on the
  difference, because an abstract type shows no components.

An abstract type with no invariant may be refined by a type that has one, as
before.

### 4.4 Every obligation is proved once

An invariant creates proof work in exactly one kind of place: where a raw
value becomes a value of its type.  An obligation is identified by a pair,
*(site, conjunct declaration)*, and each pair is proved once in the program.
Everything else is an assumption, which costs no proof.

- **Objects.**  One assertion per conjunct per constructor, at `new;`.  Not
  again at exit (section 3.4), not at `new C(...)`, not at method boundaries,
  not on use.
- **Value types.**  One assertion per conjunct per conversion site.  Nothing
  when a `T` is matched, passed, returned or stored.  A constructor
  application that is used directly at type `T` is one site, as before.
- **Raw variables.**  A value held in a `raw T` variable and used at type `T`
  in two places has two sites.  Along one path the first assertion makes the
  second immediate, so there is no second proof.  Declaring the variable as
  `T` makes it one site.
- **Updates.**  For `d.(f := e)` with `d` a `T`, used at type `T`: a conjunct
  that reads only components other than `f`, and applies no raw member to
  `this`, is not asserted.  It holds because `d` has the type.
- **Trivial conjuncts.**  A conjunct guarded by another constructor's
  discriminator is not emitted for the constructor at hand.
- **Ancestors, once each.**  A type's invariant is a *set* of conjuncts, each
  keyed by the type that declares it.  The set is collected over the set of
  ancestors, not over the paths to them.  If `C extends A, B` and both extend
  `P`, a constructor of `C` proves `P`'s conjuncts once.  What cannot be
  shared is across classes: each class that extends `P` proves `P`'s conjuncts
  for its own constructors, because a trait constructs nothing and the values
  differ.
- **Refinement.**  Inherited conjuncts at inherited, unchanged sites are not
  re-proved (section 4.2).  Only added conjuncts and new or changed sites
  carry obligations.  The inherited witness is checked against added conjuncts
  only.
- **Well-formedness.**  Each conjunct is checked for well-formedness once, in
  the type that declares it, assuming the ancestors' conjuncts.  Not in
  extending types, not in refinements that leave it alone, not at conversion
  sites.
- **Raw members.**  A raw member is verified once, with a raw receiver.  It is
  not verified a second time for finished receivers; a finished value is a
  raw value.
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
today.  A type with no invariant has no separate raw type in the encoding:
`raw T` and `T` are one Boogie type.

The pattern is the one Dafny already uses for `C` and `C?`: the declaration
denotes the larger type, and the smaller one is defined from it by a
constraint.  For every type `T<G>` with an invariant:

```
const Tclass.T.raw(G): Ty;                    // the raw type
function T#Inv(G, v): bool;                   // the type's own conjuncts, of a raw value

// definition, guarded by the raw type
axiom (forall G, v ::  { T#Inv(G, v) }
  $Is(v, Tclass.T.raw(G))  ==>  (T#Inv(G, v)  <==>  ⟦own conjuncts of T⟧(G, v)));

// the type is the raw type restricted by the invariant
axiom (forall G, v ::  { $Is(v, Tclass.T(G)) }
  $Is(v, Tclass.T(G))  <==>  $Is(v, Tclass.T.raw(G)) ∧ T#Inv(G, v) ∧ P₁#Inv(…, v) ∧ …);
```

where `P₁ …` are the distinct ancestors that declare invariants.  The raw type
gets the axioms the type has today:

- **Datatype.**  For each constructor,
  `$Is(#D.K(a), Tclass.D.raw(G)) <==> $Is(a₁, T₁) ∧ … ∧ $Is(aₙ, Tₙ)`, where a
  component declared with type `D` has `Tᵢ = Tclass.D(G)`, the finished type.
- **Codatatype.**  The same axiom, except that a co-recursive component has
  the raw type, `Tᵢ = Tclass.D.raw(G)` (section 3.8).  The equivalence above
  would then constrain only the top node, so for a codatatype it is stated for
  each constructor instead:
  `$Is(#D.K(a), Tclass.D(G)) <==> $Is(a₁, T₁) ∧ … ∧ $Is(aₙ, Tₙ) ∧ D#Inv(G, #D.K(a)) ∧ …`
  with the finished component types, together with
  `$Is(v, Tclass.D(G)) ==> $Is(v, Tclass.D.raw(G))`.  These are the equations
  of a fixed point.  They do not let the prover conclude that a co-recursively
  built value is in the type.  The check of section 3.8 does that.
- **Class.**  `$Is(o, Tclass.C.raw?(G)) <==> o == null ∨ dtype(o) == Tclass.C?(G)`.
  The finished type is
  `$Is(o, Tclass.C?(G)) <==> o == null ∨ (dtype(o) == Tclass.C?(G) ∧ invariants)`.
- **Trait.**  As for a class, with `implements$P(dtype(o), G)` in place of the
  equation.
- **Newtype.**  The raw type is the base type's representation with no
  constraint.  The finished type's axiom is the one a constrained newtype has
  today, with the invariant as the constraint.  For a newtype in the old
  syntax nothing changes at all.
- **Abstract type.**  `Tclass.T.raw` is an uninterpreted type, as `Tclass.T`
  is today.

Functions.  A member function's axioms are guarded by the type of its
receiver, as now.  For an ordinary member that is `Tclass.T`.  For a raw
member it is `Tclass.T.raw`.  So a member that is not raw is unconstrained on
raw values outside the type, and an invariant cannot use it there.

Obligations.

- **Conversion.**  Where an expression of type `raw T` is used at type `T`:
  one assertion per conjunct, own and inherited, of the invariant applied to
  the value.  This is the place the subset-type check sits today
  (`CheckSubrange`).  For a codatatype the co-recursive components must also
  be shown to have the finished type, which is immediate when that is their
  static type.
- **`new;`.**  In the translation of `DividedBlockStmt`
  (`Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs`), the
  first division runs on local surrogates of the fields, and `new;` then
  assumes that `this` is a `C?` and that each field equals its surrogate.
  With an invariant, `new;` assumes only the *raw* type, then asserts each
  conjunct of the invariant of `this`.  The finished type then follows from
  the equivalence above.  There is no assumption of the finished type to
  place before or after the check.
- **Co-recursive calls** (section 3.8).  As today, `canCall` is not assumed
  for a guarded co-recursive call, so neither its type nor its definition is
  available in the body.  Where a constructor application over such calls is
  used at a type `T` with an invariant, the verifier does not assert `$Is` of
  the whole value at `T`.  It takes the nested constructor applications from
  the innermost outward.  For each node it asserts that the components holding
  no suspended call have their finished types.  Then it asserts `T#Inv` of the
  node, in a scope that assumes the raw type of the suspended calls and the
  invariants of the inner nodes.  That assumption does not leave the scope.
  Outside the cluster, the function's consequence axiom gives the result its
  type, as for any function.

### 5.2 Why it is consistent

Fix a program that meets the reads restrictions, the extension condition, the
raw-member rule, and for codatatypes the rule that the raw type is raw at
every depth.  Build a model.

**The raw types** are what the types are today.  Raw datatype values are
constructor applications to components in their types.  Raw codatatype values
are the values of the codatatype declared without its invariant.  Raw
references of dynamic type `C` are all references with that dynamic type, with
every combination of `const` values in the constants' types.  Raw newtype
values are the base values.

**The invariants are fixed predicates of raw values.**  The cone of `T`'s
invariant does not refer to the extension of `T` or of a type that depends on
it, by quantifying (clauses 1 to 4) or by testing a raw value (clause 5).
What it applies to a raw `this` is a raw member, defined on all raw values
without reference to the invariant.  What it applies to a component is
applied to a value that is already in its type.  For a codatatype a
co-recursive component is itself raw, and only raw members are applied to it.

**The types.**  Order the types so that each comes after the types whose
extensions its cone refers to.

- *Datatypes.*  `K(a) ∈ D ⟺ a ∈ types ∧ Inv(K(a))`, by recursion on the
  structure of values.  Datatype values are well-founded, which Dafny's rank
  axioms already require, so there is exactly one solution.
- *Classes and traits.*  A component of a reference may be a reference of the
  same class.  Build the references by stages, each stage using finished
  references of earlier stages as `const` values, so that no reference is its
  own component.  Then `o ∈ C ⟺ dtype(o) = C ∧ Inv(o)` is a definition by
  recursion on the stage.
- *Codatatypes.*  The greatest fixed point of section 3.8.  It exists because
  the invariant is a fixed predicate of raw values, which the rule on raw
  types secures.  It satisfies the equations of section 5.1, since a greatest
  fixed point is a fixed point.
- *Newtypes and abstract types.*  A subset of the raw values, with nothing
  recursive about it.

Every emitted axiom is one of: an axiom Dafny emits today, now about the raw
type; the definition of a new function symbol; or the equivalence that the
construction above satisfies.

**Unsatisfiable invariants.**  If no raw value satisfies the invariant, the
type is the empty set.  That is a model, and it is sound: `invariant false`
gives a type with no values, not an inconsistency.  Nothing else in the
emitted Boogie says a value of the type exists; see the hazards below.

**`new` needs no side condition.**  In the second version of this plan the
references of a class were only those satisfying the invariant, so `new`
could find a reference only if the check had passed, and the order of the
check and the assumption carried the soundness.  Now a raw reference exists
for every combination of constants.  `new;` assumes only that, which is what
`new` assumes today, and the invariant is asserted of that reference.  This
is also why the instance can be handed to a lemma before the check
(section 3.4): it exists.

**Why `o is C` is still a dynamic type test.**  In the model, `object` and
the trait types contain raw references that fail their class's invariant, and
there `o is C` is false although the dynamic type matches.  At run time there
are no such references outside a first division.  A raw reference type is not
a subtype of `object` or of a finished trait type, so the only way a
reference reaches one is as a finished value, and it became finished at a
`new;` that was checked.  On every state that occurs at run time, the logical
test and the dynamic type test agree.  The verifier reasons about more states
than occur; that is a loss of completeness in no program anyone can write.

**What the extension condition buys.**  Without it there may be no model at
all: for `Liar` and `Liar2` of section 3.3 no set satisfies the equivalence.

**Codatatypes.**  The model shows the axioms are consistent.  It does not
show that the value of a co-recursive function is in its type.  That is the
induction of section 3.8, and it has two steps.

1. By today's rule, every value such a function denotes is in its raw type.
   Nothing about a co-recursive call was assumed to show it.
2. Fix `k`, and suppose the result of every co-recursive call is in `T#k`.
   Take a node of a body.  Its components that are not suspended are in their
   finished types, as checked.  Its suspended components are in `T#k`: the
   calls by the supposition, and the inner nodes because they are in
   `T#(k+1)` or higher and the `T#k` decrease.  Its invariant was proved from
   the raw types of the calls, which step 1 supplies, and from the invariants
   of the inner nodes.  So the node is in `T#(k+1)`.

The argument fails if an obligation of step 1 can see the assumption that
step 2 makes.  That is why the two are kept apart.

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
| Local or out-parameter of a value type with an invariant, used before assignment | Possibly-empty characteristic (section 3.6); definite-assignment rules that already exist |
| Class field of such a type | Existing rule: the class must have a constructor that assigns it |
| Type argument for `(0)` or `(00)` | Characteristic check |
| Enclosing datatype believed inhabited through a component | `GetAutoInit` propagation |
| Result of a co-recursive call assumed to be in its type | The prefix-wise check (section 3.8), on top of the suspended-value rule the fork already has |
| A raw member of a codatatype assuming that a co-recursive component is finished | The raw type of a codatatype is raw at every depth (section 3.8) |
| Abstract type with an invariant, refined by a type that cannot carry it | Rejected (section 4.3) |
| Bodiless function or method returning the type | An assumption today, for any possibly-empty type; unchanged, and it should appear in `dafny audit` |
| `new C` on a class with no constructor | Rejected: the class must declare one |
| Extern or bodiless constructors, extern datatype values | Assumptions; reported by `dafny audit` |
| Any place the translator branches on `SubsetTypeDecl` to decide emptiness | An audit, as a stage-2 task with its own tests |

A parameter of a possibly-empty type is not a hazard.  A method that takes an
`Empty` is never called.

**A raw value taken for a finished one.**  The new hazard that raw types
bring.  It is closed by typing: `T` is a subtype of `raw T` and not the
reverse, and every use of a raw value at type `T` is a checked conversion.
The places to audit are the ones where the translator produces a type on its
own account: the type of a constructor application, of an update, of `this`
in an invariant and in a raw member, of a `match` scrutinee's bound
variables, and the receiver guard of function axioms.

**A raw reference escaping.**  A `raw C` must not reach `object`, a finished
trait type, a `modifies` or `reads` frame element typed `object`, or a
collection of those, without a conversion.  The subtyping rule of section 3.4
is the whole defence, so it needs its own tests, including through type
parameters: `raw C` may instantiate a type parameter, and a `G` is not an
`object`.

**Mutable objects whose invariants read fields.**  Prevented by the reads
restriction, not managed.  An invariant reads only immutable state.  The
specific risks:

- A `const` assigned after `new;`: already an error.
- A `const` that is a reference to a mutable object (`const buf: array<int>`):
  the invariant can speak of the reference and its immutable parts
  (`buf.Length`), never its contents.
- `this` escaping before the check: already impossible in the first division.
  In a `new by` block it can reach only what accepts a `raw C`.
- A constructor that calls a method on `this` after `new;` while it "is still
  setting things up": the object already satisfies its invariant, and the
  mutable state the method sees is not the invariant's business.
- An invariant in a trait, about a `const` a class assigns: the class's `new;`
  checks it.  Consts cannot be overridden.

**Two-state reasoning.**  An invariant has no heap argument, so `old(inv)` is
`inv`.  Frame axioms, `unchanged`, `modifies` and heap succession never mention
it.  Inside `new;` the implementation must assume the raw type and nothing
more before the assertion.  A regression test pins that: if `new;` assumed the
finished type, as it does today, the test's bad constructor would verify.

The invariant holds for unallocated references of the type as well.  This is
deliberate.  A function has no way to know its argument is allocated, and the
invariant must be usable in functions.  It is sound because the model fixes
the constants of all references at once, in the way `const` fields are already
modelled.

**Reference to the type's own extension.**  Section 3.3.  It makes the meaning
of the type a recursive definition, which may have no solution.  The extension
condition excludes it, for every kind of type.  It is sufficient, not
necessary, and its clauses 2 to 5 are conservative.

**Co-recursion.**  Section 3.8.  The exposure existed for the types of
fields and was closed in the fork
(https://github.com/erniecohen/dafny/issues/143).  Invariants add two ways to
reopen it.  One is to let the raw type of a co-recursive call's result, which
the invariant check assumes, reach the component-type obligations.  The other
is to let an invariant or a raw member assume that a co-recursive component is
finished.  Controls `V7`, `V7b` and `V7c` of section 6.4 are the tests, and
the tests of issue 143 must keep their verdicts.

**Trigger cost.**  The equivalence for a class triggers on
`$Is(o, Tclass.C?)`, which is a common term.  It appears only for classes
with invariants, but its cost must be measured in resource units on the first
real examples.

## 6. Examples

### 6.1 Accepted

```dafny
// A datatype invariant, used without restating it.
datatype Interval = Interval(lo: int, hi: int) {
  invariant lo <= hi
  witness Interval(0, 0)

  function Width(): nat { hi - lo }                    // nat: uses the invariant
  function Shift(d: int): Interval { this.(lo := lo + d).(hi := hi + d) }
                                                       // the middle value is raw; one check, at the result
  predicate Tightest()
  { forall j: Interval :: j.lo <= lo && hi <= j.hi ==> j == this }   // quantifies over the type: not in the cone
}

method Use(i: Interval) {
  assert i.lo <= i.hi;                                 // from the type
  var j: Interval := Interval(i.lo, i.hi + 1);         // checked here
  var r := Interval(i.hi, i.lo);                       // r: raw Interval.  No check, and no error.
  assert r.lo >= r.hi;                                 // destructors work on raw values
}
```

```dafny
// The invariant through a member, and a separate proof about an instance.
datatype Tree = Leaf | Node(left: Tree, key: int, right: Tree) {
  predicate Ordered() {                                // a raw member: 'this' is a 'raw Tree' here
    Node? ==> (forall k <- left.Keys() :: k < key) && (forall k <- right.Keys() :: key < k)
  }
  invariant Ordered()
  function Keys(): set<int> { if Leaf? then {} else left.Keys() + {key} + right.Keys() }
}

lemma InsertOrdered(r: raw Tree, t: Tree, k: int)
  requires r == Rebuild(t, k)
  ensures r.Ordered()

method Insert(t: Tree, k: int) returns (u: Tree) {
  var r := Rebuild(t, k);          // function Rebuild(t: Tree, k: int): raw Tree
  InsertOrdered(r, t, k);          // the proof has the instance to work with
  u := r;                          // checked here
}
```

`left.Keys()` is applied to a component, which is a finished `Tree`, so `Keys`
is not a raw member and may assume the invariant.

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
  const half: nat := cap / 2                           // a const with an initializer may be read too
  var len: nat                                         // mutable: not the invariant's business

  invariant data.Length == cap && 0 < cap && half < cap

  constructor (n: nat)
    requires 0 < n
  {
    data := new int[n];
    cap := n;
    len := 0;
    new;                                               // invariant checked here
    Reset();                                           // 'this' is a Buffer
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
datatype Rose = Node(size: nat, kids: seq<Rose>) {
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

```dafny
// A newtype: the two forms mean the same.
newtype Even  = x: int | x % 2 == 0
newtype Even' = int { invariant this as int % 2 == 0  witness 0 }
```

```dafny
// Codatatypes.  The invariant of each new node is proved.  A co-recursive call is only known to be raw.
codatatype Pos = Cons(head: int, tail: Pos) { invariant head > 0 }
function From(n: int): Pos requires n > 0 { Cons(n, From(n + 1)) }       // checked: n > 0
lemma AllPositive(s: Pos, k: nat) ensures Nth(s, k) > 0                  // by induction on k, from the type

codatatype Zs = Cons(head: int, tail: Zs) {
  function Nth(n: nat): int decreases n { if n == 0 then head else tail.Nth(n - 1) }
  invariant exists n: nat :: Nth(n) == 0                                 // every suffix has a zero
}
function Alt(): Zs { Cons(1, Cons(0, Alt())) }                           // checked for the inner node, then the outer
```

The abstract type of section 4.3 is the remaining accepted example.

### 6.2 Rejected by the resolver

```dafny
class C {
  var v: int
  invariant v >= 0             // error: an invariant may not read a mutable field
}

class Max {
  const x: int
  invariant forall c: Max :: c.x <= x       // error: the invariant of 'Max' refers to the set of values of 'Max'
}

datatype Liar = Liar(f: int) {
  invariant f == 0 ==> !(exists e: Liar :: e.f == 0)
}                                           // error: the invariant of 'Liar' refers to the set of values of 'Liar'

datatype Liar2 = Liar2(f: int) {
  predicate No() { !(this is Liar2) }       // error: 'No' is used by the invariant of 'Liar2' and tests a raw value against 'Liar2'
  invariant No()
}

module Bad refines Spec {                   // Spec from section 4.3
  type T = int                              // error: 'T' has an invariant; a type synonym cannot carry it
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
  witness D(0, 1)                              // error: invariant might not hold
  function Inc(): D { this.(x := x + 1) }      // error at the result: invariant might not hold
  function Gap(): nat { x - y }                // error: result might be negative.  'Gap' is used by an invariant,
  invariant Gap() == 0                         //        so 'this' is raw here and x == y is not known
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

Codatatypes (section 3.8):

```dafny
function Ones(): Zs { Cons(1, Ones()) }        // Zs from section 6.1.  error: invariant might not hold.
                                               // Ones() is only known to be raw

codatatype Zs' = Cons(head: int, tail: Zs') {
  function Nth(n: nat): int decreases n { if n == 0 then head else tail.Nth(n - 1) }
  predicate Q() ensures Q() { exists n: nat :: Nth(n) == 0 }   // error: postcondition might not hold.  'Q' is used by
  invariant Q()                                                 //        the invariant, so 'this' and its tail are raw
}

codatatype Sorted = Cons(head: int, tail: Sorted) { invariant head <= tail.head }
function Up(n: int): Sorted { Cons(n, Up(n + 1)) }   // error in the first codatatype stage: the check needs
                                                     // Up(n + 1).head.  Accepted once the call can be unfolded
```

### 6.4 Vacuity controls

Each of `V1` to `V4` must report an error on its `assert false`:

```dafny
method V1(i: Interval) { assert false; }                       // the equivalence for a datatype
method V2() { var p := new Pos(1); assert false; }             // the equivalence for a class, after a good new
method V3(a: Account) { assert false; }                        // a trait, with inheritance
method V4() { var t: Rose := Node(1, [Node(0, [])]); assert false; } // recursive invariant

class Bad { const k: int  invariant k > 0
  constructor () { k := 0; new; } }                            // must report an error at 'new;'
```

`Bad` pins that `new;` assumes only the raw type before the check.  If it
assumed the finished type, `Bad` would verify with no error at all.

`V5` is the datatype `Liar` of section 6.2 with the extension condition
disabled by a test-only switch, and a method `method V5() { assert false; }`
that mentions the type.  Its `assert false` must verify.  It records why the
condition exists, and that the vacuity tests can tell the difference.  `V5b`
is the same for `Liar2`.

`V6` is `class F { const x: int  invariant false  constructor () { new; } }`.
It must report an error at `new;`, and a method that takes an `F` must be
accepted.  It records that an unsatisfiable object invariant is sound: the
class simply cannot be constructed.

`V7` is the co-call control:

```dafny
codatatype S = Cons(head: int, tail: S) { invariant false }
function F(): S { Cons(1, F()) }               // must report: invariant might not hold
```

If the result of the co-recursive call were assumed to be an `S`, `F` would
verify, and `S` would be both empty and inhabited.

`V7b` is the declaration of `Zs'` in section 6.3.  It must report the error in
`Q`.  If a raw member could assume a finished tail, `Q` would verify, and
`function Ones(): Zs' { Cons(1, Ones()) }` with it.

`V7c` keeps the two kinds of obligation apart:

```dafny
type Impossible = x: int | false witness *
codatatype P = P(w: W, f: Impossible)
codatatype W = W(p: P) { invariant true }
function Bad(): P { P(W(Bad()), 0 as Impossible) }    // must report an error at '0 as Impossible'
```

The invariant check of `W(Bad())` assumes that `Bad()` is a raw `P`.  No raw
`P` exists.  If the assumption stayed in force after that check, the
conversion to `Impossible` would pass.

`V8` is `method V8(r: raw Interval) { var i: Interval := r; }`.  It must
report an error at the assignment.  It records that a raw value is not taken
for a finished one.

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
- `TypeMembers` reaches datatypes, codatatypes, newtypes and abstract types
  through the same production.
- `raw` before a type name, in the type productions, by a lookahead predicate
  so that `raw` stays an identifier everywhere else.
- `new by { ... }` in a constructor body, if open question 2 is accepted.
- The old newtype constraint syntax is parsed as now and desugared in the
  resolver, so that printing gives back what was written.

**AST.**  `Source/DafnyCore/AST/Members/`: a new `InvariantDecl : MemberDecl`
that is an `ICallable`, so it is a call-graph node and a code context, and a
`WitnessDecl`.  On `TopLevelDeclWithMembers`: `Invariants`, a method that
returns all invariants, inherited first, with substitutions, and the set of
raw members.  For the raw type there is a precedent to follow: a class
declaration `C` denotes `C?`, and `C` is a derived `NonNullTypeDecl`, a subset
type over it.  In the same way the declaration of a type with an invariant
denotes the raw type, and the type's own name resolves to a derived
subset-style declaration whose constraint is the invariant.  `Cloner`,
`Printer`, `Substituter`, and the serialization used for `.doo` files need the
new nodes.

**Resolver.**

- `Source/DafnyCore/Resolver/PreType/`: `raw T` and `T` share a pre-type.
  Resolve the invariant as a ghost expression of type `bool` with
  `this: raw T`, under a new resolution context flag, `InInvariant`, alongside
  `InFirstPhaseConstructor` in
  `Source/DafnyCore/Resolver/ResolutionContext.cs`.  Give constructor
  applications and updates the raw type, and let subset-type inference do the
  rest.
- A new pass: compute the raw members (a fixpoint over "applied to `this` in an
  invariant or a raw member", closed under overriding), give them a raw
  receiver, and apply the syntactic reads restrictions.
- `Source/DafnyCore/Resolver/BoundsDiscovery/BoundsDiscovery.cs`: resolve an
  invariant under the restriction that function bodies already have.  For a
  type that may involve references this discharges clause 1 of the extension
  condition.
- `Source/DafnyCore/Resolver/CallGraphBuilder.cs` and
  `Source/DafnyCore/Resolver/ModuleResolver.cs`: the extension condition's
  cone analysis.  Add an edge from each callable to `Inv(T)` for every
  unbounded binder over a `T`-dependent type and every test or conversion of a
  raw value to one.  Report any cycle through `Inv(T)`, reusing
  `ReportCallGraphCycleError`.  Reject applications of stored function values,
  and dynamically bound calls that clause 4 does not accept, directly.
  Compute the per-type-parameter "quantifies without a bound" flag on
  functions.
- `Source/DafnyCore/Resolver/CoCallResolution.cs`: no new disqualification.
  A guarded self-call stays co-recursive when its result type has an
  invariant.  It is not demoted, as a call to a function with an `ensures`
  clause is.  The raw type of a codatatype, with raw co-recursive components,
  belongs with the other typing rules for `raw`.
- `Source/DafnyCore/AST/Types/Types.cs`: `GetAutoInit` for value types with
  invariants; subtyping for raw reference types.
- `Source/DafnyCore/Resolver/ModuleResolver.cs`: "a class with an invariant
  must declare a constructor".
- `Source/DafnyCore/Rewriters/RefinementTransformer.cs`: `MergeClass` already
  merges members; let it accept added invariants and a replaced witness, carry
  an abstract type's invariant into the refining declaration, and reject a
  refining declaration that cannot hold one.

**Verifier.**

- `Source/DafnyCore/Verifier/BoogieGenerator.Types.cs` and
  `Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs`: the raw
  type constant, `T#Inv` with its definition, the equivalence, and the
  existing `$Is` and `$IsAlloc` axioms retargeted to the raw type.
- `Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs` (`AddClassMembers`):
  the same for classes and traits, next to the `implements$` and field axioms.
- The conversion obligation, where the subset-type check is made today.
- `Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs`
  (`DividedBlockStmt`): assume the raw type, translate the `by` block, assert
  the conjuncts.
- `Source/DafnyCore/Verifier/BoogieGenerator.ExpressionWellformed.cs`: the
  suspended-value checks of https://github.com/erniecohen/dafny/pull/159 stay
  as they are.  Add the obligation of section 3.8 where a constructor
  application over suspended values is used at a type with an invariant: the
  components that are not suspended, at their finished types, and then the
  invariant, in a scope of its own that assumes the raw type of the suspended
  calls.  The relaxation that unfolds a co-recursive call needs the function
  consequence axiom split, so that a call's definition is available without
  its result type.
- A well-formedness procedure for each invariant and each witness, in the
  manner of `AddWellformednessCheck` for subset types.
- `Source/DafnyCore/Verifier/ProofObligationDescription.cs`: two descriptions,
  "invariant might not hold" for a conversion and "invariant might not hold at
  `new;`".

**Compilers.**  Invariants are ghost and erased, and `raw T` compiles as `T`.
A conversion from a raw type compiles to nothing.  `is` from a raw type is
ghost.  A compilable witness feeds the existing default-value machinery, as a
subset type's does.  The detection of a native representation for a newtype
must see the constraint through the new form.

**Audit.**  `Source/DafnyCore/Auditor/`: a bodiless constructor of a class with
an invariant assumes the invariant; so does a bodiless function that returns a
type with one.

**What every change needs** (`AGENTS.md`): `docs/dev/news/105.feat`; tests in
`Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-105*.dfy`;
the Reference Manual (`docs/DafnyRef/`) sections on types, classes, datatypes,
newtypes and refinement; `--help` text for the option.

### 7.2 Stages

Every stage is behind `--member-invariants`.  Every stage shows, on the suite
and the standard library, that verdicts and resource counts are unchanged with
the option off.

| Stage | Content | Exit test |
|---|---|---|
| 0 | Option, grammar (`invariant`, `witness`, `raw`), AST, printer, cloner.  The resolver rejects every invariant with "not yet supported". | Round-trip printing; the diagnostic with the option off is unchanged. |
| 1 | **Datatypes, non-generic, no parents.**  The raw type and its typing rules, raw members, the reads rule, the extension condition in its conservative form (clause 2 rejects every dependent type argument), the encoding, conversion obligations, witnesses and `GetAutoInit`. | Sections 6.1 (datatype examples), 6.2, 6.3, and controls V1, V4, V5, V5b, V8. |
| 2 | **Emptiness and raw-typing audit.**  Every consumer of "is this type inhabited" for value types, and every place the translator chooses between a type and its raw type.  Generic datatypes. | A test for each row of the vacuous-types table. |
| 3 | **Classes.**  Raw reference types and their subtyping, the equivalence, the check at `new;`, the constructor requirement, audit reporting.  The test for lambda bodies (section 3.3).  Measure the trigger cost. | Controls V2 and V6, the `Bad` test; the `Buffer`, `Box` and `Max` examples; raw references do not reach `object`. |
| 4 | **Traits and `extends`.**  Inherited invariants and raw members, the conjunction at `new;`, export visibility.  General traits extended by datatypes. | Control V3; a cross-module extension test. |
| 5 | **Refinement and abstract types.**  Added invariants, replaced witnesses, the tests of section 4.2, invariants on abstract types. | The `M0`/`M1` and `Spec`/`Impl` examples; a refinement that must fail at an inherited `new;`. |
| 6 | **Newtypes.**  The member form, the sugar, native representations. | Every existing newtype test gives the same verdict and resource count in both forms. |
| 7 | **Codatatypes.**  The raw type that is raw at every depth.  The prefix-wise check, on top of the suspended-value rule, without unfolding. | Controls V7, V7b and V7c; the `Pos`, `Zs` and `Ones` examples; the tests of issue 143 keep their verdicts. |
| 8 | **Relaxations**, each with its own argument: `new by`; the exact clause 2; conversions in the cone; stored function values and dynamic dispatch in a reference-free type's cone; positive occurrences under a fixed-point reading; unfolding a co-recursive call in an invariant check (the `Sorted` example); membership in a codatatype proved by a greatest lemma. | Per relaxation. |

## 8. Open questions for the owner

Settled by the owner, and assumed above: raw types exist and are nameable;
invariants go wherever members can be declared, abstract types and newtypes
included, with the newtype constraint as sugar; `this` is not limited to
component selection; codatatypes are included, and are checked prefix-wise;
the object check is at `new;`; every obligation is proved once.

Each remaining question has a recommendation.  The plan above assumes it.

1. **The raw type's syntax.**
   *Recommend `raw T`.*  The reasons are in section 3.5.  The alternatives are
   a member-style name (`T.Raw`), which can clash with a member or nested
   declaration and reads as something declared, and a suffix mark in the
   manner of `C?`, which is shorter and says less.

2. **`new by { ... }` for a proof with the object in hand.**
   *Recommend yes, as a later stage.*  For value types the raw value is an
   ordinary expression, so the owner's point is met by the typing rules.  For
   an object the only place the instance exists unfinished is at `new;`, and
   something must give the proof access to it there.  Without this, the proof
   has to be about the constants, passed one by one.

3. **Raw members: inferred or declared?**
   *Recommend inferred.*  The owner's example,
   `type T { function f(): int  invariant f() > 0 }`, has no marker, and the
   set is determined by the program.  The cost is that it is not local: adding
   an invariant can make an existing member raw, in the same type, in a
   refinement, or in a trait that a new class's invariant wants to use.  A
   modifier (`raw function`) would make that visible at the member and could
   be added as an optional assertion of intent.

4. **Unannotated constructor applications have the raw type.**
   *Recommend yes.*  It is the subset-type rule and what "constructors produce
   raw values" says.  The consequence is that `var r := K(e)` is checked where
   `r` is first used as a `T`, and the error appears there, not at the
   constructor.  Declaring the variable's type moves it back.

5. **Invariants that refer to the type's own extension: ban, or allow positive
   occurrences only?**
   *Recommend ban (the extension condition).*  It is one condition for every
   kind of type.  Positive occurrences admit existentials only, not the
   `forall` an invariant would want, and leave a choice of fixed point that
   changes what the check must be.  Within the ban, clause 3 (no stored
   function values in the cone) and the conversion half of clause 5 are
   conservative and unsupported by a counterexample; the owner may prefer to
   drop them.

6. **Codatatypes: is a raw type that is raw at every depth acceptable?**
   *Recommend yes* (section 3.8).  It follows from the prefix reading: the
   invariant is evaluated on values whose co-recursive components are known
   only to level `k`, and `k` may be 0.  The alternative that keeps one
   meaning of `raw` for every type is to forbid a codatatype's invariant from
   mentioning a co-recursive component at all.  That rules out an invariant
   like that of `Sorted` for good.
   A second point: unfolding a co-recursive call is placed with the
   relaxations, so the first codatatype stage accepts the type `Sorted` and
   rejects the function `Up`.  Unfolding could be moved into that stage.

7. **A value type with an invariant and no `witness`: possibly empty, or an
   error?**
   *Recommend possibly empty.*  It is always sound and never guesses.  A subset
   type without a witness clause instead makes the verifier look for one, and
   a newtype in the old syntax keeps that behaviour.  It could be mirrored for
   the member form, but a silent search is the less explicit choice.

8. **Witness as a member (`witness e`) rather than a clause on the type header?**
   *Recommend member.*  A refinement can then replace it with the syntax it
   already uses to add members.

9. **Must a class with an invariant declare a constructor?**
   *Recommend yes.*  The alternative is a check at every `new C` site that the
   invariant holds for arbitrary constants, which is non-modular and almost
   never true.

10. **Strengthening an invariant in a refinement.**
    *Recommend allow.*  It is the issue's motivating case.  It is sound because
    every inherited conversion site carries the added conjuncts as new
    obligations, while the inherited conjuncts are assumed there (sections 4.2
    and 4.4).  The owner should confirm that the translator's skipping of
    inherited obligations is a premise the fork intends to keep, since the plan
    leans on it in both directions.

11. **Newtype arithmetic.**
    *Recommend no change.*  Operators on a newtype keep requiring a result in
    the type.  Letting them produce raw values would be the uniform rule, and
    would cost the native representation.

12. **Option name and off-behaviour.**
    *Recommend `--member-invariants`*, with the parser diagnostic unchanged
    when the option is off.  A friendlier "enable `--member-invariants`"
    message would change an existing diagnostic, which the rules allow for an
    error-message fix; it can be a separate change.

13. **Named invariants** (`invariant Sorted: ...`) for error messages, `reveal`
    and opacity.
    *Recommend defer.*  Attributes already attach, and `{:error "..."}` covers
    messages.

## 9. Settled enough to implement first

Stages 0 and 1: **invariants on non-generic inductive datatypes with no
parents, with their raw types.**

- The semantics is a definition by structural recursion with a three-line
  statement.
- The encoding is the subset-type pattern: a raw type, one predicate, one
  equivalence.
- The obligation sits where a subset-type conversion check does.
- Witnesses reuse the possibly-empty machinery.
- The extension condition is needed from the first day, in its conservative
  form.  With the typing of raw values, it is the part to review hardest.
- None of it touches the heap, `new;`, inheritance, refinement or
  co-recursion.
