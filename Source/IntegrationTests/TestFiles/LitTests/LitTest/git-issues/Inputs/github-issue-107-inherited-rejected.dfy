// A copying import cannot take the place of an inherited abstract import unless the copied module
// refines the target of that import (https://github.com/erniecohen/dafny/issues/408).

abstract module Idempotent {
  type T
  function F(t: T): T
  lemma Idem(t: T)
    ensures F(F(t)) == F(t)
}

module Constant refines Idempotent {
  datatype T = Zero | One
  function F(t: T): T { Zero }
  lemma Idem(t: T)
    ensures F(F(t)) == F(t)
  { }
}

// Negation has the declarations of Idempotent but does not refine it, and its F is not idempotent.
module Negation {
  type T = bool
  function F(t: T): T { !t }
  lemma Idem(t: T)
    ensures true
  { }
}

abstract module User {
  import A : Idempotent
  lemma Twice(t: A.T)
    ensures A.F(A.F(t)) == A.F(t)
  {
    A.Idem(t);
  }
}

module BindsOther refines User {
  import A := Negation  // error: Negation is not a refinement of Idempotent
  lemma Unsound()
    ensures false
  {
    Twice(true);
  }
}

// The copy that takes the place of the abstract import and another copy have different types.
module BindsOneOfTwo refines User {
  import A := Constant
  import B := Constant
  lemma Mix(b: B.T)
    ensures A.F(b) == A.Zero  // error: a B.T is not an A.T
  { }
}
