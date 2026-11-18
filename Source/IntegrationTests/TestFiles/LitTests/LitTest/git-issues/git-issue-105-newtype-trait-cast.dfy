// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Found with member invariants (https://github.com/erniecohen/dafny/issues/105), but without the
// option: the verifier failed an internal assertion ("No translation implemented from int to G") on a
// conversion from a newtype to a trait that it extends and that is not a reference type, since the
// newtype's base type, which the translation looked at, does not show the trait.  The value is
// boxed, as a datatype's is.  An invariant that a newtype inherits from a trait needs the same
// conversion.  The last assertion is a negative control.

trait G {
  const k: int := 1
  function F(): int { k }
}

newtype N extends G = x: int | x < 10

method M(n: N) {
  var g := n as G;
  var m := g as N;
  assert m == n;
  assert g.F() == 1;
  assert g is N;
}

method Control(n: N) {
  var g: G := n;
  assert g.F() == 2; // error
}
