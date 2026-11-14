# The `Axioms` module

The `Axioms` module is the one place in the standard library for facts that are
true in Dafny's intended model but cannot be proved from its axiomatization.

Each fact is a lemma with the attribute `{:axiom}` and no body.  A lemma without a
body is a procedure, not a global axiom: its conclusions are available only at an
explicit call, and only for the arguments of that call.  So a program that does
not call one of these lemmas is unaffected by it, and importing the module adds
nothing to the verifier's context.  `dafny audit` lists each lemma as an
assumption.  Each lemma's documentation in `Axioms.dfy` states the fact, why it is
true, and the restrictions that keep it sound against Dafny's encoding.

## `Std.Axioms.FunctionExtensionality`

Two total functions that agree at every argument are equal:

- `Nullary(f, g)` for `() -> B`, given `f() == g()`;
- `OnInt(f, g)`, `OnReal(f, g)`, `OnBool(f, g)` and `OnOrdinal(f, g)` for
  `int -> B`, `real -> B`, `bool -> B` and `ORDINAL -> B`, given
  `forall x :: f(x) == g(x)`;
- `OnIntInt(f, g)` for `(int, int) -> B`, given `forall x, y :: f(x, y) == g(x, y)`.

The result type `B` is arbitrary.  The domain is not.  The fact is stated only for
total arrows (`->`), and only for domains in which every value of the verifier's
representation is a value of the type and which have no proper supertype.  A
generic version over an arbitrary domain `A` would be unsound: with `A := nat`,
the values `(x: int) => if x < 0 then 7 else x` and `(x: int) => x` agree on every
`nat`, but both are also `int ~> int` values, which differ at `-1`.  For the same
reason there is no version for datatypes, `char`, collections, partial arrows
(`-->`) or heap-reading arrows (`~>`).

<!-- %check-verify -->
```dafny
import Std.Axioms.FunctionExtensionality

datatype R = R(f: int -> int, p: bool -> bool)

lemma EqualRecords()
  ensures R(x => x + x, b => !b) == R(x => 2 * x, b => if b then false else true)
{
  FunctionExtensionality.OnInt((x: int) => x + x, (x: int) => 2 * x);
  FunctionExtensionality.OnBool((b: bool) => !b, (b: bool) => if b then false else true);
}
```

## `Std.Axioms.Reals`

`Complete(s, w, b)`: every nonempty set of reals that is bounded above has a least
upper bound.  `UpperBound(s, b)` holds when `b` is an upper bound of
`s: iset<real>`.  The lemma has no preconditions; its conclusions hold only when
`w in s` and `UpperBound(s, b)`.  [Std.RealCompleteness](RealCompleteness.md)
builds `Sup` and `ExistsLub` on it, and is the interface to use.
