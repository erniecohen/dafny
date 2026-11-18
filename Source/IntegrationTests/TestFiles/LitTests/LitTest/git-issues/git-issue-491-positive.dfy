// RUN: %exits-with 4 %verify --type-system-refresh --general-traits=datatype --general-newtypes "%s" > "%t"
// RUN: %exits-with 0 %resolve "%s" >> "%t"
// RUN: %exits-with 4 %verify --legacy-unchecked-type-variance "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Accepted constraints. Each FalseControl has a reachable assertion as its only error.
module Covariance {
  type Empty = x: int | false witness *
  ghost predicate Some<X(!new)>() { exists y: X {:nowarn} :: true }
  ghost predicate Rec<X(!new)>(n: nat) {
    n == 0 || (Some<X>() && Rec<X>(n - 1))
  }
  datatype G<+X(!new)> = G(x: X)
  datatype Box<+X(!new)> = Box(b: bool)
  type Existential<+X(!new)> = s: Box<X> | s.b ==> (exists y: X {:nowarn} :: true) witness *
  type Covariant<+X(!new)> = s: Box<X> | s.b ==> (exists y: G<X> {:nowarn} :: true) witness *
  type Fixpoint<+X(!new)> = s: Box<X> | s.b ==> Rec<X>(1) witness *
  newtype Numeric<+X(!new)> = n: int | n == 0 ==> Some<X>() witness *
  lemma Accepted() {
    assert 0 is nat;
    assert Some<nat>();
    var witnessValue: G<nat> := G(0);
    assert exists y: G<nat> {:nowarn} :: y == witnessValue;
    var a: Existential<nat> := Box(true);
    var b: Existential<int> := a;
    var c: Covariant<nat> := Box(true);
    var d: Covariant<int> := c;
    var e: Fixpoint<nat> := Box(true);
    var f: Fixpoint<int> := e;
    var g: Numeric<nat> := 1;
    var h: Numeric<int> := g;
  }
  lemma FalseControl() { Accepted(); assert false; }
}
module Contravariance {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(x: X) { false }
  datatype Box<-X(!new)> = Box(b: bool)
  type S<-X(!new)> = s: Box<X> | s.b ==> (forall x: X :: F(x)) witness *
  lemma Accepted() {
    var s: S<int> := Box(false);
    var t: S<Empty> := s;
  }
  lemma FalseControl() { Accepted(); assert false; }
}
module TypeTest {
  trait Tr { }
  datatype Item extends Tr = Item
  datatype Box<+X extends Tr> = Box(o: Tr)
  type S<+X extends Tr> = s: Box<X> | s.o is X witness *
  lemma Accepted() {
    var s: S<Item> := Box(Item());
    var t: S<Tr> := s;
  }
  lemma FalseControl() { Accepted(); assert false; }
}
