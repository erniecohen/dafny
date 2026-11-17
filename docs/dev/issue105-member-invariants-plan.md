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
| Where | `class`, `trait`, `datatype`, `newtype`, abstract type.  Not subset types, which have no members.  Not codatatypes, and a program that declares an invariant may not declare a codatatype at all (section 3.8). |
| The raw type | Derived and nameable: `raw T`.  Constructors and updates produce raw values.  Using a raw value where a `T` is expected checks the invariant, as a subset-type conversion does. |
| What an invariant may use | Any member with empty reads, applied to `this` or to anything else.  Inside an invariant `this` has type `raw T`. |
| When assumed | Everywhere a value has type `T`. |
| When checked, value types | Where a raw value is used at type `T`. |
| When checked, objects | One obligation per constructor, emitted at `new;` (explicit or implicit).  Because the state it reads cannot change, this is the same obligation as "at constructor exit", not an extra one. |
| Newtypes | The `x: B \| P(x)` constraint *is* the invariant.  The old syntax is kept as sugar during migration. |
| Inhabitance | A value type with an invariant is possibly empty unless it declares a `witness`.  Its raw type is as inhabited as the type is today.  Reference types are unaffected. |
| Quantifying over the type | Not in the type's own invariant or in what the invariant reaches, unless the quantifier is bounded by a collection.  One condition for every kind of type. |
| Repeated obligations | None.  Each conjunct is proved once per construction site; inherited conjuncts are not re-proved in a refinement, and an ancestor reached by two paths counts once (section 4.4). |
| `extends`, refinement | Invariants accumulate by conjunction.  There is no syntax that weakens, so "only strengthened" holds by construction.  The raw type follows automatically. |
| Encoding | Per-type axioms only.  No prelude change.  A program without invariants gets the same Boogie as today. |
| Option | `--member-invariants`, default off, hidden while the feature is incomplete, requires `--type-system-refresh`. |

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
4. **More places, and one exclusion.**  Newtypes and abstract types are
   included.  A program that declares an invariant may not declare a
   codatatype: a co-recursive call has no termination argument to justify
   assuming that its result is in the type (section 3.8).

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

Wherever members can be declared, except in a codatatype.

| Declaration | Allowed | Note |
|---|---|---|
| `class` | yes | Checked at `new;` (section 3.4). |
| `trait` (reference or general) | yes | Every extending type inherits it. |
| `datatype` | yes | Section 3.5. |
| `codatatype` | no | A resolution error, and a program with an invariant may not declare a codatatype at all (section 3.8). |
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

A datatype value could contain itself through a codatatype component, but a
program with invariants declares no codatatype (section 3.8), so the recursion
on structure is always well-founded.

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
  ghost predicate No() { !(this is Liar2) }  // 'this' is raw here, so the test is not trivial
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

**A correction (stage 1 of section 7.2).**  The argument above shows that no
override can violate clause 1 where `T` may involve references.  It does not
cover clause 5: an override may test a constructor application, which is raw,
against `T`, and that is the `Liar2` paradox again.

```dafny
trait Oracle extends object { ghost predicate Ok(n: int) }
datatype Liar3 = Liar3(f: int, o: Oracle) {
  invariant o.Ok(f)                         // error: 'Ok' is dynamically bound
}
class Teller extends Oracle {
  ghost predicate Ok(n: int) { !(Liar3(n, this) is Liar3) }
}
```

So clause 4 rejects every dynamically bound call, for every kind of type, and
the `Box` example, first listed in section 6.1, is rejected (section 6.2).

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

This section covers datatypes and newtypes.  Abstract types are in section
4.3.

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

**Generic types and variance.**  For a generic `D<X>` with invariants, `D<A>`
is `{ v in raw D<A> | Inv[X := A](v) }`.  `D` declares a variance for `X`, and
`D<A>` keeps it, as `raw D<A>` does; the invariants must justify it.  Where `X`
is covariant and `A` is a subtype of `B`, `raw D<A>` is a subtype of
`raw D<B>` by the ordinary rule, so `D<A>` is a subtype of `D<B>` exactly where
`Inv[X := A](v)` implies `Inv[X := B](v)` for every raw `v`: the invariants are
*monotone* in `X`.  Where `X` is contravariant they must be *antitone*.  An
invariant may quantify over `X`, so neither is automatic:

```dafny
ghost predicate Never<X>(x: X) { false }
datatype Box<+X(!new)> = Box(b: bool) {
  invariant b ==> forall y: X :: Never(y)   // Box(true) is a Box<X> exactly where X is empty
}
```

`Box(true)` is a `Box<Empty>` and not a `Box<int>`, so a covariant `Box` would
let a `Box<Empty>` become a `Box<int>` with no check, and prove `false`.

