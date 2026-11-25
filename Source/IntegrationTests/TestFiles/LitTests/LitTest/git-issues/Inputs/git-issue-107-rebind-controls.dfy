module Q {
  class C { constructor () { } }
  function F(): int { 1 }
}
module Q1 refines Q { }
module M {
  import X := Q
  import Z := Q
  lemma Distinct(o: object) ensures o is X.C ==> !(o is Z.C) { }
}
module R refines M {
  import Q1
  import M
  import X := Q1
  lemma SameAsTarget(o: object) ensures o is X.C ==> o is Q1.C { }
  lemma SameAsPrevious(o: object) ensures o is X.C ==> o is M.X.C { }
  lemma SameAsOther(o: object) ensures o is X.C ==> o is Z.C { }
  method Vacuity() ensures false {
    var c := new X.C();
    Distinct(c);
    assert X.F() == 1;
  }
}
abstract module Facade {
  import I : R
  import R
  lemma Same(o: object) ensures o is I.X.C ==> o is R.X.C { }
  lemma Different(o: object) ensures o is I.X.C ==> !(o is R.X.C) { }
}
