// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --solver-path "%review-z3" --cores 1 "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --solver-path "%review-z3" --cores 1 --additional-axioms "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --solver-path "%review-z3" --cores 1 --member-invariants "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=full --general-newtypes=true --solver-path "%review-z3" --cores 1 --member-invariants --additional-axioms "%s" >> "%t"
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

// The original recursive-receiver case from https://github.com/erniecohen/dafny/issues/472.
// It uses the same boxing translation, inside a function definition and its call checks.
module A {
  trait T { function F(n: nat): nat }
  newtype N extends T = x: int | 0 <= x {
    function F(n: nat): nat { if n == 0 then 0 else (this as T).F(n - 1) }
  }
}

module RecursiveCastControls {
  import A

  lemma Witness() {
    var n := 0 as A.N;
    var t := n as A.T;
    var back := t as A.N;
    assert back == n;
    assert t is A.N;
    assert n.F(0) == 0;
    assert t.F(1) >= 0;
  }

  lemma FalseAfterCast() {
    var n := 0 as A.N;
    var t := n as A.T;
    var result := t.F(0);
    assert false; // error
  }

  lemma DowncastRequiresMembership(t: A.T) {
    var n := t as A.N; // error
  }

  lemma NewtypeConstraint() {
    var n := -1 as A.N; // error
  }

  lemma CallArgumentConstraint() {
    var n := 0 as A.N;
    var t := n as A.T;
    var result := t.F(-1); // error
  }
}
