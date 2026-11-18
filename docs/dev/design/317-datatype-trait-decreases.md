# Decreases between a datatype and a trait it extends

https://github.com/erniecohen/dafny/issues/317, split from
https://github.com/erniecohen/dafny/issues/103.

## Problem

A trait that does not extend `object` is not a reference type, so its values are
boxed (Boogie type `Box`). In the consistent decreases order
([`docs/dev/decreases-order.md`](../decreases-order.md)) such a trait has no
order of its own, and `CompatibleDecreasesTypes(C, A)` is false for a datatype `C`
and a trait `A` it extends. So a caller in `C` with `decreases this` cannot be
compared with a callee of `A` with `decreases this`, and every such termination
check is emitted as `assert false`:

```dafny
trait A { ghost predicate has(d: int, b: bool) decreases this }
datatype C extends A = C(a: A) {
  ghost predicate has(d: int, b: bool) decreases this { a.has(d, b) } // failed
}
```

Two values of such a trait are not comparable either; with the option off, a
`decreases x` with `x: A` in a recursive function that passes another `A` value
reaches `Contract.Assert(ty0.IsRefType)` in `ComputeLessEq` and crashes the
verifier. That crash is independent of this change and is not fixed by default.

## Change

The new option `--trait-rank-decreases` (default false, no effect with
`--legacy-decreases-order`) puts the values of every trait that is not a
reference type into the structural-rank family of sequences and inductive
datatypes:

- `CompatibleDecreasesTypesForComparison` accepts any pair from
  {sequence, inductive datatype, non-reference trait} in which one side is a
  non-reference trait. Pairs without such a trait are decided as before.
- `RankFunction` of a non-reference trait is `BoxRank`, so a datatype against a
  trait compares `DtRank(d)` with `BoxRank(b)`, a sequence compares `Seq#Rank(s)`
  with `BoxRank(b)`, and two trait values compare `BoxRank` with `BoxRank`
  (equality, `<` and `<=` of the ranks).
- In `CheckCallTermination` the comparison takes its static types from the
  callee's decreases expressions as substituted with the callee's formals (`e0`),
  which is the expression that is translated. A trait-typed formal can receive an
  argument of a datatype subtype; the direct substitution would have the
  subtype's type but not its Boogie representation.

With `--additional-axioms` as well, a field of an inductive datatype constructor
whose type is a non-reference trait gets the axiom that a type-parameter field
already has:

```boogie
axiom (forall a0..an :: { #C.Ctor(a0..an) } BoxRank(ai) < DtRank(#C.Ctor(a0..an)));
```

