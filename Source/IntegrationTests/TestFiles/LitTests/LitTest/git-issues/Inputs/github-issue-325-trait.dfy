// A user-written {:opaque_reveal} does not make a trait lemma a reveal lemma: C must implement L,
// or a call c.L() would assume L's postcondition with no proof.
trait T {
  lemma {:opaque_reveal} L() ensures false
}
class C extends T { }
method M() {
  var c := new C;
  c.L();
  assert false;
}

// A generated reveal lemma still needs no implementation.
trait U {
  opaque function f(): int { 1 }
}
class E extends U { }