*Rule (the owner's decision).*  A declared `+X` or `-X` stands only where the
invariants, own and inherited, closed over the functions and constants they
call, are monotone (antitone) in `X`.  Otherwise it is a resolution error at
the declaration that names the offending occurrence.  `raw D` keeps its
declared variance in any case.

*The check is by polarity.*  Each occurrence of `X` in the invariants has a
polarity, positive, negative or both, in the lattice none < positive,
negative < both.  The invariant itself is in a positive context.

- `forall y: X :: P` is negative in `X`; `exists y: X :: P` and `e is X` are
  positive.  The quantifier's domain grows with `X`: an existential that holds
  keeps its witness, a universal may meet a new counterexample.  A type test
  against a larger type holds of more values.
- Within the bound variable's type, the occurrence composes with the type's
  variance: in `A -> R` the parameter position flips (an arrow type shrinks as
  its domain grows), and in `G<..X..>` it composes with the variance `G`
  declares there, a non-variant parameter giving both.  Collections are
  covariant.
- Negation and the antecedent of an implication flip the context (`A <== B`
  is `B ==> A`).  `<==>`, `==` and `!=` of booleans, and the condition of an
  `if`, are in both contexts.  Conjunction, disjunction, the consequent and the
  branches of an `if` keep it.  The range of `forall` is flipped, that of
  `exists` kept.
- A set or map comprehension, a lambda, a `:|` binding and a function used as
  a value give `X` both polarities: their meaning is a value that is not
  ordered by implication.
- A call of a generic function contributes, for each of its type parameters,
  its polarity in the function's body, composed with the context and with the
  occurrence of `X` in the type argument.  The body is in a positive context if
  the function returns `bool`, and in both otherwise.  A constant with a
  right-hand side is a nullary function.  The polarities are the least
  fixpoint of these equations over the call graph, computed by iteration from
  none.  For a function defined by well-founded recursion, induction on its
  decreases measure shows that its result is monotone (antitone) where the
  fixpoint says so.  For an extreme predicate, if the defining operator
  `F[X := A]` is pointwise below `F[X := B]`, then `lfp F[X := B]` is a
  pre-fixed point of `F[X := A]` and `gfp F[X := A]` a post-fixed point of
  `F[X := B]`, so both fixpoints are monotone as the operator is.
- Anything opaque is both: a function with no body (abstract, or declared
  without one), a call that is dynamically dispatched (a trait function
  without a body; a function with a body cannot be overridden), and a function
  whose body the invariant's module cannot see because the exporting module
  does not reveal it.  Any interpretation of such a function is a model of what
  the verifier knows, so nothing can be assumed of its dependence on `X`.  A
  function declared `opaque` is analysed by its body, which is its meaning;
  `opaque` concerns only when a proof may unfold it.
- `X` used only as the type of a value does not count: a component of `D`'s
  constructors, an operand of `==` on values of type `X`, an argument.  A value
  is the same value at `A` and at `B`; its components of type `X` are in `A`,
  so in `B`.
- A statement inside an expression (`S; E`) is a proof hint, and its value is
  that of `E` (section 10, the extension condition of the second step), so
  only `E` is analysed.

*Soundness.*  By induction on the formula, an expression all of whose
occurrences of `X` are positive (negative) in a positive context is monotone
(antitone) in `X`: each rule above preserves the direction it states, and the
calls are covered by the fixpoint.  So a declaration that passes the check has
`D<A>` a subtype of `D<B>` (or `D<B>` of `D<A>`) as its variance says.

*What is trusted.*  The analysis trusts the declared variance of every other
type, as subtyping does.  For a datatype or a type with invariants that is
justified, by the ordinary variance check of its components and by this check.
For a subset type or a newtype it is not: a covariant subset type whose
constraint quantifies over its parameter has the same hole without this
feature (https://github.com/erniecohen/dafny/issues/491).  That issue's
intended fix is this analysis applied to the constraint, which then justifies
the trust here.

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
| Recursion | `T_`'s components can only be `T_`: a component of type `T` is rejected as a recursive constraint dependency.  An invariant on every node is a recursive predicate over the whole value | components are finished unless declared raw |
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

### 3.8 Codatatypes: the declaration rule

**Rule: a resolved program may not declare both a codatatype and a member
invariant.**  Under `--member-invariants` the resolver reports an error when
it does, at each `invariant` member: "a program that declares a member
invariant may not declare a codatatype", naming one codatatype.  The owner
chose this rule
(https://github.com/erniecohen/dafny/issues/395#issuecomment-6908809898) in
place of the mixing rules of the previous revision of this section.

- **What counts.**  An `invariant` member, written as such, in any declaration
  of the program.  A subset type's constraint and a newtype written in the old
  `x: B | P(x)` syntax are not member invariants and do not count, so the rule
  rejects no program that does not use the new syntax.  (A newtype in the
  member form, `newtype N = B { invariant ... }`, does count.)  The rule is a
  resolution error raised only where an invariant is declared, so without the
  option, and in every program written without invariants, nothing changes.
- **Libraries.**  A program's libraries, `.doo` files and `--library` sources
  alike, are resolved together with it, so the one check covers them.  There is
  no new option, nothing recorded in a `.doo` file, and no check at link time.
  A library that declares neither a codatatype nor an invariant works with
  programs of either kind.
- **An invariant inside a codatatype** was a resolution error of its own in the
  previous revision ("a codatatype may not declare an invariant").  It is now
  a special case of the rule: the codatatype that would hold the invariant is a
  codatatype declared in the program.  The resolver reports it with the more
  specific message, at the invariant.  A `witness` member in a codatatype is
  also an error, since a codatatype has no invariant for it to witness.

**Why this is enough.**  Three reasons kept invariants away from codatatypes
(they are recorded in full in
https://github.com/erniecohen/dafny/pull/384/commits/267570e34873023a813f57ec65ce1511f5de407c):
a guarded co-recursive call has no termination argument, so a check that may
assume the call's result is in the type assumes its own conclusion; the
suspended-value rule of https://github.com/erniecohen/dafny/issues/143 and
https://github.com/erniecohen/dafny/pull/159 is sound but rejects every
co-recursive function whose result type carries a constraint; and the
defining equation of a co-recursive function is tied to the type of its
result.  All three are about co-recursive calls, and a co-recursive call is a
call whose result is a codatatype value built under a co-constructor.  A
program with no codatatype has none.

**What the rule removes from the plan.**  The previous revision allowed
mixing in both directions, and needed one extra rule for it: a conversion to a
type with an invariant was not a position for a co-recursive call, however
many constructors lay between the conversion and the call.  It was needed
because a datatype value can contain itself through a codatatype component:

```dafny
codatatype Forest = Cons(head: Bush, tail: Forest)
datatype Bush = Tip | Fork(kids: Forest)
function C(): Forest { Cons(Fork(Cons(Tip, C())), C()) }     // accepted today
```

If `Bush` had an invariant, the check of `Fork(Cons(Tip, C()))` would assume
that `C()` is finished while checking a node that contains it.  Under the
declaration rule `Bush` cannot have an invariant in a program that declares
`Forest`, so the situation does not arise, and the rule is dropped with
nothing in its place.  So are its consequences: the translator records no
implicit conversions for the co-call pass, `CoCallResolution.cs` is not
touched, and the model of section 5.2 needs no ordinal stages for datatype
values that contain themselves.  In a program with invariants every datatype
value is well-founded in its constructors, because the only way to build a
value around a call that does not terminate is guarded co-recursion.

**Semantics.**  A program that declares invariants rests only on least fixed
points: inductive datatypes, terminating functions, and the definitions of
types by structural recursion (section 5.2).  Greatest predicates and lemmas
are still available.  They are defined through their prefix predicates, which
are least definitions over a natural-number index, and need no guarded
recursion.

**A newtype over a codatatype**, which the fork allows under
`--extended-newtype-bases`, needs a codatatype, so it cannot appear in a
program with invariants.  In other programs it keeps the rules it has today.

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
  datatype, newtype, class or trait, or another abstract type.  A type synonym
  or a subset type cannot, because it has no members.  A codatatype cannot
  appear in a program with invariants at all (section 3.8).  Refining an abstract type that has an invariant with
  one of those is an error.
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
  A program with invariants declares no codatatype (section 3.8).
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
  (`CheckSubrange`).
- **`new;`.**  In the translation of `DividedBlockStmt`
  (`Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs`), the
  first division runs on local surrogates of the fields, and `new;` then
  assumes that `this` is a `C?` and that each field equals its surrogate.
  With an invariant, `new;` assumes only the *raw* type, then asserts each
  conjunct of the invariant of `this`.  The finished type then follows from
  the equivalence above.  There is no assumption of the finished type to
  place before or after the check.
- **Co-recursion** (section 3.8).  Nothing new is emitted.  A program with
  invariants declares no codatatype, so it has no co-recursive calls, and none
  of the obligations above is ever stated of a value that holds a suspended
  one.

### 5.2 Why it is consistent

Fix a program that meets the reads restrictions, the extension condition, the
raw-member rule, and the declaration rule of section 3.8.  Build a model.

**The raw types** are what the types are today.  Raw datatype values are
constructor applications to components in their types.  Raw references of
dynamic type `C` are all references with that dynamic type, with every
combination of `const` values in the constants' types.  Raw newtype values
are the base values.

**The invariants are fixed predicates of raw values.**  The cone of `T`'s
invariant does not refer to the extension of `T` or of a type that depends on
it, by quantifying (clauses 1 to 4) or by testing a raw value (clause 5).
What it applies to a raw `this` is a raw member, defined on all raw values
without reference to the invariant.  What it applies to a component is
applied to a value that is already in its type.

**The types.**  Order the types so that each comes after the types whose
extensions its cone refers to.

- *Datatypes.*  `K(a) ∈ D ⟺ a ∈ types ∧ Inv(K(a))`, by recursion on the
  structure of values.  A datatype value is well-founded in its own
  constructors, which Dafny's rank axioms already require.  The program
  declares no codatatype (section 3.8), so no value contains itself and
  there is exactly one solution.  Datatypes without invariants keep the
  meaning they have today over the types so defined.
- *Classes and traits.*  A component of a reference may be a reference of the
  same class.  Build the references by stages, each stage using finished
  references of earlier stages as `const` values, so that no reference is its
  own component.  Then `o ∈ C ⟺ dtype(o) = C ∧ Inv(o)` is a definition by
  recursion on the stage.
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
| A value of a type with an invariant built around a guarded co-recursive call | The declaration rule: a program with invariants declares no codatatype, so it has no co-recursive calls (section 3.8) |
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

**Co-recursion.**  Section 3.8.  A program that declares an invariant may not
declare a codatatype, so the exposure through guarded co-recursion that the
previous revision had to close with a rule on conversions does not arise.
Control `V7` of section 6.4 is the test of the declaration rule, and the tests
of https://github.com/erniecohen/dafny/issues/143 must keep their verdicts,
since they declare no invariant.

**Trigger cost.**  The equivalence for a class triggers on
`$Is(o, Tclass.C?)`, which is a common term.  It appears only for classes
with invariants, but its cost must be measured in resource units on the first
real examples.  Measured with the fifth step, on programs that state the same
facts once with an invariant and once with the subset-type idiom of section
3.7: the cost is of the same order.  A client of the class pays slightly
more resource units; the class's own methods pay more, since there `this`
carries the invariant.

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
  ghost predicate Tightest()
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
// A bounded quantifier over the type itself, inside the invariant.  (In a module apart from Tree, whose
// constructor has the same name.)
datatype Rose = Node(size: nat, kids: seq<Rose>) {
  invariant forall k <- kids :: k.size < size
  witness Node(0, [])
}
```

```dafny
// A newtype: the two forms mean the same.
newtype Even  = x: int | x % 2 == 0
newtype Even' = int { invariant this as int % 2 == 0  witness 0 }
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
  ghost predicate No() { !(this is Liar2) } // error: 'No' is used by the invariant of 'Liar2' and tests a raw value against 'Liar2'
  invariant No()
}

module Bad refines Spec {                   // Spec from section 4.3
  type T = int                              // error: 'T' has an invariant; a type synonym cannot carry it
}

class NoCtor {
  const k: int
  invariant k > 0              // error: a class with an invariant must declare a constructor
}

// An object invariant through a trait-typed component: rejected by clause 4 of section 3.3 (see its correction).
trait Shape extends object { predicate Fits(n: int) }   // no reads clause
class Box {
  const s: Shape
  const n: int
  invariant s.Fits(n)          // error: 'Fits' is dynamically bound
  constructor (s: Shape, n: int) requires s.Fits(n) { this.s := s; this.n := n; }
}

codatatype NonNeg = Cons(head: int, tail: NonNeg) {
  invariant head >= 0          // error: a codatatype may not declare an invariant
}
```

The declaration rule (section 3.8), in a program of its own:

```dafny
codatatype Stream = Cons(head: int, tail: Stream)
datatype Bounded = Bounded(bound: int, s: Stream) {
  invariant bound >= 0         // error: a program that declares a member invariant may not declare a codatatype ('Stream')
}
```

### 6.3 Rejected by the verifier

```dafny
datatype D = D(x: nat, y: nat) {
  invariant x == y
  witness D(0, 1)                              // error: invariant might not hold
  function Inc(): D { this.(x := x + 1) }      // error at the result, once for each invariant: invariant might not hold
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
  Consume(e);                                  // error: variable 'e', which is subject to definite-assignment
}                                              //        rules, might be uninitialized here
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
is the same for `Liar2`.  (The switch is `--unchecked-invariant-extension`.  The
inconsistency is in the axioms of `$Is` for the type, which are instantiated
only for a value of it, so each method states a raw value and splits on whether
it is one; a bare `assert false` does not verify.)

`V6` is `class F { const x: int  invariant false  constructor () { new; } }`.
It must report an error at `new;`, and a method that takes an `F` must be
accepted.  It records that an unsatisfiable object invariant is sound: the
class simply cannot be constructed.

`V7` is the declaration rule.  The program of section 3.8 with `Forest`,
`Bush` and `C` must be rejected by the resolver when `Bush` declares
`invariant true`, and accepted, as it is today, without the invariant.  The
same pair is repeated with the codatatype in a library and the invariant in
the program, and the other way round, and a neutral library (neither) must be
accepted with programs of both kinds.

`V8` is `method V8(r: raw Interval) { var i: Interval := r; }`.  It must
report an error at the assignment.  It records that a raw value is not taken
for a finished one.

## 7. Implementation sketch

### 7.1 Files

**Option.**  `Source/DafnyCore/Options/CommonOptionBag.cs`: `--member-invariants`,
default false, hidden until the feature is complete, usable from `dfyconfig.toml`.  It is an error without
`--type-system-refresh`.  `Source/DafnyCore/Options/DafnyCommands.cs`: add to
the resolver options.

**Parser.**  `Source/DafnyCore/Dafny.atg`:

- `ClassMemberDecl`: a new alternative for `InvariantDecl`, and one for
  `WitnessDecl`.
- With the option off, the alternatives are disabled by a semantic predicate,
  so the diagnostic for `trait T { invariant true }` stays the one recorded in
  the issue.
- `TypeMembers` reaches datatypes, codatatypes, newtypes and abstract types
  through the same production.  The resolver rejects an invariant or a witness
  in a codatatype, and an invariant in any program that declares a codatatype
  (section 3.8).
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
- `Source/DafnyCore/AST/Types/Types.cs`: `GetAutoInit` for value types with
  invariants; subtyping for raw reference types.
- `Source/DafnyCore/Resolver/ModuleResolver.cs`: "a class with an invariant
  must declare a constructor", "a codatatype may not declare an
  invariant", and the declaration rule of section 3.8: after the program and
  its libraries are resolved, if any declaration declares a codatatype,
  every `invariant` member is an error.
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
| 1 | **Datatypes, non-generic, no parents.**  The raw type and its typing rules, raw members, the reads rule, the extension condition in its conservative form (clause 2 rejects every dependent type argument), the encoding, conversion obligations, witnesses and `GetAutoInit`.  The error for an invariant in a codatatype, and the declaration rule (section 3.8). | Sections 6.1 (datatype examples), 6.2, 6.3, and controls V1, V4, V5, V5b, V7, V8.  The tests of issue 143 keep their verdicts. |
| 2 | **Emptiness and raw-typing audit.**  Every consumer of "is this type inhabited" for value types, and every place the translator chooses between a type and its raw type.  Generic datatypes. | A test for each row of the vacuous-types table. |
| 3 | **Classes.**  Raw reference types and their subtyping, the equivalence, the check at `new;`, the constructor requirement, audit reporting.  The test for lambda bodies (section 3.3).  Measure the trigger cost. | Controls V2 and V6, the `Bad` test; the `Buffer`, `Box` and `Max` examples; raw references do not reach `object`. |
| 4 | **Traits and `extends`.**  Inherited invariants and raw members, the conjunction at `new;`, export visibility.  General traits extended by datatypes. | Control V3; a cross-module extension test. |
| 5 | **Refinement and abstract types.**  Added invariants, replaced witnesses, the tests of section 4.2, invariants on abstract types. | The `M0`/`M1` and `Spec`/`Impl` examples; a refinement that must fail at an inherited `new;`. |
| 6 | **Newtypes.**  The member form, the sugar, native representations. | Every existing newtype test gives the same verdict and resource count in both forms. |
| 7 | **Relaxations**, each with its own argument: `new by`; the exact clause 2; conversions in the cone; stored function values and dynamic dispatch in a reference-free type's cone; positive occurrences under a fixed-point reading. | Per relaxation. |

Invariants on codatatypes are not a stage of this plan, and the declaration
rule of section 3.8 keeps codatatypes out of every program that declares an
invariant.

## 8. Open questions for the owner

Settled by the owner, and assumed above: raw types exist and are nameable;
invariants go wherever members can be declared, abstract types and newtypes
included, with the newtype constraint as sugar, but not in codatatypes, and
not in a program that declares a codatatype (section 3.8); `this` is not limited to component selection; the object check is at
`new;`; every obligation is proved once.

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

6. **A datatype with an invariant inside a codatatype.**
   *Settled by the owner: the declaration rule of section 3.8.*  A program
   that declares an invariant may not declare a codatatype.  The previous
   recommendation, a rule that a conversion to a type with an invariant is not
   a position for a co-recursive call, is withdrawn as unnecessary.

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
- None of it touches the heap, `new;`, inheritance or refinement, or
  co-recursion: the declaration rule (section 3.8) keeps codatatypes out.

## 10. Implementation decisions

Decisions made while implementing, on the principle of consistent semantics.
Each is also recorded in the message of the commit that makes it.  The
feature lands as one pull request, a sequence of commits, one or a few for
each step below.

**Stage 0 and the resolution part of stage 1 (first step).**

- *Split.*  Section 7.2's stage 0 has the resolver reject every invariant.
  Instead, the first step resolves invariants, applies the reads
  restriction of section 3.2, the codatatype ban and the declaration rule of
  section 3.8, and the verifier reports each invariant as not yet supported.
  `witness` members and `raw T` are parsed and rejected by the resolver as not
  yet supported.  Resolution is the same for every kind of type, so it is not
  split by kind; the stages by kind are about verification.  The extension
  condition of section 3.3 is a resolver check, but it protects only the
  axioms of section 5.1, so it lands with the first stage that emits them;
  until then the verifier rejects every invariant and nothing depends on it.
- *Representation.*  An invariant is a `MemberInvariant`, a ghost instance
  predicate with no parameters, an empty reads clause and the reserved name
  `invariant#k` (k numbers the invariants of the declaration).  A witness is a
  `MemberWitness`, a static const with an inferred type and the reserved name
  `witness#k`.  Neither name can be written in source, so neither member can be
  referred to directly.  This reuses resolution, well-formedness, the call graph
  and Boogie function generation.
- *Invariants are anonymous.*  An invariant never overrides and is never
  overridden, so it takes no part in the name-based inheritance of trait
  members: two invariants with the same reserved name in a type and its parent
  trait are different invariants, not a re-declaration.  A type's ancestors'
  invariants (section 4.1) are found from its parent traits, not from its
  member table.  Likewise in refinement (section 4.2), an invariant of a
  refining declaration never refines one of the refinement base: the inherited
  invariants keep their numbers and the refining declaration's own are numbered
  after them, so the numbers give the order of the conjuncts, inherited ones
  first.
- *Parsing.*  With the option off the scanner and parser see exactly the tokens
  they saw before.  Three new tokens stand for `invariant` and `witness` in a
  member position and for `raw` directly before a type name; the parser retags
  the ordinary tokens as these only under the option.  Coco/R generates no
  scanner if a token lacks a definition, so each has one that starts with the
  control character U+0001.  The scanner therefore produces these tokens from
  U+0001 followed by the word, which used to be a scan error; each production
  rejects such a token, with or without the option, so a program containing
  U+0001 is still rejected.  `raw` is retagged only when the next token is an
  identifier, so `raw` remains usable as a name, including as the name of a
  type.  `invariant` and `witness` are also retagged at the top of a module, so
  that one written there gets the error that it belongs to a type.
- *Never opaque.*  An invariant is a fixed predicate of the type (section 3.1);
  hiding it would hide what the type means.  So an invariant cannot be declared
  `{:opaque}` (or `opaque`), `--default-function-opacity` does not make it
  opaque, and it gets no reveal lemma.  A reveal lemma would also be inherited
  by name, which invariants are not, so traits meeting in a diamond would
  clash on it.
- *Printing.*  The printed program is source: `invariant e`, with the reserved
  name omitted.  The resolved program (`--rprint`) also shows the reserved name
  as a comment, `invariant /* invariant#k */ e`, so the numbering of section 4.2
  can be seen.  Refinement places a declaration's own members before the
  inherited ones, so the printed order is not the order of the numbers.
- *Option check.*  `--member-invariants` without `--type-system-refresh` is a
  command-line error, reported before any file is read (as for
  `--translation-cache`).  The resolver repeats the check for front ends that
  do not go through the command line, such as the language server.
- *Option scope.*  A `.doo` library built with the option requires its clients
  to enable it, since it may declare invariants.  The manifest records the
  option only when it is on, so a library built without it is byte-for-byte
  unchanged and may be used by programs with or without the option.
- *Declaration rule.*  The rule is checked once per program, syntactically,
  before modules are resolved, over every module (abstract and refined modules
  included) and every library.  When it is violated, the error is reported once:
  at an invariant in the program's own source if there is one, otherwise at a
  codatatype in it, naming the other declaration.
- *Reads restriction.*  Checked syntactically on the resolved body, including
  inside lambdas and statement expressions: no mutable field, no array element
  (also through slices and multi-dimensional selection), no `old`, `fresh`,
  `allocated`, `unchanged`, no two-state function or lemma, no function with a
  reads clause (called or used as a value), and no application of a function
  value whose type allows reading the heap (`~>`).  The checks of every
  function body already reject `old`, `fresh`, `allocated`, `unchanged` and
  two-state functions and lemmas in an invariant, which is a single-state
  predicate with no reads clause, so the restriction adds only the rest (its
  own checks of these were removed at the controls step, since no program
  reaches them).  A function value's
  `requires` and `reads` read what the value may read, so they are allowed on a
  `->` or `-->` value and rejected on a `~>` value.  A lambda with a reads
  clause may be built, since building it reads nothing, but its body is held to
  the same restrictions, although it is evaluated only where the lambda is
  applied; this is conservative, and simpler than tracking where a lambda may
  be applied.  Inside an invariant, `this` has the type itself until `raw T`
  exists.

**Open after the first step.**  Recorded at review; none is a
soundness problem while nothing checks or assumes an invariant.

- *The not-yet-supported error is per translated module.*  A module given only
  as a library, one left out by `--filter-symbol`, or a program that is
  compiled with `--no-verify`, is never translated, so its invariants are not
  reported.  The second step makes the error program-wide: if any
  module, a library's included, declares an invariant, no module is verified.
  Compiling with `--no-verify` remains allowed, and the compiled code ignores
  the invariants.
- *Export sets.*  `export provides T` clones a type's members without their
  bodies, so a client sees an invariant without its body.  What a client that
  cannot see the body may assume about a value of `T` is to be decided before
  invariants are assumed.  Settled in the fifth step: an invariant is revealed
  exactly where its type is.
- *The language server's parse cache* keys on the file's name and contents,
  not on the parser's options, so toggling `--member-invariants` in a project
  does not reparse an unchanged file.  This predates member invariants and
  applies to every parser option.

**`raw T` for datatypes (second step).**

- *The derived type.*  A datatype `D` that declares an invariant is itself the
  raw type, `raw D`.  The name `D` written as a type denotes a derived
  declaration (`InvariantTypeDecl`): a subset type of `raw D` whose constraint
  is the conjunction of `D`'s invariants, in the order of their numbers, with
  `witness *`.  This follows `NonNullTypeDecl`, where a class `C` is `C?` and
  `C` is derived.  Unlike `C?`, the derived declaration has no name of its own:
  `D` stays registered for the datatype, so constructors, discriminators,
  static members, `import` and export sets refer to the datatype as before,
  and only `D` in a type position is redirected.  The derived declaration
  shares the datatype's visibility, and member lookup on a `D` sees through it
  to the datatype.  A datatype without invariants gets no derived declaration,
  and `raw D` is `D`.
- *Typing.*  Constructor applications and updates have type `raw D`
  (section 3.5).  `D` converts to `raw D` freely; `raw D` converts to `D` as
  any type converts to a subset type of it, with the obligation left to the
  verifier, and so do the collection and variance rules.  A destructor of a
  raw value has the component's declared type, so components are finished.
  Under `--type-system-refresh`, `raw D` and `D` have the same pre-type.
- *Raw members are inferred* (question 3), for every instance member:
  functions, predicates, constants with an initializer, methods, lemmas, the
  by-method part of a function (which goes with the function) and the prefix
  predicate of an extreme predicate (which goes with the predicate).  Each
  instance member's `this` starts as `D` (as `raw D` in an invariant), and type
  refinement raises it to `raw D` when the member is applied to a raw receiver
  in an invariant or raw member of `D`.  Only `D`'s own invariants and raw
  members make a member of `D` raw: where anything else applies a member that
  is not raw to a raw value, that is a conversion of the receiver, which the
  verifier is to check.  The result is part of type refinement's fixpoint,
  since raising `this` changes the types that flow from it.
- *Where `raw` applies* (section 3.5).  `raw` is supported for an inductive
  datatype.  For a class, trait, newtype or abstract type it is reported as not
  yet supported: their raw types come with their stages.  It is an error for a
  type parameter, and for a type that cannot declare an invariant: a type
  synonym, a subset type, a codatatype, a built-in type.  Since `raw` is
  retagged only before an identifier, `raw int` and `raw array<D>` do not
  parse.
- *Provided but not revealed.*  Where a datatype is exported by `provides`
  only, it is an abstract type, so `raw D` there is not yet supported (stage 5).
  This includes an export set that provides `D` and reveals a member whose
  signature mentions `raw D`; the export set is reported as inconsistent.
- *Emptiness.*  Until witnesses are supported, `D` is possibly empty: it does
  not satisfy `(0)` or `(00)`.  `raw D` has the datatype's own characteristics.
- *Call graph.*  A use of `D` as a type is a dependency on the datatype.  The
  extension condition of section 3.3 lands with verification.
- *Verification.*  `D` in a verified module is now a subset type whose
  constraint calls the invariants, and its conversions carry obligations.  So
  the verifier translates no module of a program that declares an invariant:
  it reports each invariant, and in each other module the first declaration it
  would have verified.  The first step verified the modules without
  invariants.  The translation cache (`--translation-cache`) keys a module on
  what the module depends on, so in the language server a module could be
  reused from before an unrelated module declared an invariant; it therefore
  reuses nothing in such a program, so that a cache never changes a result.
- *Open until verification.*  Under `--general-traits=datatype`, a `raw D`
  converts to a parent trait of `D` without the obligation that it satisfy
  `D`'s invariants.  Section 3.4 makes `raw D` a subtype of the raw parent
  only, so this conversion must carry the obligation before verification is
  enabled for a datatype with parents (stage 4).
- *Compilation.*  The derived declaration is not compiled: `D` and `raw D` are
  the same target type.
- *Printing.*  `/rprint` shows the derived declaration, in a comment after the
  datatype, and marks each raw member.

**Verification of datatypes (third step; the rest of stage 1 of
section 7.2).**

- *The gate.*  The invariants of an inductive datatype with no type parameters
  and no parent traits, declared in a module that refines no other, are
  verified.  The program-wide gate of the second step stays for every
  other invariant: if the program, libraries included, declares one, no module
  is translated, and each such invariant is reported with its reason (a
  datatype with type parameters, a datatype that extends a trait, a refining
  module, or a class, trait, newtype or abstract type).  It is decided per
  invariant, not per module, since the types of every module may depend on any
  invariant.  The conversion of a `raw D` to a parent trait under
  `--general-traits=datatype`, left open above, therefore cannot reach the
  verifier: a datatype with parents stays behind the gate until stage 4, which
  must add that obligation.
- *Encoding.*  Section 5.1's pattern, built from existing machinery instead of
  new axioms.  The derived declaration is translated as a subset type, so
  `$Is(v, Tclass.D) <==> $Is(v, Tclass.D#raw) ∧ D.invariant#0(v) ∧ …`.  The
  datatype is the raw type: `Tclass.D#raw` carries the constructor, allocation,
  box and discriminator axioms, in which a component declared with type `D` has
  the finished type.  The invariants are the Boogie functions of the
  `MemberInvariant` predicates.  An invariant is always a raw member, even one
  that never mentions `this`, so their axioms are guarded by the raw type.
  There is no separate `T#Inv`: the subset type's constraint is that
  conjunction.  The prelude is unchanged, and so is the Boogie of a program
  without invariants.  The raw type's class constant is `class.D#raw`, as `C?`
  takes the suffix for a class; `#` cannot occur in a Dafny name.
- *Conversions.*  Where a value is converted to `D` and its invariants are
  visible, membership in `raw D` is checked as before, and then each invariant
  is a separate assertion, "invariant might not hold", whose related location is
  that invariant; membership in `D` is then assumed, as for a subset type.  This
  is a new obligation description, `InvariantCheck`.  A member that is not raw,
  applied to a `raw D` (called, or selected as a value), checks
  that the receiver is a `D`.  So `Inc` in section 6.3 is reported once for each
  invariant, and a raw member's `x - y` is reported where `nat` needs it.
- *Witnesses* (section 3.6).  A witness member is a static const of type `D`,
  so checking it is the ordinary check of a const's initializer, and it is the
  witness of the derived declaration: `witness e` makes `D` auto-initializable,
  `ghost witness e` nonempty, and with neither `D` is possibly empty.  The
  derived declaration gets no well-formedness check of its own, since its
  constraint is the invariants, checked as predicates, and its witness is
  checked as a const.  Where the witness is compiled, the derived declaration is
  compiled as a subset type, under the name `D#` (sanitized for each target),
  whose default value is the witness; otherwise it is not compiled, and `D` and
  `raw D` are the same target type.  A datatype declares at most one witness,
  and none if it has no invariant, own or inherited (a refining declaration may
  supply one for inherited invariants, section 4.2).  A witness member of a
  generic datatype, or of another kind of type, is not yet supported.
- *The extension condition* (section 3.3) is `InvariantExtensionCondition`,
  run for each type with invariants after the reads restriction, and only if
  that reported nothing, since the cone analysis assumes a resolved,
  restriction-respecting body.  Decisions:
  - Clauses 3 and 4 apply to every type, not only to types that cannot involve
    references: the correction in section 3.3.  So the `Box` example moved from
    section 6.1 to section 6.2.
  - The cone includes the types of the components of `raw D`, since its values
    are built from them, and the constraints of the subset types, newtypes and
    types with invariants that it quantifies over, tests, converts to or passes
    as type arguments.  A generic subset type or newtype is analysed with the
    dependence of each of its type parameters, so a constraint that does not
    quantify over a dependent parameter is accepted.  A generic function given
    a dependent type argument is rejected, the first-stage form of clause 2.
  - Clause 1 accepts a bound variable that the resolver bounds by the elements
    of a collection, by a single given value, or by the subsets of a given set.
    If that collection holds raw values and the variable's type is not raw,
    ranging over it tests them against the type, so clause 5 rejects it.  A
    bound variable whose type is inferred as `raw D` makes `k is D` a test of a
    raw value; the program writes `k: D` to range over values of `D`.
  - Built-in arrow types.  `A -> B` and `A --> B` are subset types of `A ~> B`
    whose constraints quantify over `A`.  So an arrow type whose parameter type
    depends on `D`, wherever the cone reaches it, refers to the set of values of
    `D` (clause 1), and is reported there, since the built-in declaration has no
    place in the program.  Skipping these constraints was unsound: an invariant
    could test a lambda that requires something of every `D` against
    `D -> bool`, and `assert false` verified.
  - Statements inside expressions (`assert`, `assume`, `calc`, `reveal`, a
    lemma call or a `forall` statement, in a statement expression `S; E`) are
    proof hints and give no meaning, so they are not in the cone.  The
    argument has three parts.
    1. *They are not in the axioms.*  The condition exists so that the axioms
       of section 5.1 have a model (section 5.2).  Every axiom that mentions
       the extension of `T` (the definition and consequence axioms of the
       functions, constants and constraints in the cone, and the `$Is`
       equivalence of `T`) is built by the expression translator, which
       translates `S; E` as `E` alone.  A lemma that `S` calls contributes no
       axiom: its postcondition is used only at a call, after its
       precondition is checked.  So the axioms are exactly those of the
       program with every statement inside an expression erased.  That
       program satisfies the condition, since the analysis looks at nothing
       the erasure removes, so section 5.2 gives it a model, and that is a
       model of the axioms.
    2. *They are used only in proofs.*  In the well-formedness check of the
       enclosing function, `S` is proved (an `assert` or a `calc` step is
       checked; a lemma's precondition is checked, and the lemma is verified
       on its own; a `forall` statement's body proves its conclusion) and its
       conclusion is assumed after it.  Where the enclosing function's body
       is inlined into a proof obligation (`TrSplitExpr`), `S; E` is proved as
       `C ==> E`, with `C` the conclusion of `S`, and the call itself is then
       assumed.  Each use rests on a fact proved from the axioms, or, for
       `assume`, on the program's own unchecked assumption, as anywhere else.
       The axioms have a model, so a statement may refer to the extension of
       `T` freely (`assert forall e: T :: P(e)`, a test `this is T` of a raw
       `this`, a dynamically bound lemma call): it can only be an obligation
       that fails, never a source of inconsistency.
    3. *A caution particular to invariants.*  The inlining rule of part 2
       relies on `C` holding wherever the call is made, which Dafny ensures
       by checking `S` under the function's preconditions.  The
       well-formedness check of `invariant#k` also assumes the earlier
       invariants, which are not its preconditions.  So an invariant's body
       must never be inlined where the earlier invariants are not known.  It
       never is: `invariant#k` cannot be named in source, and the translator
       asserts each invariant whole, as a call, at a conversion
       (`CheckInvariantTypeMembership`), at `new;` and for a witness, while
       `TrSplitExpr` sees only source expressions.  A change that inlines an
       invariant's body must guard it with the earlier invariants.

    `git-issue-105-statements.dfy` holds the controls: invariants whose
    statements quantify over the type without a bound, test a raw `this`
    against it and call a lemma that does both are accepted; such a statement
    that is true is proved from the axioms, one that is false fails as an
    obligation, and `false` is not provable after either.
  - The hidden option `--unchecked-invariant-extension` turns the check off.  It
    exists for the vacuity controls V5 and V5b (section 6.4), which must show
    that the axioms of `Liar` and `Liar2` are inconsistent without it.  It is
    recorded as an input option, and in the options that the modular
    termination record keys.
- *Ordering.*  The resolver's analysis of subset type constraints visits the
  declarations in the call graph's order, in which a derived declaration is not
  a vertex, so a subset type of `D` could be analysed before `D`'s derived
  type.  It now analyses a declaration's base type first when that is still to
  be done.  Without invariants the call graph already puts base types first, so
  the order is unchanged.
- *Translation cache.*  The cache steps aside only for a program that
  declares an invariant whose verification is not yet supported: then no
  module is translated, and a module's key covers only the modules it depends
  on.  Otherwise a module's translation depends only on what its key covers,
  the source and resolution of the module and of the modules it depends on,
  and the options, `--member-invariants` among them.  So a module of a program
  whose invariants are all verified is cached as any module is.
- *Tests.*  `git-issue-105-datatypes.dfy` holds sections 6.1 and 6.3 for
  datatypes and the controls V1, V4 and V8; `git-issue-105-vacuity.dfy` holds V5
  and V5b; `git-issue-105-extension.dfy` holds section 6.2's datatype examples
  and each clause of the condition; `git-issue-105-compile.dfy` runs witnesses
  as default values in compiled code.  V7 is the declaration rule, already
  tested by `git-issue-105-codatatypes.dfy` and `git-issue-105-libraries.dfy`.
  Section 3.3's test of lambda bodies in methods belongs to classes (stage 3).

**Emptiness, the raw-typing audit and generic datatypes (fourth step; stage
2 of section 7.2).**

- *The gate* is lifted for generic datatypes: the invariants of an inductive
  datatype with no parent traits, declared in a module that refines no other,
  are verified, and such a datatype may declare a witness whatever its type
  parameters.
- *Emptiness.*  Every consumer of "is this value type inhabited" asks
  `Type.GetAutoInit`, directly or through `IsNonempty`, `HasCompilableValue`
  and the characteristics checks: definite assignment of locals and
  out-parameters, a havoc, a such-that, a new array without an initializer,
  `(0)` and `(00)`, the constructor rule for class fields, and the trimming of
  possibly empty bound variables from CanCall assumptions.  Its branch for
  subset types reads the witness kind, which the derived declaration has, and
  its branch for datatypes propagates through the components of a grounding
  constructor.  No consumer decides emptiness by asking whether a type is a
  `SubsetTypeDecl`; the translator's remaining branches on `SubsetTypeDecl`
  choose a constraint to check (subrange checks, a declaration's witness
  check), which a derived declaration takes as a subset type does.  So nothing
  needed to change.  A datatype with invariants behaves exactly as a subset
  type with the corresponding witness clause, under strict and relaxed definite
  assignment.  Each row of the vacuous-types table of section 5.3 that applies
  to datatypes has a test, with an `assert false` that must fail after each
  channel; the rows for classes, abstract types and refinement come with their
  stages.
- *Raw typing.*  The translator uses the raw type where the datatype is the
  subject (its constructor, allocation, box and discriminator axioms, and
  `this` in a raw member) and the declared type everywhere else.  With type
  parameters, `Tclass.D(X)` and `Tclass.D#raw(X)` take the same type
  descriptors, and the constraint of `Tclass.D(X)` is the invariants
  instantiated with `X`.
- *Variance.*  The derived declaration's type parameters keep the datatype's
  declared variance and their strictness, and the resolver checks that the
  invariants justify it (section 3.5, and "Variance by polarity" below).  This
  step first made them non-variant, whatever the datatype declared, so that
  `D<A>` was a subtype of `D<B>` only where `A` and `B` were the same type and a
  type using `D<X>` could not declare `X` co- or contravariant; the owner
  replaced that rule, and the later step implements the replacement.  Pre-types
  use the datatype's variance, since a subset type's pre-type is its base's.
- *Witnesses of generic datatypes.*  The witness member is the static const
  `witness#k: D<X, ...>`, as if written with the datatype's own type
  parameters, and the derived declaration's witness is `D<Y, ...>.witness#k`
  with the derived declaration's.  So it is checked for every instantiation.
  Where it is compiled, the derived declaration compiles as the generic subset
  type `D#<X, ...>`, whose default value is the witness at each instantiation.
- *The extension condition.*  A generic type with invariants that the cone
  reaches is analysed as a generic subset type is: its invariants are analysed
  as its constraint, with each of its type parameters dependent where the type
  argument depends on the type being checked, rather than as calls of generic
  functions.  A datatype `T` with a component of type `Box<T>`, where `Box`'s
  invariant does not quantify over its parameter, is accepted.  A member
  function of the reached type, called from its invariant, is a generic
  function, so clause 2 rejects the call where a type argument is dependent;
  the exact form of clause 2 is a stage 7 relaxation.  V5c, a generic form of
  V5 in which a `Liar3` needs a `P<Liar3>` whose invariant holds only where its
  type parameter is empty, is rejected, and verifies `false` without the check.
- *Tests.*  `git-issue-105-generics.dfy` holds the controls V1 and V4 over a
  type parameter, conversions between instantiations, and witnesses;
  `git-issue-105-variance.dfy` the variance a declaration may give;
  `git-issue-105-emptiness.dfy` and `git-issue-105-emptiness-errors.dfy` the
  rows of the vacuous-types table, the first also running `dafny audit` for
  bodiless and extern functions; `git-issue-105-vacuity.dfy` adds V5c and
  `git-issue-105-extension.dfy` the generic cases of each clause;
  `git-issue-105-compile.dfy` runs generic witnesses in compiled code, and
  `git-issue-105-raw-compile.dfy` is now verified first.

**Verification of classes (fifth step; stage 3 of section 7.2).**

- *The gate* is lifted for classes: the invariants of an inductive datatype
  or a class that declares no parent trait, in a module that refines no other,
  are verified.  A class that extends a trait stays behind the gate until
  traits are verified (stage 4), since `raw C` is then a subtype of `raw P`.
  So does an invariant in a trait, a newtype or an abstract type.
- *The types.*  As for a datatype, the class declaration is the raw type,
  here `raw C?`, and the names written as types are redirected:
  - `C?` denotes a derived declaration (`InvariantTypeDecl`) whose constraint
    is `v != null ==> ` the invariants.  Its parents are those of the class,
    `object?` and the parent traits; the class declaration itself has none.
  - `C` is the non-null type of `C?`, as before, and `C` is also a subtype of
    `raw C`.
  - `raw C` is a new declaration (`RawNonNullTypeDecl`), the non-null type of
    `raw C?`, a subtype of `raw C?` and of nothing else.
  - In Boogie they are `C?`, `C`, `C#raw?` and `C#raw`.  The dynamic type of
    an object is its class, so `$Is(o, Tclass.C)` is the dynamic type, non-null
    and the invariants.
  - A class with invariants contains `null`, so `C?` is auto-initializable, and
    a class may not declare a witness: its objects are the ones its
    constructors return, so it needs none.
  - A class pre-type stands for `C?`, not `raw C?`, when it is turned into a
    type, so that an inferred type is a subtype of `object?`; type refinement
    makes it raw where a raw type flows in.  A join of a raw reference type
    with a type of another class or trait does not exist.
- *The check at `new;`* (section 3.4).  Until `new;`, explicit or implicit,
  the new object is assumed to be a `raw C` only; there each invariant is
  asserted, "invariant might not hold", and from then on `this` is a `C`.  The
  control `Bad` of section 6.4 verifies if `new;` assumes `C`.  The type of
  `this` in a constructor is `C`, as in other members: before `new;` Dafny lets
  a constructor only assign and read `this`'s fields, and a stored field (a
  mutable field, or a `const` without an initializer) is a member of
  `raw C`, so the first division never uses `this` as a `C`.  A raw member's
  `this` is `raw C`.  `new by` blocks (open question 2) are not implemented.
- *Constructors.*  A class that declares or inherits an invariant must declare
  a constructor.  A constructor with no body (or an extern one) assumes the
  invariants of the object it returns, as a method assumes that its output of
  a subset type satisfies the constraint, so `dafny audit` reports it, and
  `{:extern}` gets the warning for a declaration with a contract.
- *Where a raw reference may go.*  `raw C` is not a subtype of `object` or of
  a trait, so a conversion of a raw reference to one of them is checked.
  Wherever a value whose type may contain a raw reference reaches a type not
  based on that class, the verifier first checks that the reference is a
  finished one, `C` or `C?` (`InvariantTypeDecl.FinishRawReferences`): at the
  value itself and, covariantly, in the elements of sets, multisets, sequences
  and maps, in the covariant type arguments of other types, and in the results
  of arrows.  A type parameter instantiated with `raw C` is no exception:
  `Id(r)` is a `raw C`, and `Id<object>(r)` is checked.  A parameter position
  of an arrow type is checked only for membership, since in the verifier's
  model `object` contains every reference that is not null.
  This is defence in depth: in a verified program every object that exists has
  passed `new;`, and `this` cannot escape before, so the run-time test and the
  logical one already agree.  The resolver accepts such a conversion, as it
  accepts one to a subset type, and leaves it to the verifier.
- *Frames.*  A raw reference in a `reads` or `modifies` clause carries no
  obligation, including in the default `decreases` that a function infers from
  its `reads` clause.  Section 5.3 listed frames among the places a raw
  reference must not reach, but a frame names locations: it neither assumes
  nor yields the invariants of the objects in it.
- *Type tests.*  For a reference that is not raw, `o is C` and `o is C?` test
  the dynamic type alone and compile, since every object of the class
  satisfies its invariants (section 3.4).  A test of a raw reference, `r is C`,
  evaluates the invariants, so it is ghost.  A test against a user's subset
  type of `C` stays ghost even where its own constraint is compilable, since
  the derived constraint of `C?` is a ghost predicate; treating it as a test of
  the dynamic type is a possible relaxation.
- *The extension condition.*  `Max` of section 6.2, whose invariant quantifies
  over `Max`, is rejected by the existing rule that a function body (an
  invariant's, a lambda's in a method) quantifies over a class type only with a
  bound, as section 3.3 expected; no new rule was needed.  Its message calls
  the context "a invariant definition"; the article comes from the existing
  message, which says "a function definition" and "a lambda expression" alike,
  so it is left as upstream has it.
- *Export sets*, settling the item left open after the first step.  A member
  invariant is part of the definition of its type, as a datatype's
  constructors are, so it is revealed wherever its type is, and nowhere else.
  A client to which the type is revealed sees the invariants and assumes them
  of every value; a client to which it is only provided sees an abstract type
  and assumes nothing of it.  Before this step an invariant was exported like
  any other member, only where an export set named it, which it cannot; so a
  client to which a datatype was revealed saw its constraint without the
  invariants' bodies, and could not use them.  `raw C` is visible where the
  class is revealed.  Where the class is only provided, `raw C` is not yet
  supported, as `raw D` is for a datatype (stage 5).
- *Compilation.*  `C`, `C?`, `raw C` and `raw C?` compile to the class.  The Go
  back end asks whether one type is a subtype of another to decide whether to
  emit a type assertion; for the class, which is a subtype of `object` in the
  target but not among Dafny's types, it now answers as the target does.
  Ignoring nullity, `C` is compared as `C?` and `raw C` as `raw C?`.
- *Translation cache.*  A class whose invariants are verified no longer makes
  the cache step aside.
- *Tests.*  `git-issue-105-classes.dfy` holds sections 6.1 and 6.3 for classes,
  the controls V2, V6 and `Bad`, and each place a raw reference may and may not
  go; `git-issue-105-class-errors.dfy` section 6.2's class examples and the
  resolver's rules, including the lambda bodies of section 3.3;
  `git-issue-105-class-witness.dfy` the witness rule;
  `git-issue-105-class-compile.dfy` runs type tests and raw references in
  compiled code; `git-issue-105-exports.dfy` and
  `git-issue-105-class-exports.dfy` export sets, for datatypes and classes;
  `git-issue-105-emptiness.dfy` adds a bodiless constructor to the audit.

**Verification of traits (sixth step; stage 4 of section 7.2).**

- *The gate* is lifted for traits, reference and general, and for the
  datatypes and classes that extend them: the invariants of inductive
  datatypes, classes and traits, declared or inherited, in a module that
  refines no other, are verified.  Still behind it: an invariant of a newtype
  or an abstract type, a newtype or abstract type that extends a trait with
  invariants (reported at its name), and any declaration with invariants,
  declared or inherited, in a refining module.  The messages now read "an
  abstract type" rather than "a abstract type".
- *The types.*  A datatype, class or trait that declares no invariant but
  inherits some gets the derived declarations that one that declares an
  invariant gets.  Its name written as a type must denote the derived
  declaration before the module's signatures are resolved, but whether it
  inherits invariants is known only from its resolved extends clauses.  So
  `CreateInheritedInvariantTypeDecls` resolves clones of the extends clauses
  first, into a sink (they are resolved again, with errors reported, in the
  usual place), and makes the derived declarations to a fixpoint, since a trait
  of the module may inherit invariants from another.  The derived declarations
  of classes and traits are now made after `DetermineReferenceTypes`, which
  decides which traits are reference types.  A reference trait `P` is treated
  as a class is: the trait declaration is `raw P?`, `P?` the derived
  declaration, `P` its non-null type, and `raw P` a `RawNonNullTypeDecl`.  A
  general trait `G` is treated as a datatype is: the declaration is `raw G`,
  and `G` the derived declaration.
- *The constraint* (section 4.4).  The invariants of a type, in the order its
  constraint conjoins them, are the inherited ones, in a post-order walk of
  the extends clauses, each trait once however many paths reach it, and then
  its own.  An inherited conjunct is `(v as raw P<args>).invariant#k()`, with
  the parent type arguments of the extends clauses; for a reference type it is
  guarded by `v != null`.  A diamond proves the top trait's conjuncts once.
- *Subtyping.*  `raw C` is a subtype of `raw P` for each parent trait `P` with
  invariants, and `C` of `P` through the subset-type axioms, since the
  conjuncts of `C` include those of `P`.  A raw value is not a subtype of
  `object`, nor of a parent trait without invariants: it reaches them only as
  a finished value of its own type, so that conversion checks all the
  invariants of `C`, the inherited ones included.  Through `raw P`, only those
  of `P` are checked, so a `P` may hold a value that is a `raw C` and not a
  `C` (`AShapeThatIsNotASquare` in `git-issue-105-general-traits.dfy`).  The
  Boogie parent axiom relates raw to raw (`raw C?` to `raw P?`, or `raw G`);
  relating the raw child to the finished parent would have been unsound.  The
  variance check of an extends clause uses the raw parent type.
- *Type tests.*  At run time every object satisfies the invariants of its
  class, the inherited ones included, since they read only constants and are
  checked before the constructor's `this` can be used.  So, from a type that
  is not raw, a test against a class or a reference trait tests the dynamic
  type alone and compiles, even where the static type does not tell the
  verifier that the object satisfies the class's own invariants.  A datatype
  has raw values at run time, so a test of a general trait's value against a
  datatype with invariants (`g is D`) is ghost, as is a test against a general
  trait with invariants; `g is raw D` compiles.  A test of a raw reference
  against a class or a trait with invariants stays ghost.
- *Joins.*  Where the least common supertype of two types has invariants, the
  join is its finished type (`P`, `P?` or `G`) if neither type is raw, and its
  raw type otherwise, with the type arguments joined by the declared variance
  (until the polarity step, finished only where the type arguments agreed).  Before this step a join
  of a raw reference type with any other class or trait did not exist.
- *Receivers.*  A member applied to a raw receiver is checked unless it is
  static, a stored field or a datatype destructor (`SpecialField`), or a raw
  member; otherwise the receiver is checked against the finished type of its
  own static type, all of its invariants included, even where the member is
  inherited from a trait without invariants.  Before this step only members of
  the receiver's own declaration were considered.
- *Well-formedness* (sections 4.1 and 4.4).  An invariant is well formed where
  the invariants before it in its type's constraint hold, as successive
  `requires` clauses are: the inherited ones, then the type's own earlier ones.
  The earlier steps checked each invariant with no assumption, which rejected a
  datatype whose second invariant divides by a component that its first one
  says is nonzero; this fixes that too.  The well-formedness check of each
  invariant gains one more `CanCall` assumption in the obligation inventory.
- *The extension condition* runs for every declaration with derived
  declarations, the inherited invariants in its cone, with the type parameters
  of each trait mapped through the extends clauses.  Two consequences:
  - The traits a type extends, the types that extend it, and `object` for a
    reference type, are `T`-dependent (section 3.3), so an invariant may not
    convert `this` to `raw P` or test it against `P`.  This is conservative: a
    raw ancestor depends on `T` only through its components, as section 3.3
    says of `raw U`.  It is a candidate relaxation (stage 7).
  - Clause 4 rejects a dynamically bound call, so the remark of section 4.1
    that an invariant may call a function a later class overrides does not
    apply: such an invariant is rejected.  An invariant may call a function
    with a body, which no type overrides.
- *Witnesses.*  A datatype that only inherits invariants may declare a witness,
  which is checked against the inherited invariants as well as its own; one
  that extends only traits without invariants, and so has none, may not (the
  syntactic check of the first step cannot see that, so the resolver reports
  it).  A trait may not declare a witness, a general trait included ("not yet
  supported"): its values are those of the types that extend it, each with its
  own witness or none, and what a trait's witness would promise is not yet
  settled.
- *Visibility.*  To extend a trait with invariants, a module must see them
  (section 4.1).  The existing rules already ensure it: a trait exported by
  `provides` alone is not a trait where it is imported, so the existing "can
  only extend traits" error rejects the extends clause, and an export set that
  reveals a trait must reveal the traits it extends.  The resolver asserts it.
  (An error for this case was removed at the controls step: no program reaches
  it.)
- *Two fixes to earlier steps.*  The parser now accepts `invariant` or
  `witness` directly after a method or function with no body (the member
  keyword is retagged before the specification's synchronization point); the
  first step's grammar missed it.  The printer names a general trait's derived
  declaration without `?`.
- *Translation cache.*  A trait whose invariants are verified, and a class
  that extends one, no longer make the cache step aside; an abstract type with
  an invariant still does.
- *Tests.*  `git-issue-105-traits.dfy` holds reference traits: V3, the check at
  `new;` of inherited invariants, a diamond, a trait of another module, raw
  receivers and joins; `git-issue-105-general-traits.dfy` general traits, the
  values of `raw G`, generic traits, witnesses that inherit, and a class that
  extends a general trait; `git-issue-105-trait-errors.dfy` the resolver's
  rules, including the paradoxes of section 3.3 built through a trait;
  `git-issue-105-trait-witness.dfy` the witness rule;
  `git-issue-105-wellformedness.dfy` the order of well-formedness assumptions;
  `git-issue-105-trait-compile.dfy` runs trait values and type tests in
  compiled code; `git-issue-105-trait-gates.dfy` what stays behind the gate.
  `git-issue-105-syntax.dfy` adds members after bodiless ones, and
  `git-issue-105-opacity.dfy` now verifies.

**Variance by polarity (the owner's decision, replacing the variance rule of the step for generic datatypes).**

- *The rule* is section 3.5's: a declared `+X` or `-X` of a type with
  invariants stands only where the invariants are monotone (antitone) in `X`,
  checked by polarity; otherwise the resolver reports an error at the type
  parameter, with the occurrence as a related location, naming it ("the
  universal quantifier over 'y: X' uses 'X' negatively", "the call of 'F',
  whose body is not known here, uses 'X' both positively and negatively").
  The derived declaration keeps the declared variance, so `D<nat>` is a
  subtype of `D<int>` for a covariant `X` and that conversion is not checked;
  a conversion against the variance is checked against the invariants.
- *Where it lives.*  `TypeParameterPolarity` in the resolver computes the
  polarity of type parameters in formulas and the least-fixpoint summaries of
  functions and constants, independently of invariants, so that the fix for
  https://github.com/erniecohen/dafny/issues/491 can apply it to the
  constraint of a subset type or newtype.  `CheckInvariantVariance` applies it
  to the invariants.  It runs after the extension condition, and only where
  resolution has reported no error.
- *Trust.*  The analysis trusts the variance every other type declares, as
  subtyping does (section 3.5).  It does not compute an "effective" variance of
  a subset type from its constraint: that would make this feature's rule
  differ from the subtyping relation the verifier uses everywhere else, and the
  hole it would close is issue 491's, whose fix belongs to subset types and
  newtypes.
- *What is opaque.*  A function is analysed by its body where its body is
  visible: in its own module, or where the module signature's visibility scope
  reveals it.  A function with no body (abstract, bodiless, or a trait
  function, so possibly dynamically dispatched) and a function exported by
  `provides` alone give each type argument both polarities.  The body of a
  function declared `opaque` is its meaning, and is analysed.  Deciding by
  visibility rather than by the body itself keeps a client's well-formedness
  independent of what an export hides.
- *Inherited invariants.*  An inherited invariant enters as the call of the
  trait's invariant with the extends clause's type arguments.  Traits are
  non-variant (a class or reference trait by the existing rule, and a general
  trait cannot pass a variant parameter to a non-variant one), so a variant `X`
  never reaches an inherited invariant today; the path is there for abstract
  types and newtypes.
- *Bounded quantifiers.*  `forall y: X | y in s :: P(y)` and
  `forall y <- s :: P(y)` still count as quantifiers over `X`, although where
  `s` is a value of type `set<X>` the quantifier ranges over `s` alone, which is
  the same at `A` and at `B`.  Recognising that is a candidate relaxation
  (stage 7 of section 7.2), with the condition of section 3.3 that bounds by a
  collection.
- *Statements inside expressions* are not analysed (section 3.5), for the
  reason the extension condition ignores them.
- *Joins and inference.*  The join of `D<A>` and `D<B>` is `D<C>`, `C` the
  join of `A` and `B` by the declared variance, where neither is raw.
- *Tests.*  `git-issue-105-variance.dfy` holds the resolver's controls: the
  declarations accepted only because `D<X>` keeps the declared variance, and
  one rejected declaration for each rule (`forall` under `+X`, `exists` under
  `-X`, the arrow's flip both ways, a non-variant type argument, a generic
  callee used negatively, a recursive one, a bodiless one, one not revealed, a
  negation, an equivalence, an `if` condition, a comprehension, a type test
  under `-X`, a constant, a function used as a value).
  `git-issue-105-generics.dfy` holds the verifier's positive controls: a
  covariant `L<nat>` used as an `L<int>` where the invariant is needed at
  `int`, and a contravariant `N<int>` used as an `N<Empty>`, each of which
  verifies only because the conversion is a subtyping (the check would need
  induction), and conversions against the variance, which are checked.

**Controls (the owner's rule for every feature).**  Every addition, meaning
every axiom or assumption, has a positive control: a test that verifies only
because the addition is there.  Every check has a negative control: a test
that is rejected exactly at that check.  A local mutation run confirms it.  It
disables each emission site of the translator, and each check of the resolver,
in turn, rebuilds, and runs the `git-issue-105` tests.  For each site, some
test changes its result.  A site that no test caught got a new control, or was
removed when no program can reach it.

- *Translator and resolver sites.*  "Producer" names the entries of
  `docs/dev/obligation-producers.json` (file, member, producer) that a site
  adds; a site that adds no obligation has none.  "Flips" lists the tests that
  change their result when the site is disabled.

  | Site | Kind | Where | Producer | Flips |
  |---|---|---|---|---|
  | earlier invariants assumed in the well-formedness of a later one | assumption | `BoogieGenerator.Functions.Wellformedness.cs`, `Check` | `Check/1`, `CanCallAssumptionForVerification` (2 to 3) | wellformedness |
  | the `$Is` axioms of `D` use its constraint | axiom | `BoogieGenerator.Types.cs`, the axioms of a subset type | none | datatypes, vacuity, classes |
  | each invariant checked at a conversion to `D` | check | `BoogieGenerator.TypeObligations.cs` | `CheckInvariantTypeMembership/10`, `Assert`, `AssertAndForget`, `CanCallAssumption` | datatypes |
  | the value is in `raw D` before the invariants are checked | check | same | `CheckInvariantTypeMembership/10`, the second `CheckSubrange` | generics (`Components`) |
  | a value converted to `C` is not null | check | same | `CheckInvariantTypeMembership/10`, the first `CheckSubrange` | classes (`RawQToCell`) |
  | a member that is not raw is applied to a raw receiver only if it satisfies the invariants | check | same | `CheckReceiverInvariants/5`, `CheckSubrange` | traits |
  | a raw reference reaches a type that is not raw only if it satisfies the invariants | check | same | `CheckTypeMembership/12`, `CheckSubrange`, `GetSubrangeCheck`, `CheckTypeMembership` | classes, traits |
  | the same, in the type test and the membership check of a raw reference | check | `BoogieGenerator.cs`, `GetSubrangeCheck` | `GetSubrangeCheck/8`, `GetSubrangeCheck` | classes (`TestRawObjectChecked`) |
  | the invariants checked at `new;` | check | `BoogieGenerator.TrStatement.cs`, `TrStmt` | `TrStmt/4`, `CheckSubrange` (1 to 2) | classes, traits |
  | the object is raw until `new;` | restriction | same | none | classes, traits |
  | the parent axioms of a raw class reach the raw types of its traits | axiom | `BoogieGenerator.cs` | none | traits, general-traits |
  | the constraint includes the inherited invariants | constraint | `InvariantTypeDecl.cs` | none | traits, general-traits |
  | the translator's gate for what is not yet verified | check | `BoogieGenerator.cs`, `DeclaresUnsupportedMemberInvariants` | none | trait-gates |
  | `D` keeps the declared variance | subtyping | `InvariantTypeDecl.cs`, `TypeParametersOf` | none | generics, variance |
  | variance by polarity | check | `TypeParameterPolarity.cs`, `CheckInvariantVariance` | none | variance |
  | the extension condition | check | `InvariantExtensionCondition.cs` | none | extension, trait-errors |
  | the reads restriction | check | `MemberInvariantChecks.cs`, `CheckReadsRestrictions` | none | reads |
  | the program-wide checks | check | `MemberInvariantChecks.cs`, `Check` | none | codatatypes |

- *Resolver errors.*  Each error that the feature adds has a negative control
  in the expect files named:

  | Error | Tests |
  |---|---|
  | an invariant or witness outside a type declaration | modifiers |
  | a witness in a class | class-witness |
  | a witness or an invariant in a codatatype | codatatypes, libraries |
  | a witness of a declaration other than a datatype (not yet) | trait-witness |
  | a witness without an invariant, two witnesses | trait-witness |
  | the declaration rule (an invariant and a codatatype in one program) | codatatypes, libraries |
  | the reads restriction | reads, class-errors |
  | an inherited witness | trait-errors |
  | a class with invariants and no constructor, an inherited constructor | class-errors |
  | `raw` applied to a type parameter | raw-errors |
  | `raw` of a type that is provided but not revealed | class-exports, raw-exports |
  | `raw` where it is not yet supported | not-yet, raw-errors, class-exports, raw-exports |
  | an opaque invariant | modifiers |
  | `allocated` in an invariant | class-errors, two-state |
  | the translator's gate | trait-gates, verify |
  | variance by polarity | variance |
  | `--member-invariants` without `--type-system-refresh` | option (command line); `MemberInvariantsRequireTypeSystemRefresh` in `DafnyCore.Test` (resolver); `MemberInvariantsTest.OptionsFromTheProjectFile` in `DafnyLanguageServer.Test` (a project file) |

- *Polarity controls.*  `git-issue-105-variance.dfy` has the negative ones
  (`forall` under `+X`, `exists` under `-X`, the arrow's flip, a non-variant
  type argument, a generic callee used negatively, an opaque callee, and the
  rest listed under variance by polarity above).  `git-issue-105-generics.dfy`
  has the positive ones, a covariant `L<nat>` used as an `L<int>` and a
  contravariant `N<int>` used as an `N<Empty>`.
- *Removed at this step.*  Four things that no test could catch:
  - The assumption, after the checks of a conversion to `D`, that the value is
    in `D`.  It is redundant: the introduction axiom of `D`, "`$Is(v, raw D)`
    and `CanCall ==> constraint` imply `$Is(v, D)`", derives it from the checks
    themselves.
  - The parser's error for `raw` before something other than a named type.
    `raw` is a keyword only before an identifier, so the error was
    unreachable.
  - The reads restriction's own errors for `old`, `fresh`, `allocated`,
    `unchanged` and two-state functions and lemmas.  The existing checks of a
    function body reject them first (`git-issue-105-two-state.dfy` holds the
    controls).
  - The error for extending a trait whose invariants are not revealed.  The
    existing rules reject such a program first (see the step for traits); the
    resolver now asserts it.
- *The IDE.*  `MemberInvariantsTest` in `DafnyLanguageServer.Test` sets the
  option in a project file.  It checks the diagnostics of the invariant's
  well-formedness and of a failing conversion, at the conversion with the
  invariant as related location, and the verification status of each.  It also
  checks hovers, document symbols, and that the reserved names `invariant#k`
  and `witness#k` are never shown, and that the error for the option without
  `--type-system-refresh` is reported in the project file.  The test found one
  fault, fixed at this step: the hover and the verification tree showed the
  reserved names.
