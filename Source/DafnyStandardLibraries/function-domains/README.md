# Total function domains

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
