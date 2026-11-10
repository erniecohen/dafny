# The `RealCompleteness` module

The `RealCompleteness` module supplies the completeness of `real`:
every nonempty set of reals that is bounded above has a least upper bound.

Dafny's `real` is axiomatized as an ordered field, with `.Floor` and the embedding
of the integers, but without completeness, so facts such as the existence of a
square root of 2, or the intermediate value theorem for polynomials of arbitrary
degree, cannot be proved from it alone.  Completeness cannot be derived from the
ordered-field laws, so this module assumes it, in exactly one lemma:

- `upper(s, b)` holds when `b` is an upper bound of the set `s: iset<real>`.
- `Complete(s, w, b)` is the trusted fact, an `{:axiom}` lemma with no
  preconditions.  When `w in s` and `upper(s, b)`, the real `u` it returns is an
  upper bound of `s` that is at most every upper bound of `s`.  For any other
  arguments it says nothing about `u`.  The fact is true of the real numbers ℝ,
  which are Dafny's model of `real`.  `dafny audit` reports `Complete` as this
  module's one assumption.

The rest of the module is verified from `Complete`:

- `Eligible(s)`: `s` is nonempty and bounded above;
- `IsLub(s, u)`: `u` is the least upper bound of `s`;
- `ExistsLub(s)`: an eligible set has a least upper bound;
- `UniqueLub(s, u, v)`: a set has at most one least upper bound;
- `Sup(s)`: the least upper bound of `s` when `s` is eligible, and `0.0` otherwise.

A lemma without a body is a procedure, not a global axiom: its conclusions are
available only at an explicit call, so a program that does not call `Complete`
(directly, or through `ExistsLub` or `Sup`) is unaffected by it.

For example, the supremum of the open interval from 0 to 1 is 1:
<!-- %check-verify -->
```dafny
import opened Std.RealCompleteness

lemma SupOfInterval()
  ensures Sup(iset x: real {:nowarn} | 0.0 < x < 1.0) == 1.0
{
  var s := iset x: real {:nowarn} | 0.0 < x < 1.0;
  assert 0.5 in s;
  assert upper(s, 1.0);
  var u := Sup(s);
  if u < 1.0 {
    var m := if u < 0.5 then 0.75 else (u + 1.0) / 2.0;
    assert m in s;
  }
}
```
