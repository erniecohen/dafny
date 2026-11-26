# Function domains

`Functions.dfy` is the opt-in source library `Std.FunctionDomains`. Verify it
and clients together with `--additional-axioms` (explicit-call extensionality)
or `--function-extensionality-axiom` (also automatic equality instances):

```sh
dafny verify function-domains/Functions.dfy Client.dfy --additional-axioms
```

Paths in this example are relative to `Source/DafnyStandardLibraries`.
The library's own `dfyconfig.toml` checks all its proofs with additional axioms.
It is separate from the default precompiled standard library, so programs that
do not opt in and supply this source file are unaffected.

Import `Std.FunctionDomains`, then select `Arity0`, `Arity1`, `Arity2`, `Arity3`
or `Arity4`. Each supplies:

- `Restrict(f)`: an ordinary ghost function with a checked eta-expansion body,
  whose value has exactly the requested argument domain and agrees with `f` there.
- `ExactDom(f)`: a compiler-defined predicate saying that the value's domain is
  exactly that product, rather than a proper superset. This intrinsic has no
  body or source postcondition and introduces no source assumption.
- `Restriction(f)`: a checked lemma giving exactness, agreement and the identity
  criterion `ExactDom(f) <==> Restrict(f) == f`.
- `Extensionality(f, g)`: a checked explicit-call lemma, requiring exact domains
  and pointwise agreement, and proving ordinary Leibniz equality.

`Equal(f, g)` is the checked equality predicate used inside `Extensionality`.
Its annotation enables the intrinsic instance at that explicit call. No
body-less extensionality lemma, `assume` or `{:axiom}` is used. Ordinary equality
without an explicit call still does not gain extensionality under
`--additional-axioms` alone. Domain type parameters of `Extensionality` require
`(!new)` so its pointwise quantifiers do not depend on allocation; results may
have any type. Partial (`-->`) and heap-reading (`~>`) values are excluded. `ORDINAL` cannot be
instantiated as a generic type argument in Dafny; monomorphic explicit-call
helpers for that type are not supplied by this source API.

For domains that may contain references, supply `Pointwise.dfy` instead (it
includes `Functions.dfy`) and import `Std.FunctionPointwise`. `Arity1` through
`Arity4` each supply:

- `Pointwise(f, g)`: agreement at **every** correctly typed argument tuple,
  including values containing references that have not been allocated. This
  compiler-defined predicate has no heap argument. It is stronger than an
  ordinary `forall x: A :: f(x) == g(x)` when `A` may contain references:
  that source quantifier implicitly ranges over currently allocated values.
- `Extensionality(f, g)`: a checked lemma requiring `Pointwise(f, g)` and exact
  domains, and proving Leibniz equality. Neither domain nor result types require
  `(!new)`. Agreement on allocated values alone cannot establish its premise.

```dafny
import P = Std.FunctionPointwise

class Cell { var value: int }

lemma IdentityOnReferences() {
  var f := (c: Cell) => c;
  var g := (c: Cell) => if true then c else c;
  assert P.Arity1.Pointwise<Cell, Cell>(f, g);
  P.Arity1.Extensionality<Cell, Cell>(f, g);
  assert f == g;
}
```

Verify this example with `function-domains/Pointwise.dfy` and
`--additional-axioms --verify-included-files`. Explicit type arguments keep
reference domains precise when inference could choose a nullable supertype.
Pure closures may capture references or snapshots of field values; a function
that reads a field on each application has type `~>` and fails the total-arrow
argument check. Partial (`-->`) arrows are also excluded. Exactness and
restriction still come from `Std.FunctionDomains`.

The compiler's `{:functionDomain "pointwise"}` intrinsic supports every arity,
including zero; the companion convenience modules cover one through four
because nullary arrows already have an allocation-independent interface.

For example, two functions on all integers may agree only on natural numbers.
Narrowing their static type does not change their values or domains. Restrict
both to `nat` before calling extensionality:

```dafny
import FD = Std.FunctionDomains

lemma RestrictedAgreement() {
  var f: nat -> int := (x: int) => if x < 0 then 7 else x;
  var g: nat -> int := (x: int) => x;
  var r: nat -> int := FD.Arity1.Restrict<nat, int>(f);
  var s: nat -> int := FD.Arity1.Restrict<nat, int>(g);
  FD.Arity1.Extensionality<nat, int>(r, s);
  assert r == s;
}
```

The verifier's intrinsic annotations support every arity, using type parameters
`<A1, ..., An, B>` in argument/result order. Further convenience modules can
follow the checked bodies in this file. For nullary functions, the argument
domain contains the single empty tuple; products with an empty component are
empty. Exactness compares sets of tuples, not identities of type tags.

The full performance trial and automatic embedding of this opt-in source API
in a precompiled standard-library interface are not claimed here. See
https://github.com/erniecohen/dafny/issues/374.

For partial arrows, supply `Partial.dfy` with `--additional-axioms` and import
`Std.PartialFunctionDomains`. `Arity0` through `Arity4` supply `ExactDom`,
`Pointwise`, `Equal` and the checked `Extensionality` lemma. `Pointwise` means
that preconditions agree on every correctly typed tuple, including unallocated
arguments, and results agree wherever that common precondition holds. Exact
domains are still required; viewing a wider function at a narrower arrow type
does not restrict its identity. Differences in body expressions outside the
precondition do not distinguish partial values.

```dafny
import P = Std.PartialFunctionDomains

lemma PartialAgreement() {
  var f: int --> int := (x: int) requires x >= 0 => x;
  var g: int --> int := (x: int) requires x >= 0 => if x < 0 then 7 else x;
  P.Arity1.Extensionality(f, g);
  assert f == g;
}
```

The lemma does not grant a call outside the precondition. Agreement only on the
intersection of two different preconditions does not satisfy `Pointwise`.
Heap-reading arrows are excluded. For subset-domain lambdas,
`--heap-free-lambda-facts` can expose their precondition facts at every heap
and establish the full-domain premise. Ordinary partial-arrow equality still
needs this explicit call; the automatic equality option continues to cover
total arrows. Partial restriction helpers and automatic partial equality are
not provided in this milestone.
