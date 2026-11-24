// Defining an abstract type can turn an inherited pattern variable into a constructor.
abstract module Spec { type T }
abstract module M {
  import X := Spec
  function F(t: X.T): int { match t case foo => 1 case _ => 2 }
  lemma P(t: X.T) ensures F(t) == 1 { }
}
module Impl refines Spec { datatype T = foo | bar }
module Rebound refines M {
  import X := Impl
  lemma Bad() ensures false { P(X.bar); }
}

// The same attack through an opened copy must be rejected.
abstract module OpenM {
  import opened X := Spec
  function F(t: T): int { match t case foo => 1 case _ => 2 }
  lemma P(t: T) ensures F(t) == 1 { }
}
module OpenRebound refines OpenM {
  import opened X := Impl
  lemma Bad() ensures false { P(bar); }
}