This is a new fact, so it joins `--additional-axioms` with the fork's other
axioms (AGENTS.md, "One option for all added axioms"). The comparison is a
change to the language's termination order, not an axiom, and has its own
option. Without the axiom, the comparison alone proves only what follows from
`BoxRank($Box(d)) == DtRank(d)` (for example, a downcast `x as D` relates `x`
to `D`'s fields), and the issue's program needs both options.

Nothing is emitted differently with either option off: each new path tests its
option first.

## Soundness

### Model

Take the model the existing rank axioms already use
([`docs/dev/additional-axioms.md`](../additional-axioms.md#model)): a value of an
inductive datatype is a well-founded tree, possibly infinitely branching, and
`DtRank` and `Seq#Rank` are ordinal ranks, with every existing rank axiom
(datatype, type-parameter, `seq`, `set`, `multiset`, `map`, `imap` and, with
`--additional-axioms`, arrow fields; `Seq#Rank` of an element, `Take`, `Drop`
and the `Take`/`Drop` append) true. Values of other types, including references,
codatatype values, newtype values and values of abstract types, are leaves.

Define, for every Dafny value `v`, `ρ(v)` as `DtRank(v)` for an inductive
datatype value, `Seq#Rank(v)` for a sequence, and `0` otherwise, and interpret
`BoxRank($Box(v))` as `ρ(v)`.

- The prelude axiom `BoxRank($Box(d)) == DtRank(d)` holds by definition.
- The type-parameter field axiom `BoxRank(ai) < DtRank(#C.Ctor(..))` holds: if
  the field's value is a datatype value or a sequence it is a child of the
  constructor value, so its rank is smaller; otherwise it is a leaf, with rank
  `0`, and a constructor that has a field has rank at least `1`.
- The new axiom for a trait-typed field is the same statement about the same
  kind of value, a box, so it holds for the same reason. The dynamic type of the
  field's value is irrelevant: a datatype or sequence value is a child, anything
  else is a leaf.
- No other axiom mentions `BoxRank`, and the arrow-field axioms of
  https://github.com/erniecohen/dafny/issues/326 use it only for type-parameter
  results, where the same argument applies.

Nothing constrains `BoxRank` of a boxed reference, codatatype or other leaf, so
the verifier can never prove anything about it beyond what holds for `0`.

A cyclic value through two datatypes that extend the same trait would need
`p == P(Q(p))`, or a deeper nesting. No such value exists: the trees are
well-founded, so `ρ(P(Q(p))) > ρ(Q(p)) > ρ(p)`. A cycle through the heap, a
datatype value holding an object whose mutable field holds that value, is not a
cycle of values: the reference is a leaf.

### Order

The structural-rank family's component relations are, in the model,
`s(x, y) = ρ(x) < ρ(y)` and `w(x, y) = ρ(x) <= ρ(y)` on the values of
sequences, inductive datatypes and non-reference traits. Ordinal `<` is
well-founded, `<=` is a preorder containing `<`, and both absorption laws hold,
so this is a component pair of the kind `docs/dev/decreases-order.md` lifts to
tuples, with TOP padding. Its argument for the lifted tuple order (the least
coordinate decisive infinitely often) applies unchanged. Every other family is
as before: no pair across the rank family and another family is comparable, so
an incompatible pair still stops the comparison with false.

Every comparison the option emits is a statement about `DtRank`, `Seq#Rank`
and `BoxRank`, which in the model means a statement about `ρ`. A verified
termination check is true in the model, so along every call the callee's
decreases tuple is strictly below the caller's in the lifted order.

Dynamic dispatch is covered the same way: a call to a trait member is checked
against the trait member's decreases clause, and the override check states that
the implementation's clause is weakly below the trait member's at the same
arguments. Under the option, the trait member's `this` is the boxed receiver
and the datatype implementation's `this` is the unboxed value, and
`ρ($Box(d)) = ρ(d)`, so the two views of the receiver have the same rank.

When the implementation belongs to a class, a codatatype, a newtype or an
abstract type, its `this` is not in the rank family. Its measure can be compared
with the trait's only through an earlier coordinate or through a component it
declares itself; a component of the rank family that it declares (for example
`decreases back` with `back: C`) must be weakly below `BoxRank` of its boxed
receiver, which nothing proves, and which in the model is `0`. Either way the
override check is a true statement about `ρ`, so the descent argument above is
unaffected.

So every infinite chain of verified calls would give an infinite descending
chain in a well-founded order, and none exists. No counterexample was found.

### Inference

Dafny infers `decreases this` for a member only when the receiver's type is
ordered and not a reference type (`InferDecreasesClause.cs`). A non-reference
trait is not ordered, so a trait member with no `decreases` clause has the
empty clause, which is TOP. This change does not alter inference: a class that
extends the trait would then need its own override to be below the trait's
`this`, which a reference cannot be. So the issue's literal program still fails
at `a.top()` (callee TOP, caller `this`). Writing `decreases this` on the trait
member makes it verify.

## Tests

`git-issues/github-issue-317.dfy` runs, under both resolvers and with each
option off and on:

- `github-issue-317-positive.dfy`: the issue's program with `decreases this` on
  `A.top` and `var ad: A := this` (the legacy resolver rejects `this as A`), two
  datatypes extending one trait with a size function and a lemma, a method and a
  lemma that recurse through two trait fields, and a tuple `decreases this, n`
  whose recursive call increases `n`. It verifies only with both options;
- `github-issue-317-false-control.dfy`: `this` viewed as the trait (equal rank),
  a cycle through two datatypes (`Q(this).f()` in `P`), a trait value that is not
  a component of `this`, a class that extends the trait and calls back into the
  datatype that holds it, two codatatypes that extend the trait, an `assert false`
  after calls that use the new order and an `ensures false` function that
  recurses through a downcast. Every call marked `FAIL` fails in every setting;
- `github-issue-317-downcast.dfy`: with `--additional-axioms` alone, recursion
  through `x.next as L`, a `<` lemma, and an `ensures false` vacuity control;
- the issue's literal program, which fails at `a.top()` in every setting (see
  "Inference");
- with the option on, `github-issue-317-trait-pairs.dfy`: two trait values
  compared with each other (a constant first coordinate and a decreasing second,
  a downcast to a field), and the false controls `Swap(y, x)`, `Same(x)` and
  `Up(Node(x))`. With the option off these crash, as described above;
- with `--legacy-decreases-order`, the positive file, which then fails as with the
  option off.
